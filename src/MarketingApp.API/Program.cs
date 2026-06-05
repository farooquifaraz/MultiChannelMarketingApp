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
using MarketingApp.Application.Configuration;
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

// Layer in .env file if present (repo root). Environment variables take priority over appsettings.json.
// This lets ops swap JWT secret / DB password / Redis without touching appsettings.json.
var envFile = Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env");
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
        var eq = trimmed.IndexOf('=');
        if (eq <= 0) continue;
        var key = trimmed[..eq].Trim();
        var value = trimmed[(eq + 1)..].Trim().Trim('"');
        if (!string.IsNullOrEmpty(value))
            Environment.SetEnvironmentVariable(key, value);
    }
    builder.Configuration.AddEnvironmentVariables();
}

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
            },
            // SignalR (Day 8) sends the JWT via the query string ?access_token= because the browser
            // WebSocket API can't set Authorization headers. Pull it for /hubs paths only.
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
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

// SignalR (Day 8 — real-time inbox push)
builder.Services.AddSignalR();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Inbox.IInboxRealtimeNotifier,
    MarketingApp.API.Realtime.SignalRInboxNotifier>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateCampaignValidator>();
builder.Services.AddFluentValidationAutoValidation();

// AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

// HttpClientFactory (used by WhatsApp Cloud API and SMS gateway)
builder.Services.AddHttpClient();

// Campaign settings (configurable send rate / delays — for deliverability + spam avoidance)
builder.Services.Configure<CampaignSettings>(
    builder.Configuration.GetSection(CampaignSettings.SectionName));

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppCloudService>();
builder.Services.AddScoped<IWhatsAppTemplateService, WhatsAppTemplateService>();
builder.Services.AddScoped<IWhatsAppInboundService, MarketingApp.Application.Jobs.WhatsAppInboundService>();
builder.Services.AddScoped<IBillingService, MarketingApp.Application.Services.BillingService>();
builder.Services.AddScoped<IQuotaService, MarketingApp.Application.Services.QuotaService>();
builder.Services.AddScoped<IOrganizationService, MarketingApp.Application.Services.OrganizationService>();
builder.Services.AddScoped<ISmsService, SmsGatewayService>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ICampaignJobService, CampaignJobService>();
builder.Services.AddScoped<ISmtpSettingsService, SmtpSettingsService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<ISmtpGroupService, SmtpGroupService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();

// === Day 7 G2: Inbound webhook handlers (strategy pattern) ===
// Add a new provider here = register one IInboundWebhookHandler. Factory + controller auto-pick it up.
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Webhooks.IInboundWebhookHandler,
    MarketingApp.Infrastructure.Services.Webhooks.SendGridWebhookHandler>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Webhooks.IInboundWebhookHandler,
    MarketingApp.Infrastructure.Services.Webhooks.BrevoWebhookHandler>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Webhooks.IInboundWebhookHandler,
    MarketingApp.Infrastructure.Services.Webhooks.MailgunWebhookHandler>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Webhooks.IWebhookHandlerFactory,
    MarketingApp.Infrastructure.Services.Webhooks.WebhookHandlerFactory>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Webhooks.IWebhookProcessor,
    MarketingApp.Application.Services.Webhooks.WebhookProcessor>();

// === Day 7 G3: Inbox polling ===
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Inbox.IInboxFetcher,
    MarketingApp.Infrastructure.Services.Inbox.MailKitImapInboxFetcher>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Inbox.IInboxPollingService,
    MarketingApp.Application.Jobs.InboxPollingService>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Inbox.IInboxService,
    MarketingApp.Application.Services.InboxService>();

// === Day 7 G5: AI clients (strategy pattern) ===
// 5 first-party + universal OpenAI-compatible adapter. Adding new providers = drop one IAiClient + 1 line.
builder.Services.AddHttpClient(); // needed by BaseHttpAiClient
builder.Services.AddScoped<MarketingApp.Application.Interfaces.AI.IAiClient,
    MarketingApp.Infrastructure.Services.AI.AnthropicAiClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.AI.IAiClient,
    MarketingApp.Infrastructure.Services.AI.OpenAiClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.AI.IAiClient,
    MarketingApp.Infrastructure.Services.AI.GeminiAiClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.AI.IAiClient,
    MarketingApp.Infrastructure.Services.AI.GrokAiClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.AI.IAiClient,
    MarketingApp.Infrastructure.Services.AI.OpenAiCompatibleAiClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.AI.IAiClientFactory,
    MarketingApp.Infrastructure.Services.AI.AiClientFactory>();
// Phase 3 — AI image generation (mock works keyless; DALL·E activates when configured).
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Media.IImageGenerationClient,
    MarketingApp.Infrastructure.Services.Media.MockImageGenerationClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Media.IImageGenerationClient,
    MarketingApp.Infrastructure.Services.Media.DalleImageClient>();
