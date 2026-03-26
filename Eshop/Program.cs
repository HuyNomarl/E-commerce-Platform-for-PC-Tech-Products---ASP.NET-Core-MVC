using CloudinaryDotNet;
using Eshop.Areas.Admin.Repository;
using Eshop.Constants;
using Eshop.Hubs;
using Eshop.Helpers;
using Eshop.Models;
using Eshop.Models.Configurations;
using Eshop.Models.Momo;
using Eshop.Repository;
using Eshop.Services;
using Eshop.Services.Momo;
using Eshop.Services.VNPay;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// DB
builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
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
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IMomoService, MomoService>();
builder.Services.AddScoped<ICatalogCacheService, CatalogCacheService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<RecommendationTrainingService>();
builder.Services.AddScoped<RecommendationPredictService>();
builder.Services.AddScoped<IBuildRequirementExtractor, BuildRequirementExtractor>();
builder.Services.AddScoped<IPcBuildRecommendationService, PcBuildRecommendationService>();
builder.Services.AddScoped<IPcBuildSuggestionService, PcBuildSuggestionService>();
builder.Services.AddScoped<IPcBuildChatService, PcBuildChatService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IOrderStateService, OrderStateService>();
builder.Services.AddHostedService<InventoryReservationCleanupHostedService>();


builder.Services.Configure<MomoOptionModel>(
    builder.Configuration.GetSection("MomoAPI")
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
    });

// Cache + Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

//RAG Client
builder.Services.AddHttpClient<ILlmChatClient, LlmChatClient>();
builder.Services.AddScoped<IPcBuildChatService, PcBuildChatService>();
builder.Services.AddHttpClient<RagClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8001");
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
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["GoogleKeys:ClientId"];
        options.ClientSecret = builder.Configuration["GoogleKeys:ClientSecret"];
    });

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
        !isPublicAdminEndpoint &&
        !context.User.CanAccessBackOffice())
    {
        var returnUrl = Uri.EscapeDataString(
            $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");

        context.Response.Redirect($"/Account/AccessDenied?returnUrl={returnUrl}");
        return;
    }

    await next();
});

app.UseAuthorization();

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

app.Run();
