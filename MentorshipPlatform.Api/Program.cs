using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Serilog;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using MediatR;
using MentorshipPlatform.Application.Auth.Commands.RegisterUser;
using MentorshipPlatform.Application.Common.Behaviours;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Attributes;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Identity.Services;
using MentorshipPlatform.Infrastructure.Services;
using MentorshipPlatform.Api.Hubs;
using MentorshipPlatform.Api.Middleware;
using MentorshipPlatform.Api.Services;
using MentorshipPlatform.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;
using Amazon.S3;
using Amazon.Runtime;


var builder = WebApplication.CreateBuilder(args);

// Allow large file uploads (500 MB) for video course content
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000; // 500 MB
});

//Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "MentorshipPlatform.Api")
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Mentorship Platform API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
        npgsqlOptions.EnableRetryOnFailure(3);
    });
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});

builder.Services.AddScoped<IApplicationDbContext>(provider => 
    provider.GetRequiredService<ApplicationDbContext>());

// Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString) && redisConnectionString.StartsWith("rediss://"))
{
    // Convert rediss:// URL to StackExchange.Redis format
    var uri = new Uri(redisConnectionString);
    var password = uri.UserInfo.Contains(':') ? uri.UserInfo.Split(':')[1] : uri.UserInfo;
    redisConnectionString = $"{uri.Host}:{uri.Port},password={password},ssl=true,abortConnect=false";
}
else if (!string.IsNullOrEmpty(redisConnectionString) && redisConnectionString.StartsWith("redis://"))
{
    var uri = new Uri(redisConnectionString);
    var password = uri.UserInfo.Contains(':') ? uri.UserInfo.Split(':')[1] : uri.UserInfo;
    redisConnectionString = $"{uri.Host}:{uri.Port},password={password},abortConnect=false";
}
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString ?? "localhost:6379";
    options.InstanceName = "MentorshipPlatform.Api:";
});

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(FeatureFlagCheckBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
});

// External Auth Service
builder.Services.AddHttpClient();
builder.Services.AddScoped<IExternalAuthService, ExternalAuthService>();

// Email Service (dual-provider: SMTP + Resend, switchable via PlatformSettings)
builder.Services.AddScoped<IEmailProviderFactory, MentorshipPlatform.Infrastructure.Services.Email.EmailProviderFactory>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Notification Service (wraps email for bulk notifications)
builder.Services.AddScoped<INotificationService, NotificationService>();

// SMS Service (already configured for Twilio Video)
builder.Services.AddScoped<ISmsService, SmsService>();

// FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(RegisterUserCommandValidator).Assembly);

// Identity & JWT
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.Secret))
    };

    // Allow SignalR to receive JWT from query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireStudentRole", policy => 
        policy.RequireRole(UserRole.Student.ToString()));
    options.AddPolicy("RequireMentorRole", policy => 
        policy.RequireRole(UserRole.Mentor.ToString()));
    options.AddPolicy("RequireAdminRole", policy => 
        policy.RequireRole(UserRole.Admin.ToString()));
});

// External Services
builder.Services.Configure<IyzicoOptions>(builder.Configuration.GetSection("Iyzico"));
builder.Services.AddScoped<IPaymentService, IyzicoPaymentService>();

builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection("Twilio"));
builder.Services.AddScoped<IVideoService, TwilioVideoService>();

// Storage Service: R2 > MinIO > NoOp (fallback)
var r2Options = builder.Configuration.GetSection("R2").Get<R2Options>();
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
var minioOptions = builder.Configuration.GetSection("Minio").Get<MinioOptions>();

