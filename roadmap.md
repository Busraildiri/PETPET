# PetWork İçerik Zenginleştirme Roadmap'i

## 1. Amaç

PetWork'teki soru-cevap, tarif, rehber, blog ve hastalık sayfalarını güvenilir açık kaynaklardan alınan içeriklerle zenginleştirmek; yabancı dildeki uygun içerikleri otomatik olarak Türkçeye çevirmek ve her içerikte kaynağı, yazarı, lisansı ve orijinal bağlantıyı görünür biçimde sunmak.

Bu sistemin temel yayın akışı:

```text
Kaynak API
   ↓
Lisans ve kullanım izni kontrolü
   ↓
Ham içeriği alma ve kaynak kaydını oluşturma
   ↓
Temizleme, sınıflandırma ve tekrar kontrolü
   ↓
Türkçeye otomatik çeviri
   ↓
Güvenlik ve kalite kontrolleri
   ↓
Admin inceleme kuyruğu
   ↓
Düzenleme ve onay
   ↓
Kaynak bilgisiyle yayınlama
```

Otomatik alınan sağlık, hastalık ve beslenme içerikleri doğrudan yayımlanmayacaktır. İlk sürümde tüm dış kaynak içerikleri admin onayından geçecektir.

## 2. Temel İlkeler

- Web scraping yerine mümkün olduğunda resmi API kullanılacak.
- Yalnızca yeniden kullanıma izin veren lisanslara veya açık kullanım şartlarına sahip içerikler alınacak.
- API'nin erişilebilir olması, içeriğin yeniden yayımlanabileceği anlamına gelmez; her kaynak ayrı değerlendirilir.
- Türkçe metnin otomatik çeviri olduğu kullanıcıya açıkça gösterilecek.
- Orijinal içerik bağlantısı hiçbir zaman kaldırılmayacak.
- Kaynak yazar, lisans ve değişiklik/çeviri bilgisi içerikle birlikte saklanacak.
- İçerik kaldırılırsa kaynak ve işlem geçmişi denetim kaydında korunacak.
- İnsan kontrolünden geçmemiş veterinerlik ve beslenme bilgileri yayımlanmayacak.
- API anahtarları kod deposuna veya `appsettings.json` içine açık biçimde yazılmayacak.
- Uygulamanın kendi MIT lisansı, dışarıdan alınan içeriğin lisansını değiştirmeyecek. Dış içerik kendi lisansıyla işaretlenecek.

## 3. Kaynak Stratejisi

### 3.1 Soru-cevap içerikleri

İlk tercih: Stack Exchange API ve Pets Stack Exchange.

Alınabilecek alanlar:

- Soru başlığı ve gövdesi
- Etiketler
- Kabul edilmiş veya yüksek puanlı cevaplar
- Yazarın görünen adı ve profil bağlantısı
- Orijinal yayın ve son düzenleme tarihi
- Puan, görüntülenme ve cevap sayısı
- Orijinal soru/cevap bağlantısı
- Kaynak içerik kimliği ve revizyon bilgisi
- Geçerli lisans sürümü

Kurallar:

- Stack Exchange kaynak ve yazar atfı görünür olacak.
- İçeriğin tarihine göre geçerli CC BY-SA lisans sürümü kaydedilecek.
- Çeviri veya özetleme yapıldıysa “Türkçeye çevrildi ve düzenlendi” ifadesi gösterilecek.
- Tam metin yerine kısa özet kullanılacaksa kullanıcı kolayca orijinal içeriğe gidebilecek.
- Cevaplar, PetWork kullanıcılarına aitmiş gibi gösterilmeyecek; “Dış kaynak cevap” etiketi taşıyacak.
- Kaynağın `backoff` ve kota cevapları uygulanacak; istekler önbelleğe alınacak.

Referanslar:

- Stack Exchange API: https://api.stackexchange.com/docs/questions
- API kullanım şartları: https://stackoverflow.com/legal/api-terms-of-use
- İçerik lisansları: https://stackoverflow.com/help/licensing

### 3.2 Tarif ve besin bilgileri

Tariflerin doğrudan farklı sitelerden kopyalanması yerine iki ayrı veri türü kullanılacak:

1. PetWork tarafından hazırlanan, kullanıcı tarafından eklenen veya yeniden kullanım izni açıkça doğrulanan tarifler.
2. Tarif malzemelerini zenginleştiren açık besin verileri.

