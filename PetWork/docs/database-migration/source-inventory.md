# PetWork SQL Server kaynak envanteri

Envanter tarihi: 2026-09-04. Bu belge Büşra'nın bilgisayarındaki güncel kaynak üzerinden üretilmiştir; Seren'in yerel veritabanı kaynak kabul edilmemiştir.

## Doğrulanan kaynak

- Uygulama: ASP.NET Core / .NET 9 (`net9.0`)
- EF Core paketleri: 9.0.4
- Sağlayıcı: `Microsoft.EntityFrameworkCore.SqlServer` 9.0.4
- DbContext: `PetWork.Data.PetWorkDbContext`
- Yapılandırılmış sunucu: `(localdb)\\MSSQLLocalDB`
- Yapılandırılmış ve çevrimiçi DB: `PetWorkDB`
- SQL Server: 15.0.4382.1, compatibility level 150, SIMPLE recovery
- Fiziksel dosyalar: `C:\\Users\\busra\\PetWorkDB.mdf` ve `C:\\Users\\busra\\PetWorkDB_log.ldf`
- Ortam veya launch profile üzerinden bağlantı dizesi override'ı bulunmadı.
- Uygulama modeli ile son SQL Server migration'ı arasında bekleyen model değişikliği yok.

## Tablolar ve kaynak kayıt sayıları

| Tablo | Kayıt |
|---|---:|
| `Answers` | 2 |
| `Badges` | 4 |
| `BadgeUser` | 3 |
| `BlogPosts` | 0 |
| `Diseases` | 3 |
| `Guides` | 3 |
| `Pets` | 3 |
| `Questions` | 3 |
| `Recipes` | 3 |
| `Users` | 5 |
| `__EFMigrationsHistory` | 8 |

Kaynakta 11 kullanıcı tablosu vardır. Kullanıcı tanımlı view, trigger, stored procedure, function veya sequence yoktur.

## Migration geçmişi

1. `20250506195859_InitialCreate`
2. `20250515205907_ExperienceAndBadgeSystem`
3. `20250515210308_SecurityAndAdminFeatures`
4. `20250515210636_idk`
5. `20250515213516_kel`
6. `20250518202623_kizilcik`
7. `20250518203859_serbet`
8. `20260904190000_NormalizeLegacyTurkishContent`

Bu SQL Server migration geçmişi referans ve geri dönüş için korunacaktır; PostgreSQL'deki `__EFMigrationsHistory` tablosuna uygulanmış gibi kopyalanmayacaktır.

## Seren'in kopyasıyla bildirilen fark

Bu kaynakta `Comments`, `Likes`, `Notifications`, `Favorites`, `PetCategories` ve `PetHealthRecords` tabloları yoktur. `Pets` tablosunda hem nullable `Age` hem nullable `DateOfBirth`, ayrıca `Type`, `PetType`, `ProfileImage` ve nullable `Image` sütunları vardır. Dolayısıyla aktarım şeması Seren'in DB envanterinden değil, yukarıdaki gerçek kaynaktan üretilecektir.

## İlişkiler

- `Answers.QuestionId -> Questions.Id` (`CASCADE`)
- `Answers.UserId -> Users.Id` (`NO ACTION`)
- `BadgeUser.BadgesId -> Badges.Id` (`CASCADE`)
- `BadgeUser.UsersId -> Users.Id` (`CASCADE`)
- `BlogPosts.UserId -> Users.Id` (`CASCADE`)
- `Guides.UserId -> Users.Id` (`CASCADE`)
- `Pets.UserId -> Users.Id` (`CASCADE`)
- `Questions.UserId -> Users.Id` (`NO ACTION`)
- `Recipes.UserId -> Users.Id` (`NO ACTION`)

Primary key ve foreign key indeksleri kaynak katalogdan envanterlendi. `Users.Username` ve `Users.Email` üzerinde benzersiz indeks bulunmaması ayrıca ele alınmalıdır; mevcut veride olası büyük/küçük harf çakışmaları kontrol edilmeden yeni constraint eklenmeyecektir.

## Tarih/saat gözlemi

Kaynak tarih alanlarının tamamı SQL Server `datetime2` türündedir ve saat dilimi taşımaz. Kod yeni olay tarihlerini `DateTime.Now` ile üretiyor. Kaynak değerler doğrulanmadan UTC'ye çevrilmeyecek; ilk PostgreSQL aktarımı değerleri saat kayması olmadan koruyacaktır. `Pets.DateOfBirth` olay zamanı değil doğum tarihidir ve ayrı değerlendirilecektir.

## Görsel yolları

DB'de kayıtlı 15 farklı dosya yolundan yalnızca şu üçü fiziksel olarak mevcut:

- `img/user1.jpg`
- `img/user2.jpg`
- `img/user-profile.jpg`

Şu 12 yol kaynakta eksik; yer tutucu eklenerek taşınmış sayılmayacak:

- `img/badges/active.png`
- `img/badges/deneyimli-üye.png`
- `img/badges/experienced.png`
- `img/badges/yeni-başlayan.png`
- `img/diseases/disease1.jpg`
- `img/diseases/disease2.jpg`
- `img/diseases/disease3.jpg`
- `img/pet-default.jpg`
- `img/recipes/recipe1.jpg`
- `img/recipes/recipe2.jpg`
- `img/recipes/recipe3.jpg`
- `img/user3.jpg`

Mevcut `wwwroot/img` içeriği ayrı bir ZIP yedeğine alınmıştır. DB yedeği bu dosyaları içermez.

## Kod ve güvenlik bulguları

- `Program.cs` uygulama açılışında koşulsuz `Database.Migrate()` çalıştırıyor. Ortak PostgreSQL DB için bu davranış kaldırılmalı veya varsayılan kapalı, kontrollü bir dağıtım adımına dönüştürülmelidir.
- Development ortamında `EnableSensitiveDataLogging()` açık; aktarım ve ortak bağlantı testlerinde hassas değerlerin loglara düşmemesi için varsayılan olarak kapatılmalıdır.
- API controller'larında yetkilendirme niteliği olmayan `POST` ve `DELETE` uçları bulunuyor. Bunlar Supabase bağlantısından bağımsız mevcut güvenlik açığıdır ve mobil API hazırlığında düzeltilmelidir.
- Mevcut üyelik sistemi .NET `PasswordHasher<User>` kullanıyor. `PasswordHash` değerleri olduğu gibi taşınacak; düz metne çevrilmeyecek veya yeniden hash'lenmeyecektir.
- Supabase bu aşamada yalnızca PostgreSQL/ileride Storage hedefidir; Supabase Auth geçişi bu çalışmanın kapsamına dahil değildir.