if (r2Options != null && !string.IsNullOrEmpty(r2Options.AccountId) && !string.IsNullOrEmpty(r2Options.AccessKey))
{
    // Cloudflare R2 (S3-compatible)
    builder.Services.Configure<R2Options>(builder.Configuration.GetSection("R2"));
    builder.Services.AddSingleton<IAmazonS3>(_ =>
    {
        var credentials = new BasicAWSCredentials(r2Options.AccessKey, r2Options.SecretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{r2Options.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
            SignatureVersion = "4",
            AuthenticationRegion = "auto",
        };
        return new AmazonS3Client(credentials, config);
    });
    builder.Services.AddScoped<IStorageService, R2StorageService>();
    Log.Information("✅ Cloudflare R2 storage configured - AccountId: {AccountId}, Bucket: {Bucket}",
        r2Options.AccountId, r2Options.BucketName);
}
else if (minioOptions != null && !string.IsNullOrEmpty(minioOptions.Endpoint) && !string.IsNullOrEmpty(minioOptions.AccessKey))
{
    // MinIO (local/self-hosted)
    builder.Services.AddMinio(configureClient => configureClient
        .WithEndpoint(minioOptions.Endpoint)
        .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
        .WithSSL(minioOptions.UseSSL));
    builder.Services.AddScoped<IStorageService, MinioStorageService>();
    Log.Information("✅ MinIO storage service configured - Endpoint: {Endpoint}", minioOptions.Endpoint);
}
else
{
    builder.Services.AddScoped<IStorageService, NoOpStorageService>();
    Log.Warning("⚠️ No storage configured. File upload disabled. Set R2 or Minio config to enable.");
}

// Process History (Audit Log)
builder.Services.AddScoped<IProcessHistoryService, ProcessHistoryService>();

// Feature Flags
builder.Services.AddScoped<IFeatureFlagService, FeatureFlagService>();

// Platform Settings
builder.Services.AddScoped<IPlatformSettingService, PlatformSettingService>();

// Admin Notification Service (grouped notifications)
builder.Services.AddScoped<IAdminNotificationService, AdminNotificationService>();

// Hangfire
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c =>
        c.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

// SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<IChatNotificationService, ChatNotificationService>();

// CORS
var allowedOrigins = new List<string> { "http://localhost:3000" };

// Frontend__BaseUrl env var
var frontendUrl = builder.Configuration["Frontend:BaseUrl"];
if (!string.IsNullOrEmpty(frontendUrl))
    allowedOrigins.Add(frontendUrl);

// FrontendUrl env var (fallback / eski format)
var frontendUrlLegacy = builder.Configuration["FrontendUrl"];
if (!string.IsNullOrEmpty(frontendUrlLegacy))
    allowedOrigins.Add(frontendUrlLegacy);

// CORS__AllowedOrigins__0, CORS__AllowedOrigins__1, ... env vars
var corsSection = builder.Configuration.GetSection("CORS:AllowedOrigins");
if (corsSection.Exists())
{
    foreach (var child in corsSection.GetChildren())
    {
        if (!string.IsNullOrEmpty(child.Value))
            allowedOrigins.Add(child.Value);
    }
}

var distinctOrigins = allowedOrigins.Distinct().ToArray();
Log.Information("CORS allowed origins: {Origins}", string.Join(", ", distinctOrigins));

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins(distinctOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Health Checks - only check PostgreSQL (Redis is optional/best-effort)
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString!);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();
app.UseSerilogRequestLogging();
app.UseCors("DevCors");
app.UseExceptionHandling(); // ✅ CORS'tan sonra, auth'dan önce - hata durumunda da CORS header'ları korunur
app.UseAuthentication();
app.UseAuthorization();
app.UseMaintenanceMode(); // Feature flag: maintenance_mode → 503 for non-admin users
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Register recurring background jobs
try
{
    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.ExpirePendingOrdersJob>(
        "expire-pending-orders", job => job.Execute(), "*/5 * * * *"); // Every 5 minutes

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.ExpirePendingBookingsJob>(
        "expire-pending-bookings", job => job.Execute(), "*/5 * * * *"); // Every 5 minutes

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.PaymentReconciliationJob>(
        "payment-reconciliation", job => job.Execute(), "0 * * * *"); // Every hour

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.DetectNoShowJob>(
        "detect-noshow", job => job.Execute(), "*/10 * * * *"); // Every 10 minutes

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.CleanupStaleSessionsJob>(
        "cleanup-stale-sessions", job => job.Execute(), "*/15 * * * *"); // Every 15 minutes

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.SendUnreadMessageNotificationJob>(
        "send-unread-message-notifications", job => job.Execute(), "*/2 * * * *"); // Every 2 minutes

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.AutoCompleteGroupClassesJob>(
        "auto-complete-group-classes", job => job.Execute(), "*/10 * * * *"); // Every 10 minutes

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.EnforceSessionEndJob>(
        "enforce-session-end", job => job.Execute(), "*/2 * * * *"); // Every 2 minutes — grace period enforcer

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.ExpireCreditJob>(
        "expire-credits", job => job.Execute(), "0 3 * * *"); // Daily at 03:00 UTC

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.CalculatePerformanceSummaryJob>(
        "calculate-performance-summary", job => job.Execute(), "0 2 * * *"); // Daily at 02:00 UTC

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.CalculateAccrualJob>(
        "calculate-accrual", job => job.Execute(), "0 4 1 * *"); // Monthly on the 1st at 04:00 UTC

    RecurringJob.AddOrUpdate<MentorshipPlatform.Application.Jobs.ExpirePendingSessionRequestsJob>(
        "expire-pending-session-requests", job => job.Execute(), "0 * * * *"); // Every hour
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to register recurring Hangfire jobs. They will be registered on next successful startup.");
}

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHealthChecks("/health");

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// Run migrations on startup
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
        Log.Information("Database migrations applied successfully");

        // Seed pivot feature flags
        await SeedPivotFeatureFlags(dbContext);

        // Seed CMS data if tables are empty
        await SeedCmsData(dbContext);

        // Seed TYT/AYT categories
        await SeedTytAytCategories(dbContext);

        // Seed email notification templates
        await MentorshipPlatform.Api.EmailTemplateSeedData.SeedEmailTemplates(dbContext);

        // Override PlatformSettings from environment variables (for SMTP/Resend config via Koyeb)
        await SyncEmailSettingsFromEnv(dbContext);
    }
}
catch (Exception ex)
{
    Log.Error(ex, "An error occurred while migrating the database");
}

