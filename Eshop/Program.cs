using CloudinaryDotNet;
using Eshop.Areas.Admin.Repository;
using Eshop.Constants;
using Eshop.Helpers;
using Eshop.Hubs;
using Eshop.Jobs;
using Eshop.Models;
using Eshop.Models.Configurations;
using Eshop.Models.Momo;
using Eshop.Repository;
using Eshop.Security;
using Eshop.Services;
using Eshop.Services.Momo;
using Eshop.Services.VNPay;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

ConfigureLocalConfiguration(builder);

builder.Services.Configure<HangfireSettings>(
    builder.Configuration.GetSection("Hangfire"));
builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection trong cấu hình.");

// DB
builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseSqlServer(defaultConnection);
});

// Hangfire
builder.Services.AddHangfire((serviceProvider, configuration) =>
{
    var hangfireSettings = serviceProvider.GetRequiredService<IOptions<HangfireSettings>>().Value;

    configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(defaultConnection, new SqlServerStorageOptions
        {
            PrepareSchemaIfNecessary = true,
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.FromSeconds(Math.Max(1, hangfireSettings.QueuePollIntervalSeconds)),
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        });
});

builder.Services.AddHangfireServer(options =>
{
    var queuePollIntervalSeconds =
        builder.Configuration.GetValue<int?>("Hangfire:QueuePollIntervalSeconds") ?? 15;
    options.SchedulePollingInterval = TimeSpan.FromSeconds(Math.Max(1, queuePollIntervalSeconds));
});

// Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "Logs/recommendation-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Forwarded Headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// Services
builder.Services.AddScoped<IPcCompatibilityService, PcCompatibilityService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICheckoutPricingService, CheckoutPricingService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IMomoService, MomoService>();
builder.Services.AddScoped<ICatalogCacheService, CatalogCacheService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<ISupportChatService, SupportChatService>();
builder.Services.AddScoped<RecommendationTrainingService>();
builder.Services.AddScoped<RecommendationPredictService>();
builder.Services.AddScoped<IBuildRequirementExtractor, BuildRequirementExtractor>();
builder.Services.AddScoped<IPcBuildRecommendationService, PcBuildRecommendationService>();
builder.Services.AddScoped<IPcBuildSuggestionService, PcBuildSuggestionService>();
builder.Services.AddScoped<PcBuildChatIntentAnalyzer>();
builder.Services.AddScoped<PcBuildChatProductSuggestionService>();
builder.Services.AddScoped<IPcBuildChatService, PcBuildChatService>();
builder.Services.AddScoped<IPcBuildStorageService, PcBuildStorageService>();
builder.Services.AddScoped<IPcBuildWorkbookService, PcBuildWorkbookService>();
builder.Services.AddScoped<IPcBuildShareService, PcBuildShareService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IOrderStateService, OrderStateService>();
builder.Services.AddScoped<IProductCatalogRagSyncService, ProductCatalogRagSyncService>();
builder.Services.AddScoped<InventoryReservationCleanupJob>();
builder.Services.AddScoped<OrderConfirmationEmailJob>();
builder.Services.AddScoped<ProductCatalogRagSyncJob>();

builder.Services.Configure<MomoOptionModel>(
    builder.Configuration.GetSection("MomoAPI")
);
builder.Services.Configure<RagServiceOptions>(
    builder.Configuration.GetSection("RagService")
);

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                x => x.Key,
                x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
            );

        return new BadRequestObjectResult(new
        {
            message = "Model binding failed",
            errors
        });
    };
});

// MVC + AntiForgery
builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Cache + Session
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "Eshop:";
});

builder.Services.AddMemoryCache();

builder.Services.AddHybridCache(options =>
{
    options.MaximumKeyLength = 1024;
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// RAG Client
builder.Services.AddHttpClient<ILlmChatClient, LlmChatClient>();
builder.Services.AddHttpClient<RagClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<RagServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Cloudinary
builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<IOptions<CloudinarySettings>>().Value;

    var account = new Account(
        settings.CloudName,
        settings.ApiKey,
        settings.ApiSecret);

    var cloudinary = new Cloudinary(account);
    cloudinary.Api.Secure = true;

    return cloudinary;
});

// Identity
builder.Services.AddIdentity<AppUserModel, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 3;

    options.User.RequireUniqueEmail = true;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<DataContext>()
.AddDefaultTokenProviders();

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.AdminOnly, policy => policy.RequireRole(RoleNames.Admin));
    options.AddPolicy(PolicyNames.BackOfficeAccess, policy => policy.RequireRole(RoleNames.BackOfficeRoles));
    options.AddPolicy(PolicyNames.CatalogManagement, policy => policy.RequireRole(RoleNames.CatalogRoles));
    options.AddPolicy(PolicyNames.BrandManagement, policy => policy.RequireRole(RoleNames.BrandRoles));
    options.AddPolicy(PolicyNames.OrderManagement, policy => policy.RequireRole(RoleNames.OrderRoles));
    options.AddPolicy(PolicyNames.WarehouseManagement, policy => policy.RequireRole(RoleNames.WarehouseRoles));
    options.AddPolicy(PolicyNames.SupportManagement, policy => policy.RequireRole(RoleNames.SupportRoles));
    options.AddPolicy(PolicyNames.CustomerSelfService, policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole(RoleNames.Customer) &&
            !context.User.IsInAnyRole(RoleNames.BackOfficeRoles)));
    options.AddPolicy(PolicyNames.WishlistCompareAccess, policy =>
        policy.RequireAssertion(context => context.User.CanUseWishlistAndCompare()));
});

// Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;

    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToAccessDenied = context =>
        {
            var returnUrl = Uri.EscapeDataString(
                $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");

            var canUseAdminAccessDenied =
                context.Request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase) &&
                context.HttpContext.User.CanAccessBackOffice();

            var targetPath = canUseAdminAccessDenied
                ? $"/Admin/Account/AccessDenied?returnUrl={returnUrl}"
                : $"/Account/AccessDenied?returnUrl={returnUrl}";

            context.Response.Redirect(targetPath);
            return Task.CompletedTask;
        }
    };
});

// Google Login
var authenticationBuilder = builder.Services.AddAuthentication();
var googleClientId = builder.Configuration["GoogleKeys:ClientId"];
var googleClientSecret = builder.Configuration["GoogleKeys:ClientSecret"];

if (!string.IsNullOrWhiteSpace(googleClientId) &&
    !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authenticationBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false;
    options.MaximumReceiveMessageSize = 32 * 1024;
});

var app = builder.Build();

await IdentityDataSeeder.SeedAsync(app.Services);

app.UseForwardedHeaders();

// Error handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statuscode={0}");

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();

app.Use(async (context, next) =>
{
    var requestPath = context.Request.Path.Value ?? string.Empty;
    var isPublicAdminEndpoint =
        string.Equals(requestPath, "/Admin/Shipping/GetProvincesV2", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(requestPath, "/Admin/Shipping/GetWardsByProvinceV2", StringComparison.OrdinalIgnoreCase);

    if (context.User.Identity?.IsAuthenticated == true &&
        context.Request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase) &&
        !isPublicAdminEndpoint)
    {
        var returnUrl = Uri.EscapeDataString(
            $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");

        if (!context.User.CanAccessBackOffice())
        {
            context.Response.Redirect($"/Account/AccessDenied?returnUrl={returnUrl}");
            return;
        }

        var userManager = context.RequestServices.GetRequiredService<UserManager<AppUserModel>>();
        var currentUser = await userManager.GetUserAsync(context.User);

        if (currentUser != null && !currentUser.TwoFactorEnabled)
        {
            context.Response.Redirect($"/Account/SetupAuthenticator?returnUrl={returnUrl}");
            return;
        }
    }

    await next();
});

app.UseAuthorization();

var hangfireSettings = app.Services.GetRequiredService<IOptions<HangfireSettings>>().Value;

app.MapHangfireDashboard(hangfireSettings.DashboardPath, new DashboardOptions
{
    AppPath = "/Admin",
    DashboardTitle = "Eshop Job Dashboard",
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
});

// Routes
app.MapControllerRoute(
    name: "Areas",
    pattern: "{area:exists}/{controller=Portal}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "Category",
    pattern: "category/{Slug?}",
    defaults: new { controller = "Category", action = "Index" });

app.MapControllerRoute(
    name: "Publisher",
    pattern: "publisher/{Slug?}",
    defaults: new { controller = "Publisher", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<ChatHub>("/chatHub");
app.MapHub<NotificationHub>("/hubs/notification");

RecurringJob.AddOrUpdate<InventoryReservationCleanupJob>(
    recurringJobId: "inventory-reservation-cleanup",
    methodCall: job => job.RunAsync(),
    cronExpression: hangfireSettings.InventoryReservationCleanupCron);

if (!string.IsNullOrWhiteSpace(hangfireSettings.CatalogRecurringSyncCron))
{
    RecurringJob.AddOrUpdate<ProductCatalogRagSyncJob>(
        recurringJobId: "catalog-rag-sync",
        methodCall: job => job.RunFullSyncAsync(),
        cronExpression: hangfireSettings.CatalogRecurringSyncCron);
}
else
{
    RecurringJob.RemoveIfExists("catalog-rag-sync");
}

if (app.Configuration.GetValue<bool>("RagService:StartupFullSyncEnabled"))
{
    BackgroundJob.Enqueue<ProductCatalogRagSyncJob>(job => job.RunFullSyncAsync());
}

app.Run();

static void ConfigureLocalConfiguration(WebApplicationBuilder builder)
{
    var candidateDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        builder.Environment.ContentRootPath,
        AppContext.BaseDirectory
    };

    foreach (var directory in candidateDirectories.Where(Directory.Exists))
    {
        AddOptionalJsonFile(builder.Configuration, directory, "appsettings.Local.json");
        AddOptionalJsonFile(
            builder.Configuration,
            directory,
            $"appsettings.{builder.Environment.EnvironmentName}.local.json");
    }
}

static void AddOptionalJsonFile(
    ConfigurationManager configuration,
    string directory,
    string fileName)
{
    var fullPath = Path.Combine(directory, fileName);
    if (File.Exists(fullPath))
    {
        configuration.AddJsonFile(fullPath, optional: true, reloadOnChange: true);
    }
}