// P3.3 — more image providers: Pollinations (FREE, no key), Gemini (free tier), Stability, HuggingFace (free tier).
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Media.IImageGenerationClient,
    MarketingApp.Infrastructure.Services.Media.PollinationsImageClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Media.IImageGenerationClient,
    MarketingApp.Infrastructure.Services.Media.GeminiImageClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Media.IImageGenerationClient,
    MarketingApp.Infrastructure.Services.Media.StabilityImageClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Media.IImageGenerationClient,
    MarketingApp.Infrastructure.Services.Media.HuggingFaceImageClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Media.IImageGenerationClient,
    MarketingApp.Infrastructure.Services.Media.FluxImageClient>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Media.IImageGenerationClientFactory,
    MarketingApp.Infrastructure.Services.Media.ImageGenerationClientFactory>();
builder.Services.AddScoped<IImageGenerationService,
    MarketingApp.Application.Services.ImageGenerationService>();
builder.Services.AddScoped<IBrandKitService, MarketingApp.Application.Services.BrandKitService>();
builder.Services.AddScoped<IIntegrationCredentialService, MarketingApp.Application.Services.IntegrationCredentialService>();
builder.Services.AddScoped<IMarketingContentService, MarketingApp.Application.Services.MarketingContentService>();
// P2.2 — payment providers (mock activates immediately keyless; Stripe activates when configured).
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Billing.IBillingProvider,
    MarketingApp.Infrastructure.Services.Billing.MockBillingProvider>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Billing.IBillingProvider,
    MarketingApp.Infrastructure.Services.Billing.StripeBillingProvider>();
builder.Services.AddScoped<MarketingApp.Application.Interfaces.Billing.IBillingProviderFactory,
    MarketingApp.Infrastructure.Services.Billing.BillingProviderFactory>();
builder.Services.AddScoped<ICheckoutService, MarketingApp.Application.Services.CheckoutService>();
// G6 — AI reply orchestrator (Hangfire-enqueued from InboxPollingService)
builder.Services.AddScoped<MarketingApp.Application.Interfaces.AI.IAiReplyService,
    MarketingApp.Application.Services.AI.AiReplyService>();
// Day 9 — Ask-AI chat agent
builder.Services.AddScoped<MarketingApp.Application.Interfaces.AI.IAiChatService,
    MarketingApp.Application.Services.AI.AiChatService>();
// Day 10 — AI executor with primary→fallback failover (used by reply + chat services)
builder.Services.AddScoped<MarketingApp.Application.Interfaces.AI.IAiExecutor,
    MarketingApp.Application.Services.AI.AiExecutor>();

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
// Serve uploaded static files (e.g. signature images) from wwwroot/uploads/
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MarketingApp.API.Hubs.InboxHub>("/hubs/inbox");
app.MapHangfireDashboard("/hangfire");
app.MapHealthChecks("/health");

