# PetWork veri dayanıklılığı

## Denetim özeti (2026-09-10)

| Konu | Depodan doğrulanabilen durum |
|---|---|
| Veritabanı | Uygulama SQL Server ve PostgreSQL destekliyor. Belgelenmiş ortak hedef Supabase PostgreSQL `petwork-dev`; çalışma anındaki gerçek sağlayıcı secret ile seçildiği için depodan kesinleştirilemiyor. |
| Otomatik DB yedeği | Önceden zamanlayıcı veya yedek betiği yoktu. `ops/data-resilience` altında günlük PostgreSQL + medya yedeği eklendi; sunucuda timer kurulup doğrulanana kadar **aktif sayılmaz**. |
| Saklama | Yeni politika: 30 günlük, 12 haftalık ve 12 aylık geri dönüş noktası. Bu değerler `/etc/petwork/backup.env` ile değiştirilebilir. |
| Bölge/hesap ayrımı | Depoda gerçek Supabase bölgesini veya yedek hesabını doğrulayacak kimlik yok. Kurulum belgesi Frankfurt'u öneriyor. `RESTIC_REPOSITORY` farklı sağlayıcı/hesap ve farklı bölgeye ayarlanmalıdır. |
| Yüklenen dosyalar | Yeni yüklemeler `MediaStorage:StoragePath` altındaki dosya sisteminde. DB yedeği bunları kapsamaz. Yeni betik `PETWORK_MEDIA_PATH` verildiğinde aynı şifreli restic deposuna ayrıca yedekler. Eski `MobileMediaAssets.Data` satırları varsa PostgreSQL dump'ına dahildir. |
| Geri yükleme tatbikatı | Depoda daha önce başarıyla yapılmış bir restore kaydı, tarih, süre veya doğrulama sonucu yok. İlk başarılı tatbikatın sonucu bu belgeye veya olay/tatbikat kaydına eklenmelidir. |

Supabase'in platform yedekleri Storage nesnelerini içermez. Ücretli planlardaki günlük yedek ve PITR, bağımsız hesap/bölgedeki uygulama yedeğinin yerine tek başına geçmemelidir:

- <https://supabase.com/docs/guides/platform/backups>
- <https://supabase.com/docs/guides/platform/regions>

## Kurulan yedekleme katmanı

`ops/data-resilience/backup-postgresql.sh` şunları yapar:

1. PostgreSQL'i custom-format `pg_dump` ile dışa aktarır.
2. Dump'ı `pg_restore --list` ile yapısal olarak doğrular ve SHA-256 özeti üretir.
3. DB dump'ını şifreli restic deposuna yazar.
4. `PETWORK_MEDIA_PATH` verilmişse medya dizinini ayrı snapshot olarak yazar.
5. 30 günlük, 12 haftalık ve 12 aylık snapshot bırakır.
6. `restic check` ile depo bütünlüğünü kontrol eder.

### Sunucuda etkinleştirme

Gereksinimler: sunucuyla aynı veya daha yeni ana sürümde PostgreSQL istemcisi (`pg_dump`, `pg_restore`), `restic` ve yedek hedefine ağ erişimi.

```bash
sudo useradd --system --home /var/lib/petwork-backup --create-home petwork-backup
sudo install -d -m 0700 -o petwork-backup -g petwork-backup /etc/petwork
sudo install -m 0600 -o petwork-backup -g petwork-backup \
  ops/data-resilience/backup.env.example /etc/petwork/backup.env
sudo install -m 0600 -o petwork-backup -g petwork-backup /dev/null /etc/petwork/restic-password
```

`/etc/petwork/backup.env` içindeki yer tutucuları doldurun; özel karakter içeren değerleri tek tırnakla çevreleyin. Bağlantıda yalnız okuma yetkisi dump için yeterli olmayabilir; backup rolüne bütün uygulama şemasını ve sequence'leri okuyacak en az ayrıcalıkları verin. Medya dizinine `petwork-backup` kullanıcısının salt okunur erişimi olmalıdır.

İlk kez kullanılan restic deposunu bir kez başlatın:

```bash
sudo -u petwork-backup bash -c 'set -a; source /etc/petwork/backup.env; set +a; restic init'
```

