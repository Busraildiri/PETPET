using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PetWork.Data;
using PetWork.Services;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("mobile-auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("mobile-content", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var mobileJwtKey = builder.Configuration["MobileAuth:JwtKey"];
if (string.IsNullOrWhiteSpace(mobileJwtKey) || Encoding.UTF8.GetByteCount(mobileJwtKey) < 32)
{
    throw new InvalidOperationException(
        "MobileAuth:JwtKey must be configured and contain at least 32 UTF-8 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(mobileJwtKey)),
            ValidateIssuer = true,
            ValidIssuer = "PetWork",
            ValidateAudience = true,
            ValidAudience = "PetimMobile",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// Session servisi ekle
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
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
builder.Services.AddHttpClient<GooglePlacesService>(client =>
{
    client.BaseAddress = new Uri("https://places.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<StackExchangeContentProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.stackexchange.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PetWork/1.0 (https://github.com/Busraildiri/PETPET)");
});
builder.Services.AddHttpClient<UsdaFoodDataProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.nal.usda.gov/fdc/v1/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PetWork/1.0 (https://github.com/Busraildiri/PETPET)");
});
builder.Services.AddHttpClient<WikimediaContentProvider>(client =>
{
    client.BaseAddress = new Uri("https://en.wikipedia.org/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PetWork/1.0 (https://github.com/Busraildiri/PETPET)");
});
builder.Services.AddHttpClient<WikibooksRecipeProvider>(client =>
{
    client.BaseAddress = new Uri("https://en.wikibooks.org/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PetWork/1.0 (https://github.com/Busraildiri/PETPET)");
});
builder.Services.AddScoped<IExternalContentProvider>(sp => sp.GetRequiredService<StackExchangeContentProvider>());
builder.Services.AddScoped<IExternalContentProvider>(sp => sp.GetRequiredService<UsdaFoodDataProvider>());
builder.Services.AddScoped<IExternalContentProvider>(sp => sp.GetRequiredService<WikimediaContentProvider>());
builder.Services.AddScoped<IExternalContentProvider>(sp => sp.GetRequiredService<WikibooksRecipeProvider>());
builder.Services.AddSingleton<IContentLicensePolicy, ContentLicensePolicy>();
builder.Services.AddSingleton<IContentSanitizer, HtmlPlainTextSanitizer>();
builder.Services.AddSingleton<IContentSafetyReviewService, ContentSafetyReviewService>();
builder.Services.AddHttpClient<ITranslationService, ConfigurableTranslationService>(client =>
    client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddScoped<IExternalContentImportService, ExternalContentImportService>();
builder.Services.AddScoped<IExternalContentPublishingService, ExternalContentPublishingService>();
builder.Services.AddHostedService<ExternalContentAutoPublisher>();
if (builder.Configuration.GetValue("ExternalContent:BootstrapOnStartup", true))
    builder.Services.AddHostedService<ExternalContentBootstrapService>();
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

var allowMobileDevelopmentHttp = app.Environment.IsDevelopment() &&
                                 builder.Configuration.GetValue<bool>("MobileDevelopment:AllowHttp");
if (!allowMobileDevelopmentHttp)
{
    app.UseHttpsRedirection();
}
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
app.UseRateLimiter();

app.UseSession(); // Use Session middleware

// Mobil istemci için token tabanlı kimlik doğrulama ayrı tasarlanana kadar API
// yazma işlemleri yalnızca mevcut yönetici oturumuna açıktır. GET/HEAD/OPTIONS
// uçları halka açık kalır; hassas User alanları ayrıca JSON'dan çıkarılmıştır.
app.Use(async (context, next) =>
{
    var isApiRequest = context.Request.Path.StartsWithSegments("/api");
    var isMobileAuthRequest = context.Request.Path.StartsWithSegments("/api/mobile/auth");
    var isMobileQuestionRequest = context.Request.Path.StartsWithSegments("/api/mobile/questions");
    var isMobileSocialRequest = context.Request.Path.StartsWithSegments("/api/mobile/social");
    var isReadOnlyMethod = HttpMethods.IsGet(context.Request.Method) ||
                           HttpMethods.IsHead(context.Request.Method) ||
                           HttpMethods.IsOptions(context.Request.Method);

    if (isApiRequest && !isReadOnlyMethod && !isMobileAuthRequest && !isMobileQuestionRequest && !isMobileSocialRequest)
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
