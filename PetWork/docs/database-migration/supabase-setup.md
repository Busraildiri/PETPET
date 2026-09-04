# PetWork Supabase hedef kurulumu

## Proje

- Tek ortak hedef proje oluşturulur; Büşra ve Seren ayrı PetWork DB'leri oluşturmaz.
- Geliştirme hedefi için önerilen ad: `petwork-dev`.
- Önerilen bölge: `Central EU (Frankfurt)`; Türkiye'ye yakınlık nedeniyle başlangıç tercihi budur.
- Güçlü database parolası parola yöneticisinde tutulur; sohbete, Git'e veya komut satırı argümanına yazılmaz.
- Seren hesap parolası paylaşımıyla değil, Supabase organization/team davetiyle eklenir.

2026-09-04 tarihinde doğrulanan Free Plan sınırları: proje başına 500 MB DB, 1 GB Storage, 5 GB egress, 50.000 MAU ve iki aktif proje. Kaynak PetWork DB ve mevcut görseller bu başlangıç sınırlarının çok altındadır. Free projeler düşük etkinlikte yaklaşık yedi gün sonra duraklatılabilir; Free Plan'da indirilebilir otomatik DB yedeği yoktur. Bu nedenle yerel/off-site yedekler devam ettirilmelidir. Ücretli plan veya eklenti bu aşamada açılmamıştır.

Resmî kaynaklar:

- https://supabase.com/pricing
- https://supabase.com/docs/guides/platform/free-project-pausing
- https://supabase.com/docs/guides/platform/backups
- https://supabase.com/docs/guides/platform/regions

## Bağlantı seçimi

Supabase Dashboard içindeki **Connect** panelinden gerçek bağlantı dizesi alınır.

1. Migration ve tek seferlik yönetim işlemleri için ağ IPv6 destekliyorsa Direct connection tercih edilir.
2. Bu bilgisayar/ağ yalnızca IPv4 kullanıyorsa kalıcı ASP.NET backend için Session pooler (`5432`) kullanılır.
3. Transaction pooler (`6543`) migration aracı için kullanılmaz; kalıcı backend'e de test edilmeden atanmaz.
4. TLS kapatılmaz. Supabase'in verdiği bağlantı ayarları esas alınır.

Kaynak: https://supabase.com/docs/guides/database/connecting-to-postgres

## Secret'ların yerel girilmesi

Visual Studio'da `PetWork` projesine sağ tıklayıp **Manage User Secrets** açılır. Aşağıdaki anahtarlar gerçek değerlerle yalnızca yerel secret dosyasına eklenir:

```json
{
  "ConnectionStrings": {
    "SqlServerSource": "<Büşra'nın doğrulanmış SQL Server bağlantısı>",
    "PostgreSqlAdmin": "<Supabase migration/admin bağlantısı>",
    "PostgreSqlApp": "<daha sonra oluşturulacak sınırlı petwork_app rolü bağlantısı>"
  }
}
```

`UserSecretsId`: `PetWork-7f1c92d1-5e29-4b4d-a39d-782ae514cbed`.

User Secrets geliştirme makinesine özeldir; Git commit'iyle Seren'in bilgisayarına taşınmaz. Seren kendi local secret dosyasını ayrı dolduracaktır. Canlı sunucuda User Secrets yerine barındırma platformunun secret/environment-variable sistemi kullanılır.

## Veri erişim sınırı

Uygulama tabloları `petwork` şemasındadır. Data API kullanılmayacağı için bu şema exposed schemas listesine eklenmez. `anon` veya `authenticated` rollere doğrudan tablo erişimi verilmez. .NET backend sınırlı bir DB rolüyle bağlanır ve uygulama yetkilendirmesini kendisi uygular. Secret/service role veya DB parolası web/mobil istemciye konulmaz.

Kaynaklar:

- https://supabase.com/docs/guides/database/secure-data
- https://supabase.com/docs/guides/api/securing-your-api