Servis dosyasındaki `/opt/petwork` ve `/mnt/petim-media` yollarını gerçek dağıtım yollarıyla eşleştirdikten sonra:

```bash
sudo install -m 0644 ops/data-resilience/systemd/petwork-backup.service /etc/systemd/system/
sudo install -m 0644 ops/data-resilience/systemd/petwork-backup.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now petwork-backup.timer
sudo systemctl start petwork-backup.service
sudo systemctl status petwork-backup.service
sudo systemctl list-timers petwork-backup.timer
```

Başarısız servis için izleme/uyarı eklenmeden kurulum tamamlanmış sayılmaz. İlk çalıştırmadan sonra hem `petwork-db` hem `petwork-media` snapshot'ını kontrol edin:

```bash
sudo -u petwork-backup bash -c 'set -a; source /etc/petwork/backup.env; set +a; restic snapshots --tag petwork-db'
sudo -u petwork-backup bash -c 'set -a; source /etc/petwork/backup.env; set +a; restic snapshots --tag petwork-media'
```

## Üretime dokunmadan geri yükleme testi

En az ayda bir ve her yedekleme değişikliğinden sonra uygulanmalıdır.

1. Üretim projesinden ayrı bir PostgreSQL sunucusunda boş bir veritabanı oluşturun. Adı mutlaka `_restore_test` ile bitsin; örneğin `petwork_202609_restore_test`.
2. Üretim bağlantı dizesini kullanmayın. Ayrı test sunucusunun bağlantısını `PETWORK_RESTORE_DATABASE_URL` yapın.
3. Restic deposuna salt okunur erişebilen geçici kimlik bilgileri kullanın.
4. Medya testi için boş ve geçici, adında `restore-drill` geçen bir dizin seçin.
5. Aşağıdaki değişkenleri ayrı bir terminalde ayarlayın:

```bash
export RESTIC_REPOSITORY='s3:s3.other-provider.example/petwork-backups'
export RESTIC_PASSWORD_FILE='/etc/petwork/restic-password'
export PETWORK_RESTORE_DATABASE_URL='postgresql://restore_user:REDACTED@test-db.example/petwork_202609_restore_test?sslmode=require'
export PETWORK_RESTORE_CONFIRM='NON_PRODUCTION'
export PETWORK_RESTORE_MEDIA_DIR='/var/tmp/petwork-restore-drill-media'
```

6. `bash ops/data-resilience/restore-drill-postgresql.sh` çalıştırın. Betik hedef DB adının `_restore_test` ile bittiğini ve hiç kullanıcı tablosu içermediğini doğrulamadan yazmaya başlamaz.
7. Betiğin tablo sayısı kontrolüne ek olarak uygulamaya özgü sayımları karşılaştırın:

```sql
select count(*) from petwork."Users";
select count(*) from petwork."Pets";
select count(*) from petwork."Questions";
select count(*) from petwork."Recipes";
select count(*) from petwork."MobileMediaAssets";
```

8. Test uygulamasını yalnız restore DB'ye bağlayıp giriş, profil, evcil hayvan, sosyal gönderi ve medya görüntüleme smoke testlerini yapın.
9. En yeni medya dosyalarından rastgele örnekler açın; DB'deki URL/kayıtlarla eşleştiğini kontrol edin.
10. Tatbikat tarihini, kullanılan snapshot kimliğini, RPO'yu, geri yükleme süresini ve sonucu kaydedin. Ardından yalnız test kaynaklarını silin.

## Yanlışlıkla silmeye karşı koruma

Timer etkinleştirilmeden önce doğrulanmış geri dönüş penceresi **yoktur**. Supabase planı/paneli görülmeden platform yedeğine güvenilemez. Kodda sosyal gönderi ve kayıp hayvan ilanı için `IsDeleted` kullanılıyor; ancak medya dosyası aynı işlemde fiziksel olarak siliniyor. Kullanıcı, evcil hayvan, tarif, soru, blog ve hastalık gibi pek çok kayıt doğrudan fiziksel siliniyor.

Timer ve bağımsız restic deposu doğrulandıktan sonra hedef koruma:

- En kötü normal veri kaybı: günlük çalıştığı için yaklaşık 24 saat (RPO).
- Yakın dönem: son 30 günün günlük noktaları.
- Orta dönem: 12 haftalık nokta.
- Uzun dönem: 12 aylık nokta.
- Proje/Supabase hesabının tamamen silinmesine karşı koruma: yalnız `RESTIC_REPOSITORY` gerçekten ayrı hesap/sağlayıcıdaysa vardır.
- Fidye yazılımı veya ele geçirilmiş backup kimliğine karşı koruma: hedef kovada object lock/versioning ve ayrı, silme yetkisi olmayan yazma kimliği ayrıca etkinleştirilmelidir. Restic saklama politikası tek başına değiştirilemezlik sağlamaz.

## Yıkıcı işlemler

### Uygulama çalışma zamanında toplu veya zincirleme silme

- `Controllers/AdminController.cs:237`: genel yönetici içerik silme yardımcısı tek kaydı fiziksel siler.
- `Controllers/AdminController.cs:265-268`: kullanıcı silerken cevap, tarif ve soruları toplu; ardından kullanıcıyı fiziksel siler.
- `Controllers/Api/MobileAuthApiController.cs:349-355`: hesap silmede rapor, yorum, cevap, soru ve tarifleri `ExecuteDeleteAsync` ile toplu; kullanıcıyı fiziksel siler.
- `Controllers/Api/MobilePetsApiController.cs:125-128`: eşleşme mesajları ve kararlarını toplu; profil ve evcil hayvanı fiziksel siler.
- `Controllers/Api/MobileNotificationsApiController.cs:119-121`: eşleşen push token'larını toplu siler; kapsam kullanıcı+token ile sınırlıdır.

### Diğer fiziksel kayıt silmeleri

- `Controllers/Api/BlogsApiController.cs:91-101`
- `Controllers/Api/DiseasesApiController.cs:190-200`
- `Controllers/Api/QuestionsApiController.cs:126-136`
- `Controllers/Api/RecipesApiController.cs:116-126`
- `Services/ExternalContentBootstrapService.cs:181,306,311,316`

### Fiziksel dosya silmeleri

- `Services/SecureMediaStorageService.cs:82-92`
- `Controllers/Api/MobileAdoptionApiController.cs:259-261`
- `Controllers/Api/MobileAuthApiController.cs:448-458`
- `Controllers/Api/MobileLostPetsApiController.cs:268-275`
- `Controllers/Api/MobileProductReviewsApiController.cs:92-98`
- `Controllers/Api/MobileSocialApiController.cs:478-486`

### Soft-delete yapılan ama dosyası fiziksel silinen yerler

- `Controllers/Api/MobileLostPetsApiController.cs:214-225`
- `Controllers/Api/MobileSocialApiController.cs:350-378`

### Migration kaynaklı yıkıcı işlemler

- İleri migration sırasında kolon silen uyumluluk/onarım kodu:
  - `Migrations/PostgreSql/20260907083118_MobileAuthLifecycle.cs:41-42`
  - `Migrations/PostgreSql/20260907173252_RepairMobileAuthSessionSchema.cs:57-58`
- Diğer `DropTable`, `DropColumn` ve `DROP INDEX` kullanımlarının tamamı migration `Down` metotlarında, yani rollback çalıştırıldığında devreye giriyor:
  - SQL Server: `20250506195859_InitialCreate`, `20250515205907_ExperienceAndBadgeSystem`, `20250515210308_SecurityAndAdminFeatures`, `20250515210636_idk`, `20250515213516_kel`.
  - PostgreSQL: `20260904151614_InitialPostgreSql`, `20260904223836_ExternalContentReview`, `20260906145801_AddSocialPosts`, `20260906154251_AddSocialComments`, `20260906160424_AddSocialPostReports`, `20260907083118_MobileAuthLifecycle`, `20260907140622_CompleteMobileAuthProduction`, `20260907162150_AddPatiMatchMessagingPostgres`, `20260908135338_AddSocialReactionsPostgres`, `20260908142222_AddLostPetsMobile`, `20260909111021_AddAdoptionAndProductReviews`, `20260909151908_AddMobileNotifications`, `20260909191054_AddNotificationPreferencesAndPushTokens`, `20260909212928_StoreMobileMediaInDatabase`.

Kod tabanında ham `DROP DATABASE`, `DROP SCHEMA`, `TRUNCATE TABLE` veya ham `DELETE FROM` bulunmadı.