USDA FoodData Central API şu amaçlarla kullanılabilir:

- Malzeme adı eşleştirme
- Kalori, protein, yağ ve karbonhidrat bilgileri
- Vitamin ve mineral verileri
- Porsiyon bazlı yaklaşık besin tablosu

USDA verileri bir tarifin evcil hayvan için güvenli olduğunu kanıtlamaz. Soğan, sarımsak, çikolata, ksilitol, üzüm/kuru üzüm gibi riskli maddeler için ayrıca güvenlik kontrol listesi uygulanacaktır. Tarif sayfasında porsiyonların hayvan türü, yaş, kilo ve sağlık durumuna göre değişeceği belirtilir.

Referans:

- USDA FoodData Central API: https://fdc.nal.usda.gov/api-guide/

### 3.3 Hastalık, sağlık ve bakım rehberleri

Öncelik sırası:

1. Kamu kurumları ve üniversitelerin açık lisanslı veya açık kullanım şartlarına sahip API/verileri
2. Wikimedia projelerindeki lisansı doğrulanmış metin ve medya
3. Yeniden kullanım için yazılı izin alınmış veterinerlik kaynakları

Kurallar:

- Kaynak sayfası ve güncellenme tarihi saklanacak.
- Belirti, tedavi ve ilaç bilgileri otomatik yayımlanmayacak.
- Acil durum belirtileri admin ekranında özel uyarı ile işaretlenecek.
- Tanı koyan ifadeler kullanılmayacak.
- Her sağlık içeriğinde “Bu içerik bilgilendirme amaçlıdır; veteriner muayenesinin yerini tutmaz” uyarısı bulunacak.
- Kaynak güncellendiğinde içerik yeniden inceleme kuyruğuna alınacak.
- Ticari veteriner siteleri açık izin olmadan kopyalanmayacak veya scrape edilmeyecek.

### 3.4 Görseller

Kullanılabilecek görseller:

- PetWork için üretilmiş özgün görseller
- CC0 veya kamu malı görseller
- Ticari kullanıma ve değişikliğe açık CC lisanslı görseller
- Açıkça yeniden kullanım izni veren kaynak görselleri

Her dış görsel için eser adı, üretici/fotoğrafçı, kaynak URL'si, dosya URL'si, lisans, lisans URL'si ve değişiklik bilgisi saklanacak. `NC` lisanslı görseller ticari kullanım ihtimali varsa; `ND` lisanslı görseller kırpma, filtreleme veya türev üretme ihtimali varsa kullanılmayacak.

## 4. Otomatik Çeviri Akışı

### 4.1 Çeviri servisi

Uygulama belirli bir sağlayıcıya bağımlı kalmayacak. Aşağıdaki gibi bir servis arayüzü oluşturulacak:

```csharp
public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken);
}
```

İlk sağlayıcı seçilirken şu noktalar değerlendirilecek:

- Türkçe çeviri kalitesi
- Kullanım maliyeti ve kota
- Veri saklama/gizlilik şartları
- HTML veya Markdown desteği
- Terim sözlüğü desteği
- Tekrarlanabilir çeviri ve hata yönetimi

### 4.2 Çeviri işlemleri

- Kaynak dili API bilgisinden veya dil algılama servisinden belirlenir.
- Başlık, özet, gövde ve cevaplar ayrı alanlar olarak çevrilir.
- HTML önce güvenli biçimde ayrıştırılır; kod parçaları, URL'ler ve kaynak adları çevrilmez.
- Hayvan türleri, hastalıklar, ilaçlar ve besin maddeleri için PetWork terim sözlüğü uygulanır.
- Kaynak metin ve Türkçe çeviri ayrı saklanır.
- Her çeviri için sağlayıcı, model/sürüm, tarih ve çeviri durumu kaydedilir.
- Aynı kaynak revizyonu tekrar çevrilmez; içerik hash'i ile önbellek ve tekrar kontrolü yapılır.
- Başarısız veya yarım çeviri otomatik olarak yayına gönderilmez.

### 4.3 Kullanıcıya gösterim

İçe aktarılan içeriklerde aşağıdaki kaynak kutusu bulunacak:

