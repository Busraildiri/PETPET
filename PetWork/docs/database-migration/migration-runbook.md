# Kontrollü PetWork migration iş akışı

Bu adımlar kaynak yedek doğrulandıktan ve Supabase hedef bağlantısı User Secrets'a eklendikten sonra uygulanır. Komutlarda parola veya tam bağlantı dizesi bulunmaz.

## 1. Kaynak ve hedef ön kontrolü

```powershell
dotnet run --project tools/PetWork.DatabaseMigration/PetWork.DatabaseMigration.csproj -- preflight
```

Araç kaynak DB adını, hedef DB adını, `petwork` şemasını ve her tablo için kaynak/hedef sayısını bildirir. Hedefte uygulama tablosu veya veri varsa silme/üzerine yazma yapılmaz.

## 2. PostgreSQL şemasını bir kez oluştur

```powershell
dotnet ef database update --context PostgresPetWorkDbContext
```

Bu komut PostgreSQL'e özel `Migrations/PostgreSql` setini kullanır. SQL Server migration geçmişi kopyalanmaz. Uygulama açılışında `Database.Migrate()` varsayılan olarak kapalıdır.

İnceleme/otomasyon için idempotent SQL çıktısı: `database/postgresql/petwork-schema.sql`.

## 3. Deneme veri aktarımı

```powershell
dotnet run --project tools/PetWork.DatabaseMigration/PetWork.DatabaseMigration.csproj -- migrate
```

Araç hedef tabloların boş olmasını zorunlu tutar; veri görürse durur. Kopyalama tek PostgreSQL transaction'ı içinde yapılır. Hata olursa tüm kopya geri alınır. ID'ler aynen yazılır ve identity sequence'leri yeni kayıtların çakışmayacağı şekilde ayarlanır.

## 4. Veri doğrulama

```powershell
dotnet run --project tools/PetWork.DatabaseMigration/PetWork.DatabaseMigration.csproj -- verify
```

Her tablo için kayıt sayısı ve deterministik SHA-256 özeti karşılaştırılır. NULL/boş metin, Türkçe metin, bool, tarih ve sayılar türlerine göre normalize edilir. `PasswordHash` karşılaştırmaya dahil edilir ancak değeri yazdırılmaz. Hedef foreign key yetimleri ayrıca sayılır.

## 5. Uygulamayı hedefe geçir

Yalnızca doğrulama başarılı olduktan sonra User Secrets içinde:

```json
{
  "DatabaseProvider": "PostgreSql"
}
```

ayarlanır. Uygulama `PostgreSqlApp` bağlantısını kullanır. İlk canlı yazma öncesinde giriş, kayıt, profil, hayvan, soru/cevap, tarif, blog ve admin akışları test edilir.

## Yeniden çalıştırma davranışı

- `migrate`, hedefte herhangi bir uygulama verisi görürse temizleme yapmadan durur.
- Başarısız transaction hedefte kısmi satır bırakmaz.
- Başarılı aktarım ikinci kez çalıştırılmaz; yeniden deneme gerekiyorsa hedefin nasıl sıfırlanacağı ayrıca Büşra ile kararlaştırılır.
- `verify` tekrar çalıştırılabilir ve veri değiştirmez.
