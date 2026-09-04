# Seren kurulumu ve geri dönüş

## Seren'in kurulumu

1. Büşra tarafından doğrulanıp pushlanan `feature/supabase-integration` branch'ini alır.
2. `.NET 9 SDK` ve proje NuGet paketlerini restore eder.
3. Yeni Supabase projesi oluşturmaz; Büşra'nın ortak `petwork-dev` projesine kendi Supabase hesabıyla davet edilir.
4. Visual Studio **Manage User Secrets** üzerinden kendi `PostgreSqlApp` bağlantısını yerel olarak ekler. Secret Git ile gelmez.
5. `DatabaseProvider=PostgreSql` değerini kendi User Secrets'ına koyar.
6. `dotnet build` ve uygulama health/ana sayfa kontrolünü çalıştırır.
7. İlk veri aktarımını veya `migrate` komutunu tekrar çalıştırmaz.

Ortak DB'de yapılan veri değişiklikleri iki geliştirici tarafından da görülür. Yeni migration'lar ortak DB'ye tek sorumlu kişi/dağıtım adımı tarafından bir kez uygulanır. Lokal bağımsız DB isteyen geliştirici SQL Server değil PostgreSQL kullanmalıdır.

## Geri dönüş

Doğrulama veya ilk geçiş başarısızsa uygulama secret'ındaki `DatabaseProvider` tekrar `SqlServer` yapılır ve doğrulanmış `DefaultConnection`/`SqlServerSource` kullanılır. Eski SQL Server DB ve `.bak` yedeği korunur.

Supabase hedefinde yeni kullanıcı verileri oluşmaya başladıktan sonra yalnızca bağlantıyı SQL Server'a çevirmek yeterli değildir; bu yeni satırlar SQL Server'da bulunmaz. Böyle bir geri dönüşte:

1. Yeni yazmalar kısa süreli durdurulur.
2. PostgreSQL'in yeni yedeği/dump'ı alınır.
3. Geçişten sonra oluşan fark kayıtları belirlenir ve SQL Server'a dönüştürülerek eklenir.
4. Sayım ve digest doğrulaması tekrarlanır.
5. Ancak bundan sonra bağlantı eski sağlayıcıya çevrilir.

Free Plan otomatik indirilebilir yedek sağlamadığı için düzenli `supabase db dump`/`pg_dump` ve Storage yedeği ayrıca planlanmalıdır. DB yedeği Storage nesnelerini içermez.
