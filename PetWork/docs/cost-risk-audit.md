# PetWork maliyet riski denetimi

Denetim tarihi: 10 Eylül 2026. Depo gerçek barındırma, Supabase, Resend ve Google Cloud planlarını içermez; bu nedenle parasal örnekler ilk ücretli kademe liste fiyatıyla üst-sınır hesabıdır, fatura tahmini değildir.

## Kullanıma göre büyüyen kalemler

| Kalem | Kod kanıtı | Saldırı/hata etkisi | Yeni koruma |
|---|---|---|---|
| Google Places API (New) | `Services/GooglePlacesService.cs:62,127,157,190,221,254,286` | Eski IP limitleriyle bir IP arama uçlarını teorik olarak 12/dk = 17.280/gün çağırabiliyordu. İlk ücretli kademedeki $32/1.000 Pro Search fiyatıyla bu, ücretsiz kota sonrası yaklaşık **$552,96/gün/IP** eder. Autocomplete 90/dk ile 129.600/gün ve $2,83/1.000 üzerinden yaklaşık **$366,77/gün/IP** olabilir. Botnet doğrusal büyütür. | Uçlar artık oturum ister. Kullanıcı başına 20 Search ve 100 Autocomplete/gün; veritabanında kalıcı sayaç. Üst sınırlar ücretsiz kota hesaba katılmadan sırasıyla $0,64 ve $0,283/kullanıcı-gün. Google proje kotası ayrıca şarttır. |
| Resend işlemsel e-posta | `Services/EmailVerificationService.cs:92-98`, `Services/PasswordResetEmailSender.cs:83-90` | Eski saatlik sayaç süreç belleğindeydi; restart veya birden çok kopya sayacı bölebilirdi. Ücretsiz planın 100/gün kotası hızla tükenebilir; ücretli planda fazla kullanım $0,90/1.000 e-posta. | E-posta başına 10/gün kalıcı tavan. Doğrulama, yeniden gönderme, doğrulanmamış kullanıcı girişi ve parola sıfırlama aynı e-posta kovasını paylaşır. |
| PostgreSQL/Supabase compute ve disk | `Program.cs:239-273`, `Data/PetWorkDbContext.cs` | Okuma/yazma başına ayrı Supabase ücreti yok; fakat sorgu fırtınası compute kapasitesini, tablo/indeks/WAL büyümesi disk ve yedek miktarını artırır. Pro kotası sonrası disk $0,125/GB-ay. SQL Server geçici hata politikası tek DB işlemini en çok 5 kez yeniden dener (ilk denemeyle en çok 6 yürütme). | SQL yeniden denemesi sınırlı. Supabase Spend Cap açık tutulmalı; sorgu ve yükleme kotası ayrıca izlenmeli. |
| Veritabanında tutulan görseller | `Services/MobileMediaStorageService.cs:5`, `Services/SecureMediaStorageService.cs:45-52` | Mobil görsel 8 MB'a kadar doğrudan DB'ye yazılıyor. 12 yükleme/dk/IP varsayımıyla teorik kalıcı büyüme 96 MB/dk, yaklaşık **135 GB/gün/IP** olabilir; yedek hacmi de büyür. | Boyut/tür doğrulaması var, fakat kullanıcı başına günlük depolama kotası hâlâ önerilen takip işidir. Görseller nesne depolamaya taşınmalıdır. |
| Medya bant genişliği / DB egress | `Controllers/Api/MobileMediaApiController.cs:16-28`, `Program.cs:96-104` | Medya anonim ve 240 istek/dk/IP. Her yanıt 8 MB olursa yaklaşık 1,9 GB/dk ve **2,7 TB/gün/IP** origin çıkışı oluşabilir. Supabase Pro uncached egress aşımı $0,09/GB olduğundan yalnız DB→uygulama bacağı yaklaşık $243/gün/IP; barındırıcının uygulama→istemci trafiği ayrıca ücretlenebilir. `ResponseCache` yalnız istemci/proxy başlıklarını ayarlar, origin için garanti değildir. | Ayrı obje kovası+CDN, imzalı URL, dosya başı cache ve egress alarmı gerekli. Mevcut IP sınırı botnet/dağıtık saldırıya yetmez. |
| Uygulama barındırma/işlemci/ağ | ASP.NET uygulaması `Program.cs`; sağlayıcı manifesti yok | Yoğun sorgular, 5 saniyelik sohbet polling'i ve 30 saniyelik bildirim polling'i instance ölçeklenmesini ve ağ kullanımını artırabilir. Fiyat sağlayıcı bilinmediği için TL/USD tutarı hesaplanamaz. | Host tarafında maksimum instance, CPU/alarm ve outbound bant genişliği kotası kurun. |
| Restic/S3 uyumlu bağımsız yedek | `ops/data-resilience/backup-postgresql.sh`, `docs/data-resilience.md` | Etkinleştirilirse günlük DB dump, saklama ve geri yükleme egress/API çağrıları maliyetlidir. DB içine medya yazılması her yedeği büyütür. Hatalı çok sık timer maliyeti doğrusal artırır. | Timer günlük; 30 günlük/12 haftalık/12 aylık saklama politikası var. Kovanın lifecycle ve bütçesi ayrıca kurulmalı. |
| Harici çeviri endpoint'i (isteğe bağlı) | `Services/ConfigurableTranslationService.cs:15-98` | `ExternalContent:Translation:Endpoint` ücretli bir servis olarak ayarlanırsa metin parçalara bölünür; uzunlukla çağrı sayısı doğrusal artar. Endpoint yokken MyMemory ve resmi olmayan Google Translate çağrıları kullanılıyor; doğrudan Cloud Translation faturası görünmüyor fakat kota/engelleme riski var. | Otomatik içerik üretimini üretimde kapalı tutun; ücretli endpoint eklenirse `AiGeneration` benzeri quota tüketimi zorunlu olsun. |
| Expo Push | `Services/MobilePushNotificationService.cs:22-66` | Expo servisi ücretsizdir; yine de her token için uygulama egress/CPU ve bildirim DB yazısı doğar. Çok token veya olay fan-out'u kaynak tüketir. Expo proje limiti 600 bildirim/sn'dir. | Toplu gönderim boyutu, geçersiz token temizliği ve olay başına fan-out metriği önerilir. |
| StackExchange, USDA, Wikimedia/Wikibooks | `Program.cs:301-324` | Kodda ücretli faturalama göstergesi yok; sağlayıcı kotaları aşılırsa servis kesintisi, uygulamada ise compute/egress ve içe aktarılan veri maliyeti doğar. | Başlangıç içe aktarımı varsayılan kapalı; kaynak başına batch sınırı korunmalı. |