```text
Bu içerik dış kaynaktan otomatik olarak Türkçeye çevrilmiş ve
PetWork editörleri tarafından düzenlenmiştir.

Kaynak: Pets Stack Exchange
Yazar: [görünen ad]
Lisans: CC BY-SA 4.0
[Orijinal içeriği gör] [Lisansı gör]
Son kaynak kontrolü: 4 Eylül 2026
```

İsteğe bağlı olarak “Orijinal metni göster” açılır alanı eklenebilir. Tam kaynak metnin yerel olarak gösterilmesi yalnızca lisans izin veriyorsa yapılacaktır; aksi durumda “Orijinal içeriği gör” bağlantısı kaynak siteyi açacaktır.

## 5. Veri Modeli

İçerik tablolarına aynı alanları ayrı ayrı eklemek yerine ortak bir `ExternalContentSource` tablosu önerilir.

### 5.1 ExternalContentSource

```text
Id
ContentType                 Question, Answer, Recipe, Disease, Guide, BlogPost
LocalContentId
Provider                    StackExchange, USDA, Wikimedia vb.
ExternalId
SourceUrl
ApiUrl
SourceTitle
SourceAuthorName
SourceAuthorUrl
SourceLanguage
LicenseCode
LicenseUrl
OriginalPublishedAt
SourceUpdatedAt
ImportedAt
LastCheckedAt
SourceRevision
OriginalContentHash
OriginalText
TranslationProvider
TranslationVersion
TranslatedAt
WasTranslated
WasModified
AttributionText
ReviewStatus                Pending, Approved, Rejected, NeedsReview, Archived
ReviewedByUserId
ReviewedAt
RejectionReason
IsSourceAvailable
```

`Provider + ExternalId + ContentType` alanlarında benzersiz indeks oluşturulacaktır. Böylece aynı içerik tekrar tekrar içe aktarılmaz.

### 5.2 İçerik durumları

```text
Fetched → LicenseChecked → Translated → NeedsReview → Approved → Published
                                      ↘ Rejected
Published → SourceChanged → NeedsReview
Published → SourceRemoved → Archived/ManualReview
```

### 5.3 Denetim kaydı

`ContentImportAudit` tablosu şu olayları tutacak:

- API isteği ve sonucu
- Lisans kontrol sonucu
- Çeviri başlangıcı/sonucu
- Otomatik güvenlik uyarıları
- Admin düzenlemeleri
- Onay, ret, arşivleme ve yeniden yayınlama
- Kaynakta değişiklik veya kaldırılma tespiti

API anahtarı, yetkilendirme başlığı ve kişisel veri loglara yazılmayacaktır.

## 6. Servis Mimarisi

Önerilen servisler:

```text
IExternalContentProvider
├── StackExchangeContentProvider
├── UsdaFoodDataProvider
└── WikimediaContentProvider

IContentLicensePolicy
ITranslationService
IContentSanitizer
IContentClassifier
IContentDeduplicationService
IContentSafetyReviewService
IAttributionService
IExternalContentImportService
IExternalContentSyncService
```

Mevcut `WebScrapingService` yeni entegrasyonların merkezi yapılmayacak. Resmi API istemcileri ayrı provider sınıfları olarak geliştirilecek. Scraping yalnızca kullanım şartlarının izin verdiği, API bulunmayan ve açık lisansın doğrulandığı özel durumlar için değerlendirilecek.

## 7. Admin Paneli

Admin paneline “Dış İçerikler” bölümü eklenecek.

### 7.1 Liste ekranı

Filtreler:

- İçerik türü
- Kaynak sağlayıcı
- Kaynak dili
- Çeviri durumu
- Lisans
- İnceleme durumu
- Sağlık/beslenme risk seviyesi
- İçe aktarılma tarihi
- Kaynakta değişiklik durumu

Toplu işlemler:

- Seçilenleri çevir
- Yeniden çevir
- Onaya gönder
- Onayla
- Reddet
- Kaynağı yeniden kontrol et
- Arşivle

### 7.2 Detay ve düzenleme ekranı

- Orijinal metin ve Türkçe çeviri yan yana gösterilir.
- Başlık, gövde, etiket, kategori ve görsel düzenlenebilir.
- Kaynak, yazar ve lisans alanları görünür olur; zorunlu atıf alanları yanlışlıkla silinemez.
- Otomatik bulunan sağlık/beslenme riskleri uyarı panelinde gösterilir.
- “Kaynak sayfayı aç”, “yeniden çevir”, “kaynağı güncelle” ve “yayın önizleme” işlemleri bulunur.
- Yayınlamadan önce zorunlu kontrol listesi tamamlanır.