// Sync email settings from environment variables into PlatformSettings table.
// This allows configuring SMTP/Resend credentials via Koyeb env vars
// without needing to use the admin panel.
static async Task SyncEmailSettingsFromEnv(ApplicationDbContext db)
{
    var envMappings = new Dictionary<string, string>
    {
        // SMTP
        ["SMTP__Host"] = "smtp_host",
        ["SMTP__Port"] = "smtp_port",
        ["SMTP__Username"] = "smtp_username",
        ["SMTP__Password"] = "smtp_password",
        ["SMTP__FromEmail"] = "smtp_from_email",
        ["SMTP__FromName"] = "smtp_from_name",
        // Resend
        ["Resend__ApiKey"] = "resend_api_key",
        ["Resend__FromEmail"] = "resend_from_email",
        ["Resend__FromName"] = "resend_from_name",
        // Provider selection
        ["Email__Provider"] = "email_provider",
    };

    var hasChanges = false;

    foreach (var (envKey, settingKey) in envMappings)
    {
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrEmpty(envValue)) continue;

        var setting = await db.PlatformSettings
            .FirstOrDefaultAsync(s => s.Key == settingKey);

        if (setting != null)
        {
            // Direct property update via private setter workaround
            if (setting.Value != envValue)
            {
                setting.UpdateValue(envValue, Guid.Empty);
                hasChanges = true;
                Log.Information("PlatformSetting '{Key}' updated from environment variable", settingKey);
            }
        }
        else
        {
            var category = settingKey.StartsWith("smtp") || settingKey.StartsWith("resend") || settingKey == "email_provider"
                ? "Email"
                : "General";
            db.PlatformSettings.Add(PlatformSetting.Create(settingKey, envValue, $"Set from env: {envKey}", category));
            hasChanges = true;
            Log.Information("PlatformSetting '{Key}' created from environment variable", settingKey);
        }
    }

    if (hasChanges)
    {
        await db.SaveChangesAsync();
        Log.Information("Email settings synced from environment variables");
    }
}

