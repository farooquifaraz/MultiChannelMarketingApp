using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using MarketingApp.API.Filters;
using MarketingApp.API.Middleware;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Jobs;
using MarketingApp.Application.Mappings;
using MarketingApp.Application.Services;
using MarketingApp.Application.Validators;
using MarketingApp.Infrastructure.Caching;
using MarketingApp.Infrastructure.Persistence;
using MarketingApp.Infrastructure.Persistence.Repositories;
using MarketingApp.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            npgsqlOptions.CommandTimeout(30);
        })
    .UseSnakeCaseNamingConvention());

// Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "MarketingApp_";
});

// Hangfire
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(
        builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 5;
});

// JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Log.Warning("JWT Auth failed: {Error}", ctx.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));
    options.AddPolicy("send", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(builder.Configuration["AllowedOrigins"]?.Split(",") ?? ["http://localhost:5173", "http://localhost:3000"])
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateCampaignValidator>();
builder.Services.AddFluentValidationAutoValidation();

// AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

// HttpClientFactory (used by WhatsApp Cloud API and SMS gateway)
builder.Services.AddHttpClient();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppCloudService>();
builder.Services.AddScoped<ISmsService, SmsGatewayService>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ICampaignJobService, CampaignJobService>();
builder.Services.AddScoped<ISmtpSettingsService, SmtpSettingsService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
    options.Filters.Add<ValidationFilter>();
});

var app = builder.Build();

// Middleware Pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Marketing App API v1"));
}

app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHangfireDashboard("/hangfire");
app.MapHealthChecks("/health");

// Auto-create tables on startup in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var creator = db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
    try { await creator.CreateTablesAsync(); }
    catch { /* Tables may already exist */ }
}

app.Run();