Kod tabanında AI modeli, SMS sağlayıcısı, bulut işlevi veya ödeme sağlayıcısı entegrasyonu bulunmadı. Gelecekteki AI ve SMS çağrıları için günlük kategoriler şimdiden sırasıyla 20 ve 3 olarak tanımlandı; gerçek çağrı eklenirken `DailyCostQuota("AiGeneration")` veya `DailyCostQuota("Sms")` uygulanmalıdır.

## Döngü ve kendini büyütme denetimi

- `Services/ExternalContentAutoPublisher.cs:16-36`: sonsuz `while` yok; açılışta bir kez çalışır. Ancak tüm bekleyen kökleri limitsiz listeleyip her biri için kaynak yenileme ve yayın/çeviri yapar. Restart, deployment veya birden fazla replica aynı işi eşzamanlı başlatabilir. Dağıtık kilit ve `Take(n)` yoktur. Varsayılan `ExternalContent:AutoPublish=false` riski şu anda kapatır.
- `Services/ExternalContentBootstrapService.cs:147-365,433-469`: tek seferlik ve statik liste/hedef sayılarıyla sınırlı; sonsuz değildir. Yine de her restart/replica arama, çeviri ve DB yazı fan-out'u yaratabilir. Üretimde `Program.cs` bu ayarın açılmasını reddeder.
- `Services/WikimediaContentProvider.cs:59-79`: 429 yanıtlarında en fazla dört deneme; sonsuz değil, tek mantıksal çağrıyı dört HTTP çağrısına büyütebilir.
- `Program.cs:269-272`: SQL Server geçici hata yeniden denemesi en fazla beş retry; sonsuz değil.
- `PetWork.Mobile/src/screens/PatiMatchChatScreen.tsx:38-42`: ekran açıkken her 5 saniyede mesaj listesi; **12/dk, 17.280/gün/açık istemci**. Yavaş yanıt 5 saniyeyi aşarsa çağrılar üst üste binebilir.
- `PetWork.Mobile/App.tsx:148-154`: oturum açıkken okunmamış bildirim sayısı her 30 saniye; **2.880/gün/istemci**.
- Mobil access-token yenileme hatası yaklaşık 30 saniye sonra tekrar planlanır; uygulama açık ve sorun kalıcıysa günde 2.880 auth isteğine yaklaşabilir. Timer tekil ve temizleniyor, fakat toplam deneme tavanı yok.
- `MobileMediaBackfillService` tek seferlik ve şu anda DI'a kayıtlı değil; kayıt edilirse upload ağacındaki tüm dosyaları dolaşarak DB büyümesine neden olabilir.
- Kuyruk broker'ı, webhook'un kendisini tetiklemesi veya kodda gerçek bir sonsuz `while(true)` bulunmadı.