// Pivot Feature Flags Seed Data
static async Task SeedPivotFeatureFlags(ApplicationDbContext db)
{
    var pivotFlags = new List<(string key, bool enabled, string description)>
    {
        ("MARKETPLACE_MODE", false, "Marketplace modu: true = mentor basvurusu acik, mentor kurs olusturma acik"),
        ("EXTERNAL_MENTOR_REGISTRATION", false, "Disaridan mentor kayit formu"),
        ("MENTOR_SELF_COURSE_CREATION", false, "Mentorlerin kendi kurslarini olusturmasi"),
        ("MULTI_CATEGORY_MODE", false, "TYT/AYT disinda kategori eklenmesi"),
        ("COMMISSION_PAYMENT_MODEL", false, "Mentor komisyon bazli odeme sistemi"),
        ("PACKAGE_SYSTEM_ENABLED", true, "Paket satis ve kredi sistemi"),
        ("PRIVATE_LESSON_ENABLED", true, "Ozel ders modulu"),
        ("INSTRUCTOR_SELF_SCHEDULING", true, "Egitmenlerin kendi takvimlerini yonetmesi"),
        ("INSTRUCTOR_PERFORMANCE_TRACKING", true, "Egitmen performans takip sistemi"),
        ("INSTRUCTOR_PERFORMANCE_SELF_VIEW", false, "Egitmenlerin kendi performanslarini gormesi"),
        ("INSTRUCTOR_ACCRUAL_SELF_VIEW", false, "Egitmenlerin kendi hakedislerini gormesi"),
        ("INSTRUCTOR_COMPARISON_REPORT", true, "Egitmenler arasi karsilastirma raporu"),
        ("SESSION_REQUEST_ENABLED", true, "Seans talep sistemi"),
        ("PRICE_APPROVAL_REQUIRED", false, "Fiyat onay zorunlulugu"),
        ("FREE_SESSION_ENABLED", true, "Anlik seans (kredi ile)"),
    };

    foreach (var (key, enabled, description) in pivotFlags)
    {
        var exists = await db.FeatureFlags.AnyAsync(f => f.Key == key);
        if (!exists)
        {
            db.FeatureFlags.Add(FeatureFlag.Create(key, enabled, description));
        }
    }

    await db.SaveChangesAsync();
}

// TYT/AYT Category Seed Data
static async Task SeedTytAytCategories(ApplicationDbContext db)
{
    var tytAytCategories = new List<(string name, string icon, int sortOrder)>
    {
        // TYT
        ("TYT Matematik", "📐", 101),
        ("TYT Turkce", "📖", 102),
        ("TYT Fizik", "⚡", 103),
        ("TYT Kimya", "🧪", 104),
        ("TYT Biyoloji", "🧬", 105),
        ("TYT Tarih", "📜", 106),
        ("TYT Cografya", "🌍", 107),
        ("TYT Geometri", "📏", 108),
        // AYT
        ("AYT Matematik", "📐", 201),
        ("AYT Fizik", "⚡", 202),
        ("AYT Kimya", "🧪", 203),
        ("AYT Biyoloji", "🧬", 204),
        ("AYT Tarih", "📜", 205),
        ("AYT Cografya", "🌍", 206),
        ("AYT Edebiyat", "📚", 207),
        ("AYT Felsefe", "🤔", 208),
        // General
        ("Genel", "📂", 999),
    };

    foreach (var (name, icon, sortOrder) in tytAytCategories)
    {
        var exists = await db.Categories.AnyAsync(c => c.Name == name && c.EntityType == "Course");
        if (!exists)
        {
            db.Categories.Add(Category.Create(name, icon, sortOrder, "Course"));
        }
    }

    await db.SaveChangesAsync();
    Log.Information("TYT/AYT categories seeded successfully");
}

