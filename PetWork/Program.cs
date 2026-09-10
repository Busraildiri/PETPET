using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using PetWork.Data;
using PetWork.Services;
using PetWork.Security;
using PetWork.Validation;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("rate-limits.json", optional: false, reloadOnChange: true);

if (builder.Environment.IsProduction() &&
    builder.Configuration.GetValue<bool>("DatabaseMigrations:ApplyOnStartup"))
    throw new InvalidOperationException(
        "DatabaseMigrations:ApplyOnStartup is disabled in Production. Run migrations as a controlled deployment step.");

if (builder.Environment.IsProduction() &&
    builder.Configuration.GetValue<bool>("ExternalContent:BootstrapOnStartup"))
    throw new InvalidOperationException(
        "ExternalContent:BootstrapOnStartup is disabled in Production. Bootstrap data in a controlled maintenance step.");

const string corsPolicyName = "TrustedClientOrigins";
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
foreach (var origin in allowedCorsOrigins)
{
    if (origin.Contains('*', StringComparison.Ordinal) ||
        !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
        uri.Scheme is not ("https" or "http") ||
        uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        throw new InvalidOperationException($"Cors:AllowedOrigins contains an invalid exact origin: '{origin}'.");
    if (builder.Environment.IsProduction() && uri.Scheme != Uri.UriSchemeHttps)
        throw new InvalidOperationException("Production CORS origins must use HTTPS.");
}

// Add services to the container.
builder.Services.AddControllersWithViews(options => options.Filters.Add<ApiInputValidationFilter>());
builder.Services.AddCors(options => options.AddPolicy(corsPolicyName, policy =>
{
    // Kimlik bilgileri açıkken wildcard origin geçersiz ve güvensizdir; yalnızca tam origin listesi kullanılır.
    policy.WithOrigins(allowedCorsOrigins)
        .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete)
        .WithHeaders("Accept", "Content-Type", "Authorization", "X-CSRF-TOKEN")
        .AllowCredentials();
}));
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = false;
});
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
    options.HttpsPort = builder.Configuration.GetValue<int?>("HttpsRedirection:HttpsPort");
});
builder.Services.Configure<RateLimitSettings>(builder.Configuration.GetSection("RateLimits"));
builder.Services.AddSingleton<SensitiveEndpointRateLimiter>();
builder.Services.AddSingleton<DailyCostQuotaService>();
builder.Services.AddRateLimiter(options =>
{
    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:MobileAuthPermitLimit", 8);
    var contentPermitLimit = builder.Configuration.GetValue("RateLimiting:MobileContentPermitLimit", 12);
    options.AddPolicy("mobile-auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("mobile-content", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = contentPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("mobile-autocomplete", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 90,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    // Medya uçları tek ekranda onlarca istek alır; limit yalnızca sürekli
    // taramayı durdurmak içindir, normal gezinmeyi engellememelidir.
    options.AddPolicy("media", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 240,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
            ? delay
            : TimeSpan.FromMinutes(1);
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            message = $"Çok fazla istek gönderdin. {seconds} saniye sonra tekrar dene.",
            retryAfterSeconds = seconds
        }, cancellationToken);
    };
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
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sessionClaim = context.Principal?.FindFirst("sid")?.Value;
                if (!Guid.TryParse(sessionClaim, out var sessionId))
                {
                    context.Fail("Session claim missing.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<PetWorkDbContext>();
                var now = DateTime.UtcNow;
                var active = await db.MobileAuthSessions.AsNoTracking()
                    .AnyAsync(session =>
                        session.Id == sessionId &&
                        session.RevokedAt == null &&
                        session.AccessExpiresAt > now,
                        context.HttpContext.RequestAborted);
                if (!active) context.Fail("Session is no longer active.");
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<EmailVerificationService>();
var useResend = string.Equals(builder.Configuration["Email:Provider"], "Resend", StringComparison.OrdinalIgnoreCase);
if (useResend)
{
    foreach (var key in new[] { "Email:Resend:ApiKey", "Email:Resend:FromAddress", "Email:PasswordReset:DeepLinkBase" })
        if (string.IsNullOrWhiteSpace(builder.Configuration[key]))
            throw new InvalidOperationException($"Required email configuration '{key}' is missing.");
    builder.Services.AddScoped<IPasswordResetEmailSender, ResendPasswordResetEmailSender>();
    builder.Services.AddSingleton<IEmailVerificationSender, ResendEmailVerificationSender>();
}
else if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddScoped<IPasswordResetEmailSender, DevelopmentPasswordResetEmailSender>();
    builder.Services.AddSingleton<IEmailVerificationSender, DevelopmentEmailVerificationSender>();
}
else
{
    throw new InvalidOperationException("Email:Provider must be Resend outside Development.");
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var value in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        if (IPAddress.TryParse(value, out var address)) options.KnownProxies.Add(address);

    // Render terminates TLS at its edge and uses a dynamic proxy network. The
    // container itself is not exposed directly, so trust exactly one proxy hop.
    if (builder.Configuration.GetValue<bool>("RENDER"))
    {
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

// Session servisi ekle
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Session ömrünü daha uzun tutmak için Cookie ayarları
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(1);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// HttpContextAccessor servisi ekle
builder.Services.AddHttpContextAccessor();

// Güvenlik geliştirmeleri
builder.Services.AddAntiforgery(options => 
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
    options.JsonSerializerOptions.UnmappedMemberHandling =
        System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
});

// HttpClient ve web scraping servisleri
builder.Services.AddHttpClient();
builder.Services.AddScoped<MobilePushNotificationService>();
builder.Services.AddScoped<SecureMediaStorageService>();
builder.Services.AddScoped<MobileMediaStorageService>();
builder.Services.AddHostedService<MobileMediaBackfillService>();
builder.Services.AddScoped<ResourceAuthorizationService>();
builder.Services.AddScoped<AdminAccessService>();
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
if (builder.Configuration.GetValue("ExternalContent:BootstrapOnStartup", false))
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
app.UseForwardedHeaders();
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

// Rapor modu bilinçlidir: ihlaller gözlemlendikten sonra bu değer
// Content-Security-Policy başlığına taşınarak zorlayıcı hale getirilebilir.
var contentSecurityPolicyReportOnly = string.Join(" ",
    "default-src 'self';",
    "base-uri 'self';",
    "object-src 'none';",
    "frame-ancestors 'self';",
    "form-action 'self';",
    "script-src 'self' https://cdn.jsdelivr.net;",
    "style-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://fonts.googleapis.com;",
    "font-src 'self' https://cdnjs.cloudflare.com https://fonts.gstatic.com data:;",
    "img-src 'self' data:;",
    "connect-src 'self';",
    "frame-src 'none';",
    "media-src 'self';",
    "worker-src 'self';",
    "manifest-src 'self';",
    "report-uri /security/csp-report;");

// Statik dosya yanıtları da aynı başlıkları alsın diye UseStaticFiles'dan önce çalışır.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["X-XSS-Protection"] = "0";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] =
        "accelerometer=(), ambient-light-sensor=(), autoplay=(), browsing-topics=(), camera=(), " +
        "display-capture=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), " +
        "payment=(), picture-in-picture=(), publickey-credentials-create=(), usb=()";
    context.Response.Headers["Content-Security-Policy-Report-Only"] = contentSecurityPolicyReportOnly;
    await next();
});
app.UseStaticFiles();

app.UseRouting();
app.UseCors(corsPolicyName);
app.UseRateLimiter();

app.UseCookiePolicy();
app.UseSession(); // Use Session middleware

// Yönetim alanı ve eski API yazma uçları için veritabanındaki güncel rol
// yetkilidir. Oturumdaki IsAdmin kopyası rol geri alındıktan sonra eski kalabilir.
app.Use(async (context, next) =>
{
    var isAdminArea = context.Request.Path.StartsWithSegments("/admin");
    var isApiRequest = context.Request.Path.StartsWithSegments("/api");
    var isMobileAuthRequest = context.Request.Path.StartsWithSegments("/api/mobile/auth");
    var isMobileQuestionRequest = context.Request.Path.StartsWithSegments("/api/mobile/questions");
    var isMobileSocialRequest = context.Request.Path.StartsWithSegments("/api/mobile/social");
    var isMobileNearbyRequest = context.Request.Path.StartsWithSegments("/api/mobile/nearby");
    var isMobilePetRequest = context.Request.Path.StartsWithSegments("/api/mobile/pets");
    var isMobilePatiMatchRequest = context.Request.Path.StartsWithSegments("/api/mobile/pati-match");
    var isMobileLostPetsRequest = context.Request.Path.StartsWithSegments("/api/mobile/lost-pets");
    var isMobileAdoptionRequest = context.Request.Path.StartsWithSegments("/api/mobile/adoption");
    var isMobileReviewRequest = context.Request.Path.StartsWithSegments("/api/mobile/product-reviews");
    var isMobileNotificationRequest = context.Request.Path.StartsWithSegments("/api/mobile/notifications");
    var isReadOnlyMethod = HttpMethods.IsGet(context.Request.Method) ||
                           HttpMethods.IsHead(context.Request.Method) ||
                           HttpMethods.IsOptions(context.Request.Method);

    var isLegacyApiWrite = isApiRequest && !isReadOnlyMethod && !isMobileAuthRequest &&
                           !isMobileQuestionRequest && !isMobileSocialRequest && !isMobileNearbyRequest &&
                           !isMobilePetRequest && !isMobilePatiMatchRequest && !isMobileLostPetsRequest &&
                           !isMobileAdoptionRequest && !isMobileReviewRequest && !isMobileNotificationRequest;

    if (isAdminArea || isLegacyApiWrite)
    {
        var access = await context.RequestServices.GetRequiredService<AdminAccessService>()
            .CheckAsync(context, context.RequestAborted);
        if (!access.IsAuthenticated)
        {
            if (isAdminArea)
            {
                var returnUrl = Uri.EscapeDataString(context.Request.PathBase + context.Request.Path + context.Request.QueryString);
                context.Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Authentication required." });
            }
            return;
        }

        if (!access.IsAdmin)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            if (isLegacyApiWrite)
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
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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

public partial class Program { }
