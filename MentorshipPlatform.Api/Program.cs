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
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Identity.Services;
using MentorshipPlatform.Infrastructure.Services;
using MentorshipPlatform.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;


var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "MentorshipPlatform.Api:";
});

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
});

// Email Service
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, EmailService>();

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

builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
var minioOptions = builder.Configuration.GetSection("Minio").Get<MinioOptions>()!;
builder.Services.AddMinio(configureClient => configureClient
    .WithEndpoint(minioOptions.Endpoint)
    .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
    .WithSSL(minioOptions.UseSSL));
builder.Services.AddScoped<IStorageService, MinioStorageService>();

// Process History (Audit Log)
builder.Services.AddScoped<IProcessHistoryService, ProcessHistoryService>();

// Hangfire
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => 
        c.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

// CORS
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("AllowAll", policy =>
//     {
//         policy.AllowAnyOrigin()
//               .AllowAnyMethod()
//               .AllowAnyHeader();
//     });
// });
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseSerilogRequestLogging();
app.UseCors("DevCors");
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Register recurring background jobs
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

app.MapControllers();
app.MapHealthChecks("/health");

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// Run migrations on startup (dev only, use proper migrations in production)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
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