// CMS Seed Data
static async Task SeedCmsData(ApplicationDbContext db)
{
    try
    {
        // Seed Banners (only if none exist)
        if (!await db.Banners.AnyAsync())
        {
            db.Banners.AddRange(
                Banner.Create("Yaz Donemi %25 Indirim", "Tum birebir mentorluk paketlerinde gecerli. Sinirli sure!", null, "/public/mentors", "Top", null, null, 1),
                Banner.Create("Grup Dersleri Artik Aktif!", "Canli grup dersleriyle birlikte ogren, birlikte basla.", null, "/student/explore-classes", "Top", null, null, 2),
                Banner.Create("Iyzico ile Guvenli Odeme", "Tum odemeleriniz Iyzico altyapisiyla guvence altinda.", null, null, "Bottom", null, null, 3)
            );
            await db.SaveChangesAsync();
            Log.Information("CMS Banners seeded successfully");
        }

        // Seed Announcements (only if none exist)
        if (!await db.Announcements.AnyAsync())
        {
            db.Announcements.AddRange(
                Announcement.Create("Sistem Bakimi", "19 Subat 02:00-04:00 arasi planli bakim yapilacaktir. Bu sure zarfinda platforma erisim kisitli olabilir.", "Maintenance", "All", null, null, true),
                Announcement.Create("Video Kurslar Yayinda!", "Artik mentorler video kurs olusturup satabilir. Kendi hizinizda ogrenin!", "Info", "All", null, null, true),
                Announcement.Create("Profil Bilgilerinizi Tamamlayin", "Odeme islemi yapabilmek icin profil bilgilerinizin eksiksiz olmasi gerekmektedir.", "Warning", "Students", null, null, true)
            );
            await db.SaveChangesAsync();
            Log.Information("CMS Announcements seeded successfully");
        }

        // Seed Static Pages (only if none exist)
        if (!await db.StaticPages.AnyAsync())
        {
            var privacyContent = @"<div class=""space-y-8"">
<section><h2 class=""text-2xl font-bold mb-4"">1. Veri Sorumlusu</h2><p class=""text-gray-600 leading-relaxed"">degisimmentorluk.com (""Platform"") olarak, 6698 sayili Kisisel Verilerin Korunmasi Kanunu (""KVKK"") kapsaminda veri sorumlusu sifatiyla kisisel verilerinizi islemekteyiz.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">2. Toplanan Veriler</h2><p class=""text-gray-600 leading-relaxed mb-4"">Platformumuzu kullanirken asagidaki kisisel veriler toplanabilir:</p><ul class=""list-disc list-inside space-y-2 text-gray-600""><li>Kimlik bilgileri (ad, soyad)</li><li>Iletisim bilgileri (e-posta, telefon)</li><li>Egitim bilgileri (universite, bolum, mezuniyet yili)</li><li>Odeme bilgileri (Iyzico uzerinden islenir, kart bilgileri saklanmaz)</li><li>Kullanim verileri (oturum bilgileri, tercihler)</li></ul></section>
<section><h2 class=""text-2xl font-bold mb-4"">3. Verilerin Islenmesi Amaci</h2><ul class=""list-disc list-inside space-y-2 text-gray-600""><li>Uyelik ve hesap yonetimi</li><li>Mentorluk hizmetinin sunulmasi</li><li>Odeme islemlerinin gerceklestirilmesi</li><li>Iletisim ve destek hizmetleri</li><li>Yasal yukumluluklerin yerine getirilmesi</li></ul></section>
<section><h2 class=""text-2xl font-bold mb-4"">4. Verilerin Aktarilmasi</h2><p class=""text-gray-600 leading-relaxed"">Kisisel verileriniz, hizmet saglayicilarimiz (odeme altyapisi, sunucu hizmeti) ile paylasabilir. Ucuncu taraflarla sadece yasal zorunluluk halinde paylasilir.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">5. Cerezler</h2><p class=""text-gray-600 leading-relaxed"">Platformumuz, kullanici deneyimini iyilestirmek amaciyla cerezler kullanir. Tarayici ayarlarinizdan cerez tercihlerinizi yonetebilirsiniz.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">6. Veri Guvenligi</h2><p class=""text-gray-600 leading-relaxed"">SSL/TLS sifreleme, guvenli sunucu altyapisi ve erisim kontrolleri gibi teknik ve idari tedbirler uygulanmaktadir.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">7. KVKK Kapsamindaki Haklariniz</h2><ul class=""list-disc list-inside space-y-2 text-gray-600""><li>Kisisel verilerinizin islenip islenmedigini ogrenme</li><li>Islenmisse buna iliskin bilgi talep etme</li><li>Eksik veya yanlis islenmisse duzeltilmesini isteme</li><li>Silinmesini veya yok edilmesini isteme</li><li>Aleyhine bir sonuc cikmasi halinde itiraz etme</li></ul></section>
<section><h2 class=""text-2xl font-bold mb-4"">8. Iletisim</h2><p class=""text-gray-600 leading-relaxed"">KVKK kapsamindaki haklarinizi kullanmak icin destek@degisimmentorluk.com adresinden bize ulasabilirsiniz.</p></section>
</div>";

            var termsContent = @"<div class=""space-y-8"">
<section><h2 class=""text-2xl font-bold mb-4"">1. Genel Hukumler</h2><p class=""text-gray-600 leading-relaxed"">Bu kullanim sartlari, degisimmentorluk.com (""Platform"") uzerinden sunulan hizmetlerin kullanimina iliskin kosullari duzenler. Platformu kullanarak bu sartlari kabul etmis sayilirsiniz.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">2. Hizmet Tanimi</h2><p class=""text-gray-600 leading-relaxed"">Platform, mentorler ile danisanlar arasinda online mentorluk hizmeti saglar. Mentorler bagimsiz hizmet saglayicilaridir.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">3. Uyelik ve Hesap</h2><p class=""text-gray-600 leading-relaxed"">Platforma uye olmak icin gecerli bir e-posta adresi ve dogru kisisel bilgiler gereklidir. Hesabinizin guvenliginden siz sorumlusunuz.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">4. Odeme ve Iade</h2><p class=""text-gray-600 leading-relaxed"">Odemeler Iyzico altyapisi uzerinden islenir. Ders baslama saatinden 24 saat once yapilan iptallerde tam iade yapilir. 24 saatten az kalan iptallerde iade yapilmaz.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">5. Mentor Sorumluluklari</h2><p class=""text-gray-600 leading-relaxed"">Mentorler, belirledikleri saatlerde musait olmak ve profesyonel bir tutum sergilemekle yukumludur.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">6. Danisan Sorumluluklari</h2><p class=""text-gray-600 leading-relaxed"">Danisanlar, randevularina zamaninda katilmak ve mentorlere saygi gostermekle yukumludur.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">7. Icerik ve Fikri Mulkiyet</h2><p class=""text-gray-600 leading-relaxed"">Platformdaki tum icerikler degisimmentorluk.com'a aittir. Izinsiz kopyalanmasi yasaktir.</p></section>
<section><h2 class=""text-2xl font-bold mb-4"">8. Iletisim</h2><p class=""text-gray-600 leading-relaxed"">Sorulariniz icin destek@degisimmentorluk.com adresinden bize ulasabilirsiniz.</p></section>
</div>";

            var faqContent = @"<div class=""space-y-6"">
<details class=""border border-gray-200 rounded-xl bg-white""><summary class=""px-6 py-4 font-medium text-gray-900 cursor-pointer hover:bg-gray-50"">Mentorluk nedir?</summary><p class=""px-6 pb-4 text-gray-600"">Mentorluk, hedef universitesini kazanmis ogrencilerin, sinava hazirlanan ogrencilere birebir online gorusmeler araciligiyla rehberlik etmesidir.</p></details>
<details class=""border border-gray-200 rounded-xl bg-white""><summary class=""px-6 py-4 font-medium text-gray-900 cursor-pointer hover:bg-gray-50"">Nasil kayit olurum?</summary><p class=""px-6 pb-4 text-gray-600"">Ana sayfadaki ""Uye Ol"" butonuna tiklayarak kayit olabilirsiniz. Kayit ucretsizdir.</p></details>
<details class=""border border-gray-200 rounded-xl bg-white""><summary class=""px-6 py-4 font-medium text-gray-900 cursor-pointer hover:bg-gray-50"">Ucretlendirme nasil calisiyor?</summary><p class=""px-6 pb-4 text-gray-600"">Her mentor kendi ucretini belirler. Danisanlardan %7 platform bedeli alinir. Mentorlerden %15 komisyon kesilir.</p></details>
<details class=""border border-gray-200 rounded-xl bg-white""><summary class=""px-6 py-4 font-medium text-gray-900 cursor-pointer hover:bg-gray-50"">Odeme guvenligi nasil saglaniyor?</summary><p class=""px-6 pb-4 text-gray-600"">Tum odemeler Iyzico altyapisi uzerinden guvenlice islenir.</p></details>
<details class=""border border-gray-200 rounded-xl bg-white""><summary class=""px-6 py-4 font-medium text-gray-900 cursor-pointer hover:bg-gray-50"">Randevumu iptal edebilir miyim?</summary><p class=""px-6 pb-4 text-gray-600"">24 saat oncesine kadar ucretsiz iptal yapabilirsiniz. 24 saatten az kalan iptallerde iade yapilmaz.</p></details>
<details class=""border border-gray-200 rounded-xl bg-white""><summary class=""px-6 py-4 font-medium text-gray-900 cursor-pointer hover:bg-gray-50"">Mentor olmak icin ne gerekiyor?</summary><p class=""px-6 pb-4 text-gray-600"">Kayit olduktan sonra universite ve kimlik dogrulamasi yapmaniz gerekir. Basvurunuz admin tarafindan incelenir.</p></details>
</div>";

            var supportContent = @"<div class=""space-y-8"">
<div class=""grid md:grid-cols-2 gap-8"">
<div class=""bg-white rounded-xl border p-6""><h3 class=""font-bold text-lg mb-2"">📧 E-posta</h3><p class=""text-gray-600 mb-2"">Genel sorular ve destek icin:</p><a href=""mailto:destek@degisimmentorluk.com"" class=""text-indigo-600 hover:underline font-medium"">destek@degisimmentorluk.com</a></div>
<div class=""bg-white rounded-xl border p-6""><h3 class=""font-bold text-lg mb-2"">📞 Telefon</h3><p class=""text-gray-600 mb-2"">Bizi arayin:</p><a href=""tel:+905331408819"" class=""text-indigo-600 hover:underline font-medium"">0 533 140 88 19</a></div>
<div class=""bg-white rounded-xl border p-6""><h3 class=""font-bold text-lg mb-2"">📍 Adres</h3><p class=""text-gray-600"">Sancaktepe / Istanbul</p></div>
<div class=""bg-white rounded-xl border p-6""><h3 class=""font-bold text-lg mb-2"">🕐 Calisma Saatleri</h3><p class=""text-gray-600"">Pazartesi - Cuma: 09:00 - 18:00</p><p class=""text-gray-600"">Cumartesi: 10:00 - 14:00</p></div>
</div>
<div class=""bg-indigo-50 rounded-2xl p-8 text-center""><h2 class=""text-2xl font-bold mb-4"">Hizli Destek</h2><p class=""text-gray-600 max-w-xl mx-auto"">Sikca sorulan sorular icin <a href=""/public/faq"" class=""text-indigo-600 hover:underline font-medium"">SSS sayfamizi</a> ziyaret edebilirsiniz.</p></div>
</div>";

            db.StaticPages.AddRange(
                StaticPage.Create("gizlilik-politikasi", "Gizlilik Politikasi ve KVKK", privacyContent, "Gizlilik Politikasi - Degisim Mentorluk", "degisimmentorluk.com gizlilik politikasi ve KVKK aydinlatma metni"),
                StaticPage.Create("kullanim-sartlari", "Kullanim Sartlari", termsContent, "Kullanim Sartlari - Degisim Mentorluk", "degisimmentorluk.com kullanim sartlari ve kosullari"),
                StaticPage.Create("sss", "Sikca Sorulan Sorular", faqContent, "SSS - Degisim Mentorluk", "Mentorluk platformu hakkinda sikca sorulan sorular"),
                StaticPage.Create("destek", "Iletisim ve Destek", supportContent, "Destek - Degisim Mentorluk", "degisimmentorluk.com iletisim ve destek sayfasi")
            );
            await db.SaveChangesAsync();
            Log.Information("CMS Static Pages seeded successfully");
        }

        // Seed Categories (only if none exist)
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                // Course categories
                Category.Create("Yazılım & Programlama", "💻", 1, "Course"),
                Category.Create("Matematik", "📐", 2, "Course"),
                Category.Create("Fen Bilimleri", "🔬", 3, "Course"),
                Category.Create("Dil Eğitimi", "🌍", 4, "Course"),
                Category.Create("Müzik", "🎵", 5, "Course"),
                Category.Create("Sanat & Tasarım", "🎨", 6, "Course"),
                Category.Create("İş & Kariyer", "💼", 7, "Course"),
                Category.Create("Kişisel Gelişim", "🚀", 8, "Course"),
                Category.Create("Sınava Hazırlık", "📚", 9, "Course"),
                Category.Create("Spor & Sağlık", "🏃", 10, "Course"),
                Category.Create("Sosyal Bilimler", "📜", 11, "Course"),
                Category.Create("Diğer", "📂", 12, "Course"),
                // GroupClass categories
                Category.Create("Matematik", "📐", 1, "GroupClass"),
                Category.Create("Yazılım", "💻", 2, "GroupClass"),
                Category.Create("Müzik", "🎵", 3, "GroupClass"),
                Category.Create("Dil", "🌍", 4, "GroupClass"),
                Category.Create("Sanat", "🎨", 5, "GroupClass"),
                Category.Create("İş/Kariyer", "💼", 6, "GroupClass"),
                Category.Create("Bilim", "🔬", 7, "GroupClass"),
                Category.Create("Spor/Sağlık", "🏃", 8, "GroupClass"),
                Category.Create("Diğer", "📂", 9, "GroupClass")
            );
            await db.SaveChangesAsync();
            Log.Information("Categories seeded successfully");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "CMS seed data could not be applied (may already exist)");
    }
}

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast")
    .WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
// Hangfire Authorization Filter
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.IsInRole(UserRole.Admin.ToString());
    }
}