### 7.3 Yayın kontrol listesi

- Kaynak yeniden kullanıma izin veriyor mu?
- Yazar ve orijinal bağlantı doğru mu?
- Lisans ve lisans sürümü doğru mu?
- Çeviri anlamı koruyor mu?
- Çeviri/değişiklik bilgisi yazıyor mu?
- Sağlık veya beslenme iddiaları güvenli mi?
- Kişisel bilgi veya gereksiz profil verisi var mı?
- Görselin lisansı ayrıca doğrulandı mı?
- İçerik başka bir kayıtla tekrar ediyor mu?

## 8. Kullanıcı Arayüzü

- Kartlarda “Dış kaynak” veya “Çevrilmiş içerik” rozeti gösterilecek.
- Detay sayfasında görünür bir kaynak/atıf kutusu bulunacak.
- “Orijinal içeriği gör” bağlantısı yeni sekmede ve güvenli `rel` değerleriyle açılacak.
- İçerik otomatik çeviriyse yayınlayan kişi yerine kaynak yazar gösterilecek; admin yalnızca “PetWork editörü” olarak belirtilir.
- Soru-cevap sayfasında dış cevaplar ile PetWork topluluğunun cevapları görsel olarak ayrılacak.
- Kaynak artık erişilemiyorsa kullanıcıya eski bağlantı sunmak yerine içerik incelemeye alınacak.
- Sağlık ve tarif sayfalarında uygun güvenlik uyarıları sabit ve kolay görülebilir olacak.

## 9. Güvenlik, Gizlilik ve Kalite

- API cevaplarındaki HTML allowlist tabanlı sanitizer ile temizlenecek.
- Script, iframe, event handler, izinsiz embed ve izleme parametreleri kaldırılacak.
- Dış görseller mümkünse hotlink edilmeyecek; yeniden barındırma hakkı yoksa yalnızca kaynak bağlantısı kullanılacak.
- E-posta adresi gibi gereksiz kişisel bilgiler alınmayacak.
- Yazar atfı için gereken görünen ad ve profil bağlantısından fazlası saklanmayacak.
- Çeviri veya içerik sağlayıcısına kullanıcıların özel verileri gönderilmeyecek.
- İsteklere timeout, retry, circuit breaker, rate limit ve cache uygulanacak.
- Kaynak API çalışmadığında mevcut onaylanmış içerikler gösterilmeye devam edecek.
- Zehirli gıda, ilaç dozu, acil belirti ve tedavi tavsiyesi içeren metinler yüksek riskli olarak işaretlenecek.
- Otomatik kalite puanı düşük içerikler admin kuyruğunda önceliklendirilecek.

## 10. Zamanlanmış Senkronizasyon

İlk sürümde içe aktarma admin tarafından manuel başlatılacaktır. Sistem kararlı hale geldikten sonra arka plan görevi eklenir.

Önerilen sıklıklar:

- Yeni soru taraması: 6-12 saatte bir
- Yayındaki dış içeriklerin değişiklik kontrolü: haftada bir
- Kaynak URL erişilebilirlik kontrolü: ayda bir
- Lisans/kaynak politikası kontrolü: üç ayda bir ve sağlayıcı değişiklik duyurularında

Senkronizasyon davranışı:

- Yeni içerik: inceleme kuyruğu oluştur.
- Kaynak değişmiş: mevcut yayını koru, yeni revizyonu incelemeye al.
- Kaynak silinmiş: otomatik silme yapma, admin uyarısı oluştur.
- Lisans değişmiş veya belirsiz: içeriği yayından kaldırıp incelemeye al.
- API kotası dolmuş: sağlayıcının belirttiği süre kadar bekle.

## 11. Uygulama Fazları

### Faz 0 — Hukuki ve ürün kuralları

- [ ] İzin verilen lisansların listesi oluşturulsun.
- [ ] Yasaklı/belirsiz kaynak politikası tanımlansın.
- [ ] Atıf bileşeninin zorunlu alanları belirlensin.
- [ ] Otomatik çeviri ve veteriner uyarısı metinleri onaylansın.
- [ ] Kaynak kaldırma/telif bildirimi süreci yazılsın.
- [ ] Gizlilik politikasına dış API ve çeviri sağlayıcısı açıklaması eklensin.

