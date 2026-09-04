using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Session servisi ekle
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Session ömrünü daha uzun tutmak için Cookie ayarları
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(1);
    options.SlidingExpiration = true;
});

// HttpContextAccessor servisi ekle
builder.Services.AddHttpContextAccessor();

// Güvenlik geliştirmeleri
builder.Services.AddAntiforgery(options => 
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
});

// SQL Server kaynak/geri dönüş sağlayıcısı olarak korunur. PostgreSQL seçildiğinde
// controller'lar aynı PetWorkDbContext modelini türetilmiş context üzerinden kullanır.
var databaseProvider = builder.Configuration["DatabaseProvider"]?.Trim() ?? "SqlServer";

if (databaseProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
{
    var postgresConnection = PostgreSqlConnectionString.Normalize(
        builder.Configuration.GetConnectionString("PostgreSqlApp")
            ?? throw new InvalidOperationException(
                "PostgreSqlApp connection string is required when DatabaseProvider is PostgreSql."));

    builder.Services.AddDbContext<PostgresPetWorkDbContext>(options =>
    {
        options.UseNpgsql(
            postgresConnection,
            postgresOptions => postgresOptions.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                PostgresPetWorkDbContext.SchemaName));

        ConfigureDatabaseDiagnostics(options, builder);
    });

    builder.Services.AddScoped<PetWorkDbContext>(services =>
        services.GetRequiredService<PostgresPetWorkDbContext>());
}
else if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var sqlServerConnection = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is required for SQL Server.");

    builder.Services.AddDbContext<PetWorkDbContext>(options =>
    {
        options.UseSqlServer(
            sqlServerConnection,
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null));

        ConfigureDatabaseDiagnostics(options, builder);
    });
}
else
{
    throw new InvalidOperationException(
        $"Unsupported DatabaseProvider '{databaseProvider}'. Use SqlServer or PostgreSql.");
}

// API servisleri
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// HttpClient ve web scraping servisleri
builder.Services.AddHttpClient();
builder.Services.AddScoped<WebScrapingService>();
builder.Services.AddScoped<ExperienceService>();

var app = builder.Build();

// Ortak/hedef DB migration'ları kontrollü bir dağıtım adımıdır.
// Varsayılan false'tur; uygulama açılışı şemayı kendiliğinden değiştirmez.
if (builder.Configuration.GetValue<bool>("DatabaseMigrations:ApplyOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<PetWorkDbContext>();
        context.Database.Migrate();
        Console.WriteLine("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Güvenlik başlıkları ekle
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Content-Security-Policy", 
        "default-src 'self' https://* http://*; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://* http://*; " +
        "style-src 'self' 'unsafe-inline' https://* http://*; " +
        "font-src 'self' https://* http://* data:; " +
        "img-src 'self' https://* http://* data:; " +
        "connect-src 'self' https://* http://*;");
    await next();
});

app.UseRouting();

app.UseSession(); // Use Session middleware

// Mobil istemci için token tabanlı kimlik doğrulama ayrı tasarlanana kadar API
// yazma işlemleri yalnızca mevcut yönetici oturumuna açıktır. GET/HEAD/OPTIONS
// uçları halka açık kalır; hassas User alanları ayrıca JSON'dan çıkarılmıştır.
app.Use(async (context, next) =>
{
    var isApiRequest = context.Request.Path.StartsWithSegments("/api");
    var isReadOnlyMethod = HttpMethods.IsGet(context.Request.Method) ||
                           HttpMethods.IsHead(context.Request.Method) ||
                           HttpMethods.IsOptions(context.Request.Method);

    if (isApiRequest && !isReadOnlyMethod)
    {
        var userId = context.Session.GetInt32("UserId");
        if (userId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Authentication required." });
            return;
        }

        var isAdmin = string.Equals(
            context.Session.GetString("IsAdmin"),
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

        if (!isAdmin)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Administrator permission required." });
            return;
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers(); // API controller'ları için

app.Run();

static void ConfigureDatabaseDiagnostics(
    DbContextOptionsBuilder options,
    WebApplicationBuilder builder)
{
    if (!builder.Environment.IsDevelopment())
    {
        return;
    }

    options.EnableDetailedErrors();

    if (builder.Configuration.GetValue<bool>("DatabaseDiagnostics:EnableSensitiveDataLogging"))
    {
        options.EnableSensitiveDataLogging();
    }
}