Öncelikli takip değişiklikleri: chat için WebSocket/SSE veya görünürlük-aware uzun polling; auto-publisher için dağıtık kilit + batch limiti + çalıştırma başına dış çağrı bütçesi; medya için kullanıcı başına günlük toplam byte kotası.

## Bütçe uyarıları: %50 / %90 / %100

### Google Cloud / Google Maps Platform

1. Google Cloud Console → **Billing → Budgets & alerts → Create budget**.
2. Scope'u yalnız bu projenin billing account/project'ine, mümkünse Google Maps Platform servislerine daraltın.
3. Aylık kabul edilebilir tutarı girin.
4. Üç **Actual spend** eşiği ekleyin: `%50`, `%90`, `%100`. Billing admin/owner alıcılarını doğrulayın; gerekirse Cloud Monitoring notification channel ekleyin.
5. Ayrıca **APIs & Services → Places API (New) → Quotas** altında Search ve Autocomplete için günlük/dakikalık sert kota koyun. Bütçe bildirimi harcamayı otomatik durdurmaz.

CLI eşdeğeri (yer tutucuları doldurun):

```bash
gcloud billing budgets create \
  --billing-account=BILLING_ACCOUNT_ID \
  --display-name=petwork-maps-monthly \
  --budget-amount=AMOUNTUSD \
  --filter-projects=projects/PROJECT_NUMBER \
  --threshold-rule=percent=0.50,basis=current-spend \
  --threshold-rule=percent=0.90,basis=current-spend \
  --threshold-rule=percent=1.00,basis=current-spend
```

### Supabase

Supabase şu anda özel `%50/%90/%100` parasal eşik bildirimleri sunmuyor. Organization → **Billing → Cost Control** içinde **Spend Cap açık** bırakın; **Usage** ve **Upcoming Invoice** ekranlarını haftalık kontrol edin. Spend Cap disk, egress ve storage gibi birçok değişken kalemi keser fakat compute/PITR/custom domain gibi kalemleri kapsamaz. Yüzdeli uyarı şartsa Supabase faturası/usage verisini harici izleme sistemine aktaran ayrı bir alarm gerekir.

### Resend ve yedek sağlayıcısı

Resend dashboard'da plan kullanımını ve günlük gönderimi izleyin; Free plan 100/gün ve 3.000/ay ile zaten sert sınırlıdır, ücretli planda overage açıktır. S3/restic hedefinin kendi sağlayıcısında aynı `%50/%90/%100` bütçeyi storage + request + egress kalemlerini kapsayacak şekilde ayrıca oluşturun; sağlayıcı depodan belirlenemiyor.

## Tavan davranışı kararı

AI, SMS, doğrulama/parola e-postası ve etkileşimli Places araması için **kuyruğa alma değil, 429 ile reddetme** seçildi. Kuyruk, kullanıcı ertesi gün artık istemediği işlemi yine de çalıştırıp gecikmiş maliyet yaratır; doğrulama kodu/SMS ayrıca zaman aşımına uğrar. Yanıt `Retry-After`, `retryAfterSeconds` ve “kota 00:00 UTC'de yenilenir” mesajını içerir. Batch/arka plan içerik üretiminde ise yalnız açık kullanıcı onayıyla ertesi güne erteleme düşünülebilir.

## Dağıtım

Uygulamadan önce seçili sağlayıcının migration'ını kontrollü dağıtım adımında uygulayın:

```powershell
dotnet ef database update --project PetWork/PetWork.csproj --context PetWorkDbContext
dotnet ef database update --project PetWork/PetWork.csproj --context PostgresPetWorkDbContext
```

Yalnız fiilen kullanılan veritabanı context'i için ilgili komutu çalıştırın.