Başarı ölçütü: Kaynağı, lisansı veya yazarı belirsiz hiçbir kayıt içe aktarılamıyor.

### Faz 1 — Kaynak ve inceleme altyapısı

- [ ] `ExternalContentSource` ve `ContentImportAudit` modelleri oluşturulsun.
- [ ] Entity Framework migration eklensin.
- [ ] İçerik durum makinesi ve benzersiz indeksler eklensin.
- [ ] Admin “Dış İçerikler” listesi ve detay ekranı oluşturulsun.
- [ ] Ortak kaynak/atıf UI bileşeni oluşturulsun.
- [ ] Yayın öncesi kontrol listesi zorunlu hale getirilsin.

Başarı ölçütü: Admin örnek bir dış içeriği kaynak bilgileriyle inceleyip yayımlayabiliyor.

### Faz 2 — Pets Stack Exchange MVP

- [ ] Stack Exchange API istemcisi oluşturulsun.
- [ ] `site=pets` için soru, cevap, yazar ve etiket eşlemesi yapılsın.
- [ ] Kota, `backoff`, hata ve cache yönetimi eklensin.
- [ ] Kaynak revizyonu ve lisans sürümü kaydedilsin.
- [ ] Kategori/etiket eşleme kuralları oluşturulsun.
- [ ] Tekrarlanan içerik kontrolü eklensin.
- [ ] Admin tarafından konu/etiket bazlı manuel içe aktarma yapılabilsin.

Başarı ölçütü: Bir Pets Stack Exchange sorusu ve uygun cevabı, doğru atıfla taslak olarak içe aktarılabiliyor.

### Faz 3 — Otomatik Türkçe çeviri

- [ ] `ITranslationService` ve ilk sağlayıcı implementasyonu eklensin.
- [ ] Terim sözlüğü oluşturulsun.
- [ ] HTML/Markdown korumalı çeviri geliştirilsin.
- [ ] Kaynak metin ile çeviri karşılaştırma ekranı eklensin.
- [ ] Çeviri önbelleği ve maliyet/kota takibi eklensin.
- [ ] Yeniden çeviri ve manuel düzeltme akışı eklensin.
- [ ] Kullanıcı sayfasına otomatik çeviri etiketi eklensin.

Başarı ölçütü: İçerik Türkçe taslağa çevriliyor, admin tarafından düzenleniyor ve orijinal metin/bağlantı korunuyor.

### Faz 4 — Tarif ve besin zenginleştirme

- [ ] USDA FoodData Central API istemcisi oluşturulsun.
- [ ] Malzeme eşleştirme ve birim dönüştürme geliştirilsin.
- [ ] Yaklaşık besin değerleri hesaplanıp kaynakla gösterilsin.
- [ ] Evcil hayvanlar için riskli malzeme sözlüğü oluşturulsun.
- [ ] Tarifler için yüksek riskli içerik admin uyarısı eklensin.
- [ ] Belirsiz eşleşmelerin manuel seçilmesi sağlansın.

Başarı ölçütü: Admin bir tarifin malzemelerini USDA verileriyle eşleştirip kontrollü bir besin özeti yayımlayabiliyor.

### Faz 5 — Rehber ve sağlık kaynakları

- [ ] Kaynak beyaz listesi oluşturulsun.
- [ ] Wikimedia/API provider'ı lisans kontrolüyle eklensin.
- [ ] Kaynak tarihi ve güncellik kontrolü geliştirilsin.
- [ ] Belirti/ilaç/tedavi ifadeleri için risk sınıflandırması eklensin.
- [ ] Sağlık içeriklerine zorunlu veteriner uyarısı eklensin.
- [ ] Kaynak değişikliğinde yeniden inceleme akışı uygulanmış olsun.

Başarı ölçütü: Yalnızca doğrulanmış kaynaklardan gelen sağlık içeriği insan onayıyla yayımlanabiliyor.

### Faz 6 — Otomasyon ve gözlemleme

- [ ] Arka plan senkronizasyon görevi oluşturulsun.
- [ ] Sağlayıcı bazlı zamanlama ve kota yönetimi eklensin.
- [ ] Başarısız işlemler için tekrar deneme ve dead-letter kuyruğu eklensin.
- [ ] Admin paneline kota, hata, maliyet ve yayın istatistikleri eklensin.
- [ ] Kaynak silinmesi ve lisans belirsizliği alarmları eklensin.
- [ ] Otomatik yayın yalnızca düşük riskli ve açıkça izin verilen kategoriler için ayrıca değerlendirilsin.