// Auto-create tables + run idempotent column migrations on startup.
// Every statement below uses CREATE TABLE / ADD COLUMN IF NOT EXISTS, so re-running on
// an already-migrated database is a no-op. Safe in production too — previously this was
// gated on IsDevelopment(), which meant fresh production deploys ended up with an empty
// public schema (only the hangfire-owned tables existed) and every DB call 500'd. The
// braces below still scope the `using var scope`, just unconditionally.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var creator = db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
    try { await creator.CreateTablesAsync(); }
    catch { /* Tables may already exist */ }

    // Idempotent column additions for incremental schema evolution.
    // We don't use EF migrations because the project uses CreateTables on a fresh DB —
    // but new columns added to existing entities still need to be reconciled with prod-like DBs.
    // Every statement uses IF NOT EXISTS so it's safe to run repeatedly.
    var migrationSql = new[]
    {
        // Day 6 — F1 click tracking
        "ALTER TABLE campaign_messages ADD COLUMN IF NOT EXISTS clicked_at timestamp with time zone",
        "ALTER TABLE campaign_messages ADD COLUMN IF NOT EXISTS click_count integer NOT NULL DEFAULT 0",
        // Day 6 — F2 bounce auto-flag
        "ALTER TABLE contacts ADD COLUMN IF NOT EXISTS is_bounced boolean NOT NULL DEFAULT false",
        "ALTER TABLE contacts ADD COLUMN IF NOT EXISTS bounced_at timestamp with time zone",
        "ALTER TABLE contacts ADD COLUMN IF NOT EXISTS bounce_reason text",
        // Day 6 — F3 per-SmtpGroup rate limits
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS delay_between_messages_ms integer",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS max_messages_per_minute integer",
        // Day 7 — G1 Message-ID + delivery confirmation
        "ALTER TABLE campaign_messages ADD COLUMN IF NOT EXISTS smtp_message_id character varying(255)",
        "ALTER TABLE campaign_messages ADD COLUMN IF NOT EXISTS delivery_confirmation_kind character varying(20)",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_campaign_messages_smtp_message_id ON campaign_messages (smtp_message_id) WHERE smtp_message_id IS NOT NULL",
        // Day 7 — G2 webhook secrets + event log
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS send_grid_webhook_secret character varying(500)",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS brevo_webhook_secret character varying(500)",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS mailgun_webhook_secret character varying(500)",
        @"CREATE TABLE IF NOT EXISTS webhook_event_logs (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            provider character varying(30) NOT NULL,
            provider_event_id character varying(255) NOT NULL,
            event_type character varying(40) NOT NULL,
            campaign_message_id uuid NULL,
            received_at timestamp with time zone NOT NULL DEFAULT NOW(),
            raw_payload jsonb NULL,
            CONSTRAINT uq_webhook_event_logs UNIQUE (provider, provider_event_id))",
        "CREATE INDEX IF NOT EXISTS ix_webhook_event_logs_campaign_message_id ON webhook_event_logs (campaign_message_id)",
        // Day 7 — G3 IMAP polling fields on smtp_groups
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS enable_inbox_polling boolean NOT NULL DEFAULT false",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS imap_host character varying(200)",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS imap_port integer NOT NULL DEFAULT 993",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS imap_enable_ssl boolean NOT NULL DEFAULT true",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS imap_username character varying(200)",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS imap_password character varying(500)",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS imap_folder character varying(100) NOT NULL DEFAULT 'INBOX'",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS last_imap_uid bigint NOT NULL DEFAULT 0",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS last_inbox_polled_at timestamp with time zone",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS inbox_polling_interval_minutes integer",
        "ALTER TABLE smtp_groups ADD COLUMN IF NOT EXISTS default_inbox_owner_user_id uuid",
        // Day 7 — G3 InboxMessage + OutboundReply tables
        @"CREATE TABLE IF NOT EXISTS inbox_messages (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            owner_user_id uuid NOT NULL,
            smtp_group_id uuid NOT NULL,
            imap_uid bigint NOT NULL,
            imap_folder character varying(100) NOT NULL DEFAULT 'INBOX',
            from_email character varying(255) NOT NULL,
            from_name character varying(200),
            to_email character varying(255) NOT NULL,
            subject character varying(500) NOT NULL,
            html_body text,
            text_body text,
            received_at timestamp with time zone NOT NULL,
            in_reply_to_message_id character varying(255),
            references_header character varying(2000),
            matched_campaign_message_id uuid,
            matched_contact_id uuid,
            is_orphan_reply boolean NOT NULL DEFAULT false,
            is_read boolean NOT NULL DEFAULT false,
            is_archived boolean NOT NULL DEFAULT false,
            ai_category character varying(40),
            ai_summary text,
            ai_suggested_reply text,
            user_edited_reply text,
            ai_generated_at timestamp with time zone,
            draft_saved_at timestamp with time zone,
            ai_generation_error text,
            ai_provider_used character varying(30),
            ai_input_tokens integer,
            ai_output_tokens integer,
            replied_at timestamp with time zone,
            replied_by_user_id uuid,
            reply_was_edited boolean NOT NULL DEFAULT false,
            created_at timestamp with time zone NOT NULL DEFAULT NOW(),
            updated_at timestamp with time zone NOT NULL DEFAULT NOW(),
            CONSTRAINT fk_inbox_owner_user FOREIGN KEY (owner_user_id) REFERENCES users(id) ON DELETE RESTRICT,
            CONSTRAINT fk_inbox_smtp_group FOREIGN KEY (smtp_group_id) REFERENCES smtp_groups(id) ON DELETE RESTRICT,
            CONSTRAINT fk_inbox_campaign_message FOREIGN KEY (matched_campaign_message_id) REFERENCES campaign_messages(id) ON DELETE SET NULL,
            CONSTRAINT fk_inbox_contact FOREIGN KEY (matched_contact_id) REFERENCES contacts(id) ON DELETE SET NULL)",
        "CREATE INDEX IF NOT EXISTS ix_inbox_owner ON inbox_messages (owner_user_id)",
        "CREATE INDEX IF NOT EXISTS ix_inbox_owner_unread ON inbox_messages (owner_user_id, is_read)",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_inbox_group_folder_uid ON inbox_messages (smtp_group_id, imap_folder, imap_uid)",
        "CREATE INDEX IF NOT EXISTS ix_inbox_matched_campaign_message ON inbox_messages (matched_campaign_message_id)",
        "CREATE INDEX IF NOT EXISTS ix_inbox_in_reply_to ON inbox_messages (in_reply_to_message_id)",
        @"CREATE TABLE IF NOT EXISTS outbound_replies (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            inbox_message_id uuid NOT NULL,
            sent_by_user_id uuid NOT NULL,
            subject character varying(500) NOT NULL,
            was_edited boolean NOT NULL DEFAULT false,
            sent_at timestamp with time zone NOT NULL DEFAULT NOW(),
            send_succeeded boolean NOT NULL DEFAULT false,
            error_message character varying(2000),
            CONSTRAINT fk_outbound_inbox FOREIGN KEY (inbox_message_id) REFERENCES inbox_messages(id) ON DELETE CASCADE,
            CONSTRAINT fk_outbound_user FOREIGN KEY (sent_by_user_id) REFERENCES users(id) ON DELETE RESTRICT)",
        "CREATE INDEX IF NOT EXISTS ix_outbound_inbox ON outbound_replies (inbox_message_id)",
        "CREATE INDEX IF NOT EXISTS ix_outbound_user ON outbound_replies (sent_by_user_id)",
        // Day 7 — G3 SystemSettings inbox cron
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS inbox_polling_cron character varying(50) NOT NULL DEFAULT '*/2 * * * *'",
        // Day 7 — G5 SystemSettings AI fields
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_provider character varying(30) NOT NULL DEFAULT 'disabled'",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_api_key character varying(500)",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_base_url character varying(500)",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_model character varying(100) NOT NULL DEFAULT 'claude-sonnet-4-5'",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_system_prompt text NOT NULL DEFAULT ''",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_max_tokens integer NOT NULL DEFAULT 800",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_temperature numeric(3,2) NOT NULL DEFAULT 0.4",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_timeout_seconds integer NOT NULL DEFAULT 30",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_allow_send_recipient_pii boolean NOT NULL DEFAULT true",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_allowed_categories_csv character varying(500) NOT NULL DEFAULT 'question,interested,complaint,unsubscribe,spam,other'",
        // === Day 7 G9: Re-route inbox messages to their CORRECT campaign owners ===
        // Earlier owner-resolution code used Contact.UserId as a proxy, which sends everything
        // to whoever CREATED the contact (typically admin) instead of who SENT the campaign.
        // This one-shot UPDATE walks matched_campaign_message_id -> campaign_messages.campaign_id
        // -> campaigns.user_id and sets owner_user_id correctly. Idempotent: skips rows that are
        // already correct. Safe to run on every startup.
        @"UPDATE inbox_messages im
            SET owner_user_id = c.user_id, updated_at = NOW()
          FROM campaign_messages cm
          JOIN campaigns c ON c.id = cm.campaign_id
          WHERE im.matched_campaign_message_id = cm.id
            AND im.owner_user_id IS DISTINCT FROM c.user_id",
        // === Day 8 H1/H2: threading columns ===
        "ALTER TABLE inbox_messages ADD COLUMN IF NOT EXISTS message_id character varying(255)",
        "ALTER TABLE inbox_messages ADD COLUMN IF NOT EXISTS thread_id uuid NOT NULL DEFAULT gen_random_uuid()",
        "ALTER TABLE inbox_messages ADD COLUMN IF NOT EXISTS normalized_subject character varying(500)",
        "CREATE INDEX IF NOT EXISTS ix_inbox_message_id ON inbox_messages (message_id)",
        "CREATE INDEX IF NOT EXISTS ix_inbox_thread_id ON inbox_messages (thread_id)",
        "CREATE INDEX IF NOT EXISTS ix_inbox_owner_thread ON inbox_messages (owner_user_id, thread_id)",
        "CREATE INDEX IF NOT EXISTS ix_inbox_normalized_subject ON inbox_messages (normalized_subject)",
        "ALTER TABLE outbound_replies ADD COLUMN IF NOT EXISTS smtp_message_id character varying(255)",
        "ALTER TABLE outbound_replies ADD COLUMN IF NOT EXISTS body_html text",
        "ALTER TABLE outbound_replies ADD COLUMN IF NOT EXISTS in_reply_to_message_id character varying(255)",
        "ALTER TABLE outbound_replies ADD COLUMN IF NOT EXISTS thread_id uuid NOT NULL DEFAULT gen_random_uuid()",
        "CREATE INDEX IF NOT EXISTS ix_outbound_smtp_message_id ON outbound_replies (smtp_message_id)",
        "CREATE INDEX IF NOT EXISTS ix_outbound_thread_id ON outbound_replies (thread_id)",
        // === Day 9: Ask-AI agent (suggested questions column + chat table) ===
        "ALTER TABLE inbox_messages ADD COLUMN IF NOT EXISTS ai_suggested_questions text",
        @"CREATE TABLE IF NOT EXISTS inbox_ai_chats (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            thread_id uuid NOT NULL,
            owner_user_id uuid NOT NULL,
            role character varying(20) NOT NULL,
            content text NOT NULL,
            input_tokens integer,
            output_tokens integer,
            provider_used character varying(30),
            created_at timestamp with time zone NOT NULL DEFAULT NOW())",
        "CREATE INDEX IF NOT EXISTS ix_inbox_ai_chats_thread ON inbox_ai_chats (thread_id, created_at)",
        "CREATE INDEX IF NOT EXISTS ix_inbox_ai_chats_owner ON inbox_ai_chats (owner_user_id)",
        "ALTER TABLE inbox_ai_chats ADD COLUMN IF NOT EXISTS suggestions text",
        // === Day 10: fallback AI provider (auto-failover on quota) ===
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_fallback_provider character varying(30) NOT NULL DEFAULT 'disabled'",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_fallback_api_key character varying(500)",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_fallback_base_url character varying(500)",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS ai_fallback_model character varying(100) NOT NULL DEFAULT 'llama-3.3-70b-versatile'",
        // === Day 11: CRITICAL data-integrity fix ===
        // The campaigns.group_id and campaigns.template_id FKs were created with ON DELETE CASCADE
        // (EF Core's default for required relationships). That means deleting a ContactGroup or a
        // MessageTemplate SILENTLY cascade-deletes every campaign using it — and (via the campaign
        // cascade) all of that campaign's messages, reports and history. Quick-send creates a throwaway
        // group per send, so deleting those temp groups wiped real campaigns. Convert both FKs to
        // ON DELETE RESTRICT so a group/template that still has campaigns simply CANNOT be deleted
        // (clear error) instead of destroying history. Idempotent: only rewrites CASCADE constraints.
        @"DO $$
        DECLARE r record;
        BEGIN
            FOR r IN
                SELECT con.conname
                FROM pg_constraint con
                JOIN pg_class rel ON rel.oid = con.conrelid
                JOIN pg_attribute att ON att.attrelid = con.conrelid AND att.attnum = ANY(con.conkey)
                WHERE con.contype = 'f' AND rel.relname = 'campaigns'
                  AND att.attname IN ('group_id','template_id')
                  AND con.confdeltype = 'c'
            LOOP
                EXECUTE format('ALTER TABLE campaigns DROP CONSTRAINT %I', r.conname);
            END LOOP;

            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint con
                JOIN pg_class rel ON rel.oid = con.conrelid
                JOIN pg_attribute att ON att.attrelid = con.conrelid AND att.attnum = ANY(con.conkey)
                WHERE con.contype = 'f' AND rel.relname = 'campaigns' AND att.attname = 'group_id'
            ) THEN
                ALTER TABLE campaigns ADD CONSTRAINT fk_campaigns_group_restrict
                    FOREIGN KEY (group_id) REFERENCES contact_groups(id) ON DELETE RESTRICT;
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint con
                JOIN pg_class rel ON rel.oid = con.conrelid
                JOIN pg_attribute att ON att.attrelid = con.conrelid AND att.attnum = ANY(con.conkey)
                WHERE con.contype = 'f' AND rel.relname = 'campaigns' AND att.attname = 'template_id'
            ) THEN
                ALTER TABLE campaigns ADD CONSTRAINT fk_campaigns_template_restrict
                    FOREIGN KEY (template_id) REFERENCES message_templates(id) ON DELETE RESTRICT;
            END IF;
        END $$;",
        // === Day 8 backfill: collapse existing messages into threads by normalized subject + owner ===
        // Day 8: bump existing installs from the old 2-min default to 1-min (only if still on the old default).
        "UPDATE system_settings SET inbox_polling_cron = '*/1 * * * *' WHERE inbox_polling_cron = '*/2 * * * *'",
        // Set normalized_subject first (strip Re:/Fwd: prefixes, lowercase).
        @"UPDATE inbox_messages
            SET normalized_subject = LOWER(TRIM(REGEXP_REPLACE(subject, '^\s*((re|fwd|fw)\s*:\s*)+', '', 'i')))
          WHERE normalized_subject IS NULL",
        // Group rows sharing (owner, normalized_subject) under one shared thread_id (the min id's thread).
        @"WITH grp AS (
            SELECT owner_user_id, normalized_subject, MIN(thread_id::text)::uuid AS canonical_thread
            FROM inbox_messages
            WHERE normalized_subject IS NOT NULL AND normalized_subject <> ''
            GROUP BY owner_user_id, normalized_subject
            HAVING COUNT(*) > 1)
          UPDATE inbox_messages im
            SET thread_id = grp.canonical_thread, updated_at = NOW()
          FROM grp
          WHERE im.owner_user_id = grp.owner_user_id
            AND im.normalized_subject = grp.normalized_subject
            AND im.thread_id IS DISTINCT FROM grp.canonical_thread",
        // L1 — WhatsApp media on templates (image/document/video sent with Body as caption).
        "ALTER TABLE message_templates ADD COLUMN IF NOT EXISTS media_url character varying(1000)",
        "ALTER TABLE message_templates ADD COLUMN IF NOT EXISTS media_type character varying(20)",
        "ALTER TABLE message_templates ADD COLUMN IF NOT EXISTS media_file_name character varying(255)",
        // L2 — cached WhatsApp approved templates synced from Meta Business Manager.
        @"CREATE TABLE IF NOT EXISTS whatsapp_templates (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            smtp_group_id uuid NOT NULL REFERENCES smtp_groups(id) ON DELETE CASCADE,
            name character varying(512) NOT NULL,
            language character varying(20) NOT NULL DEFAULT 'en_US',
            category character varying(40) NOT NULL DEFAULT 'MARKETING',
            status character varying(20) NOT NULL DEFAULT 'PENDING',
            body_text text NOT NULL DEFAULT '',
            variable_count integer NOT NULL DEFAULT 0,
            header_type character varying(20),
            meta_template_id character varying(100),
            synced_at timestamp with time zone NOT NULL DEFAULT NOW(),
            created_at timestamp with time zone NOT NULL DEFAULT NOW()
          )",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_whatsapp_templates_group_name_lang ON whatsapp_templates (smtp_group_id, name, language)",
        // L3 — WhatsApp inbound messages share the unified inbox. Add a channel discriminator and
        // split the dedup index: email dedups on IMAP UID, WhatsApp dedups on the Meta message id.
        "ALTER TABLE inbox_messages ADD COLUMN IF NOT EXISTS channel character varying(20) NOT NULL DEFAULT 'email'",
        "DROP INDEX IF EXISTS ix_inbox_group_folder_uid",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_inbox_email_dedup ON inbox_messages (smtp_group_id, imap_folder, imap_uid) WHERE channel = 'email'",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_inbox_whatsapp_dedup ON inbox_messages (smtp_group_id, message_id) WHERE channel = 'whatsapp' AND message_id IS NOT NULL",
        "CREATE INDEX IF NOT EXISTS ix_inbox_owner_channel ON inbox_messages (owner_user_id, channel)",
        // Phase 2 — billing: plans + subscriptions. Additive; quota enforcement is OFF by default
        // (system_settings.enable_quotas) so existing sends are never blocked.
        @"CREATE TABLE IF NOT EXISTS plans (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            code character varying(40) NOT NULL,
            name character varying(100) NOT NULL,
            price_aed_monthly numeric(10,2) NOT NULL DEFAULT 0,
            max_contacts integer NOT NULL DEFAULT 0,
            max_emails_per_month integer NOT NULL DEFAULT 0,
            max_whats_app_per_month integer NOT NULL DEFAULT 0,
            max_ai_per_month integer NOT NULL DEFAULT 0,
            max_users integer NOT NULL DEFAULT 1,
            is_active boolean NOT NULL DEFAULT true,
            sort_order integer NOT NULL DEFAULT 0,
            created_at timestamp with time zone NOT NULL DEFAULT NOW()
          )",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_plans_code ON plans (code)",
        @"CREATE TABLE IF NOT EXISTS subscriptions (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id uuid NOT NULL,
            plan_code character varying(40) NOT NULL DEFAULT 'free',
            status character varying(20) NOT NULL DEFAULT 'active',
            current_period_start timestamp with time zone NOT NULL DEFAULT NOW(),
            current_period_end timestamp with time zone NOT NULL DEFAULT (NOW() + interval '1 month'),
            external_customer_id character varying(255),
            external_subscription_id character varying(255),
            created_at timestamp with time zone NOT NULL DEFAULT NOW(),
            updated_at timestamp with time zone NOT NULL DEFAULT NOW()
          )",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_subscriptions_user ON subscriptions (user_id)",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS enable_quotas boolean NOT NULL DEFAULT false",
        // Seed the roadmap pricing tiers (idempotent — ON CONFLICT on the unique code).
        @"INSERT INTO plans (code, name, price_aed_monthly, max_contacts, max_emails_per_month, max_whats_app_per_month, max_ai_per_month, max_users, sort_order) VALUES
            ('free',     'Free',     0,     100,    500,     50,     50,     1, 1),
            ('starter',  'Starter',  69,    2500,   10000,   1000,   500,    1, 2),
            ('pro',      'Pro',      179,   10000,  50000,   5000,   2500,   3, 3),
            ('business', 'Business', 479,   50000,  250000,  25000,  10000,  10, 4),
            ('agency',   'Agency',   1099,  -1,     1000000, 100000, 50000,  -1, 5)
          ON CONFLICT (code) DO UPDATE SET
            name = EXCLUDED.name, price_aed_monthly = EXCLUDED.price_aed_monthly,
            max_contacts = EXCLUDED.max_contacts, max_emails_per_month = EXCLUDED.max_emails_per_month,
            max_whats_app_per_month = EXCLUDED.max_whats_app_per_month, max_ai_per_month = EXCLUDED.max_ai_per_month,
            max_users = EXCLUDED.max_users, sort_order = EXCLUDED.sort_order",
        // P2.4 — multi-tenancy foundation. ADDITIVE + zero-regression: introduces an Organization
        // grouping and a nullable users.organization_id, backfills every existing user to a single
        // seeded "Legacy Organization". Query-level tenant isolation is NOT enabled here — it is
        // gated behind system_settings.enable_multi_tenancy (default false), so behaviour is
        // identical to today (data still scoped by user_id) until that flag is switched on.
        @"CREATE TABLE IF NOT EXISTS organizations (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            name character varying(150) NOT NULL,
            slug character varying(80) NOT NULL,
            owner_user_id uuid NULL,
            plan_code character varying(40) NOT NULL DEFAULT 'free',
            is_active boolean NOT NULL DEFAULT true,
            created_at timestamp with time zone NOT NULL DEFAULT NOW(),
            updated_at timestamp with time zone NOT NULL DEFAULT NOW()
          )",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_organizations_slug ON organizations (slug)",
        // Seed the Legacy org with the fixed id referenced by Organization.LegacyOrgId.
        @"INSERT INTO organizations (id, name, slug, plan_code) VALUES
            ('00000000-0000-0000-0000-00000000ace0', 'Legacy Organization', 'legacy', 'free')
          ON CONFLICT (slug) DO NOTHING",
        "ALTER TABLE users ADD COLUMN IF NOT EXISTS organization_id uuid NULL",
        "CREATE INDEX IF NOT EXISTS ix_users_organization_id ON users (organization_id)",
        // Backfill every pre-existing user to the Legacy org (idempotent — only touches NULLs).
        "UPDATE users SET organization_id = '00000000-0000-0000-0000-00000000ace0' WHERE organization_id IS NULL",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS enable_multi_tenancy boolean NOT NULL DEFAULT false",
        // Phase 3 — AI image / banner generation. Additive: generated_assets table + image provider
        // settings. Default provider is 'mock' (offline SVG placeholder) so the Banner Studio works
        // with no keys; switching to 'dalle' + a key enables real OpenAI Images generation.
        @"CREATE TABLE IF NOT EXISTS generated_assets (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id uuid NOT NULL,
            prompt character varying(1000) NOT NULL,
            provider character varying(40) NOT NULL DEFAULT 'mock',
            size character varying(20) NOT NULL,
            width integer NOT NULL DEFAULT 0,
            height integer NOT NULL DEFAULT 0,
            status character varying(20) NOT NULL DEFAULT 'pending',
            image_url text,
            credit_cost integer NOT NULL DEFAULT 1,
            error_message text,
            created_at timestamp with time zone NOT NULL DEFAULT NOW()
          )",
        "CREATE INDEX IF NOT EXISTS ix_generated_assets_user_created ON generated_assets (user_id, created_at)",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS image_provider character varying(40) NOT NULL DEFAULT 'mock'",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS image_api_key text",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS image_base_url text",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS image_model character varying(60) NOT NULL DEFAULT 'dall-e-3'",
        // P2.2 — payment provider settings. Additive; default 'mock' activates plans immediately so the
        // upgrade loop works keyless. Switch to 'stripe' + keys for real hosted checkout.
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS payment_provider character varying(40) NOT NULL DEFAULT 'mock'",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS payment_api_key text",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS payment_webhook_secret text",
        "ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS payment_base_url text",
        // P3.2 — brand kits. Additive: a brand_kits table + an optional brand_kit_id on generated
        // assets (nullable, so generation without a kit is unaffected).
        @"CREATE TABLE IF NOT EXISTS brand_kits (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            user_id uuid NOT NULL,
            name character varying(100) NOT NULL,
            logo_url text,
            primary_color character varying(9) NOT NULL DEFAULT '#4f46e5',
            secondary_color character varying(9),
            accent_color character varying(9),
            font_family character varying(60) NOT NULL DEFAULT 'Inter',
            is_default boolean NOT NULL DEFAULT false,
            created_at timestamp with time zone NOT NULL DEFAULT NOW(),
            updated_at timestamp with time zone NOT NULL DEFAULT NOW()
          )",
        "CREATE INDEX IF NOT EXISTS ix_brand_kits_user ON brand_kits (user_id)",
        "ALTER TABLE generated_assets ADD COLUMN IF NOT EXISTS brand_kit_id uuid NULL",
        // P3.5 — per-provider credential vault. Additive: remembers each provider's key so switching
        // the active provider never loses another's key. The active provider per category still lives
        // in system_settings, so existing code paths are unchanged.
        @"CREATE TABLE IF NOT EXISTS integration_credentials (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            category character varying(20) NOT NULL,
            provider character varying(40) NOT NULL,
            api_key text,
            model character varying(120),
            base_url text,
            secondary_secret text,
            created_at timestamp with time zone NOT NULL DEFAULT NOW(),
            updated_at timestamp with time zone NOT NULL DEFAULT NOW()
          )",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_integration_credentials_cat_prov ON integration_credentials (category, provider)",
        // Backfill the vault from whatever is currently configured in system_settings (idempotent).
        @"INSERT INTO integration_credentials (category, provider, api_key, model, base_url)
            SELECT 'ai', ai_provider, ai_api_key, ai_model, ai_base_url FROM system_settings
            WHERE ai_provider IS NOT NULL AND ai_provider <> 'disabled' AND ai_api_key IS NOT NULL
          ON CONFLICT (category, provider) DO NOTHING",
        @"INSERT INTO integration_credentials (category, provider, api_key, model, base_url)
            SELECT 'image', image_provider, image_api_key, image_model, image_base_url FROM system_settings
            WHERE image_provider IS NOT NULL AND image_api_key IS NOT NULL
          ON CONFLICT (category, provider) DO NOTHING",
        @"INSERT INTO integration_credentials (category, provider, api_key, base_url, secondary_secret)
            SELECT 'payment', payment_provider, payment_api_key, payment_base_url, payment_webhook_secret FROM system_settings
            WHERE payment_provider IS NOT NULL AND payment_api_key IS NOT NULL
          ON CONFLICT (category, provider) DO NOTHING",
    };
    foreach (var sql in migrationSql)
    {
        try
        {
            var rows = await db.Database.ExecuteSqlRawAsync(sql);
            if (sql.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
                app.Logger.LogInformation("Startup migration UPDATE affected {Rows} rows: {Sql}", rows, sql.Substring(0, Math.Min(80, sql.Length)));
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Startup migration skipped: {Sql}", sql.Substring(0, Math.Min(80, sql.Length)));
        }
    }

    // === Day 7 G9 enhanced — fix ORPHAN messages (sent before G1's SmtpMessageId existed) ===
    // For each orphan inbox message, try to find a campaign whose contact's email matches
    // the reply's FromEmail. If unique match found, assign that campaign's user as owner.
    // This rescues all "orphan" messages from before Day 7.
    try
    {
        const string orphanFix = @"
WITH candidates AS (
    SELECT im.id AS inbox_id, camp.user_id AS new_owner,
           ROW_NUMBER() OVER (PARTITION BY im.id ORDER BY camp.created_at DESC) AS rn
    FROM inbox_messages im
    JOIN contacts ct ON LOWER(ct.email) = LOWER(im.from_email)
    JOIN campaign_messages cm ON cm.contact_id = ct.id
    JOIN campaigns camp ON camp.id = cm.campaign_id
    WHERE im.matched_campaign_message_id IS NULL
)
UPDATE inbox_messages im
SET owner_user_id = cand.new_owner, is_orphan_reply = false, updated_at = NOW()
FROM candidates cand
WHERE im.id = cand.inbox_id
  AND cand.rn = 1
  AND im.owner_user_id IS DISTINCT FROM cand.new_owner";

        var orphanRows = await db.Database.ExecuteSqlRawAsync(orphanFix);
        app.Logger.LogInformation("G9 orphan-rescue migration re-routed {Rows} inbox messages to their campaign senders.", orphanRows);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "G9 orphan-rescue migration failed.");
    }
}

// === Day 7 G3: Register inbox-poll recurring job ===
// MUST run AFTER startup migrations so SystemSettings.GetAsync can read inbox_polling_cron.
try
{
    using var rjScope = app.Services.CreateScope();
    var ss = rjScope.ServiceProvider.GetRequiredService<MarketingApp.Application.Interfaces.ISystemSettingsService>();
    var settings = await ss.GetAsync();
    var cron = settings.InboxPollingCron;
    if (!string.IsNullOrWhiteSpace(cron))
    {
        Hangfire.RecurringJob.AddOrUpdate<MarketingApp.Application.Interfaces.Inbox.IInboxPollingService>(
            "inbox-poll",
            j => j.PollAllAsync(CancellationToken.None),
            cron);
        app.Logger.LogInformation("Registered Hangfire recurring job 'inbox-poll' with cron '{Cron}'", cron);
    }
    else
    {
        Hangfire.RecurringJob.RemoveIfExists("inbox-poll");
        app.Logger.LogInformation("Inbox polling cron is empty — recurring job 'inbox-poll' removed.");
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Failed to register inbox-poll recurring job — feature will be disabled until next restart.");
}

app.Run();