Başarı ölçütü: Sistem kontrollü şekilde yeni içerik buluyor, çeviriyor ve admin kuyruğuna bırakıyor; hatalar izlenebiliyor.

## 12. Test Planı

### Birim testleri

- Lisans izin/ret kuralları
- İçerik ve kaynak eşlemesi
- Aynı dış içeriğin tekrar alınmaması
- Atıf metninin doğru üretilmesi
- HTML temizleme
- Çeviri cache/hash davranışı
- Kaynak revizyon değişikliği tespiti
- Riskli tarif malzemesi tespiti

### Entegrasyon testleri

- Sağlayıcı API başarı, timeout, kota ve `backoff` cevapları
- Çeviri sağlayıcısı hata ve kısmi cevapları
- Admin onay/ret/yayın akışı
- Kaynak değiştiğinde yeniden inceleme
- Silinmiş kaynak ve bozuk bağlantı davranışı

### Kullanıcı arayüzü testleri

- Kaynak, yazar ve lisans bilgisi mobil/masaüstünde görünür mü?
- “Orijinal içeriği gör” bağlantısı doğru hedefe gidiyor mu?
- Otomatik çeviri etiketi anlaşılır mı?
- Dış cevaplar kullanıcı cevaplarından ayrılıyor mu?
- Veteriner uyarısı kolay fark ediliyor mu?

## 13. Yayın Kriterleri

İlk canlı sürüm aşağıdakiler tamamlanmadan açılmayacaktır:

- Kaynak ve lisans verisi zorunlu ve doğrulanmış.
- Dış içerikler varsayılan olarak `Pending` durumda.
- Admin onayı olmadan sağlık, hastalık ve tarif içeriği yayımlanamıyor.
- Otomatik çeviri açıkça etiketleniyor.
- Orijinal içerik bağlantısı tüm dış içeriklerde çalışıyor.
- HTML temizleme ve tekrar kontrolü test edilmiş.
- API anahtarları güvenli yapılandırmada.
- Kaynak kaldırma ve telif bildirimi iletişim süreci hazır.
- Yedekleme ve geri alma senaryosu test edilmiş.

## 14. Başarı Metrikleri

- Admin onayına gelen uygun içerik oranı
- Çeviri sonrası gereken ortalama manuel düzenleme miktarı
- Kaynak veya lisans bilgisi eksik kayıt sayısı: hedef `0`
- Tekrarlanan içerik oranı
- Çeviri ve API maliyeti / yayımlanan içerik
- Kaynak bağlantılarının çalışırlık oranı
- Sağlık/beslenme nedeniyle reddedilen içerik oranı
- İçerik sayfasından orijinal kaynağa geçiş oranı
- Dış içeriklerde kullanıcı raporu ve düzeltme talebi sayısı

## 15. İlk Sprint Önerisi

İlk sprint yalnızca altyapı ve Pets Stack Exchange prototipine odaklanmalıdır:

1. `ExternalContentSource` veri modeli ve migration
2. Admin dış içerik inceleme ekranı
3. Stack Exchange API istemcisi
4. Tek bir soruyu cevaplarıyla taslak olarak içe aktarma
5. Kaynak/atıf kutusu
6. Geçici veya seçilen çeviri servisiyle Türkçe taslak üretme
7. Admin düzenleme, onaylama ve yayınlama
8. Birim ve entegrasyon testleri

Bu sprint sonunda sistem otomatik toplu yayın yapmayacaktır. Amaç, uçtan uca güvenli bir örnek akışın çalıştığını doğrulamaktır.

## 16. Hukuki Not

Bu belge teknik ve ürün planıdır; hukuki görüş değildir. Her sağlayıcının güncel API koşulları ve içerik lisansı entegrasyon sırasında ve canlıya çıkmadan önce yeniden kontrol edilmelidir. Proje ticari olarak kullanılacaksa özellikle CC BY-SA türev eser yükümlülükleri, görsel lisansları, kullanıcı profili verileri, KVKK/GDPR ve telif kaldırma süreci için uzman görüşü alınmalıdır.
