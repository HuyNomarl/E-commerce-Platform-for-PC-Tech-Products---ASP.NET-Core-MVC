using CloudinaryDotNet;
using Eshop.Areas.Admin.Repository;
using Eshop.Hubs;
using Eshop.Models;
using Eshop.Models.Configurations;
using Eshop.Models.Momo;
using Eshop.Repository;
using Eshop.Services;
using Eshop.Services.Momo;
using Eshop.Services.VNPay;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Connect SQL Server
builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Services
builder.Services.AddScoped<IPcCompatibilityService, PcCompatibilityService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IMomoService, MomoService>();
builder.Services.AddScoped<ICatalogCacheService, CatalogCacheService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

builder.Services.Configure<MomoOptionModel>(
    builder.Configuration.GetSection("MomoAPI")
);

// MVC + JSON
builder.Services
    .AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Cache + Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

//Cloudinary
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
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<DataContext>()
.AddDefaultTokenProviders();

// Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
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
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Route area
app.MapControllerRoute(
    name: "Areas",
    pattern: "{area:exists}/{controller=Product}/{action=Index}/{id?}");

// Route category
app.MapControllerRoute(
    name: "Category",
    pattern: "category/{Slug?}",
    defaults: new { controller = "Category", action = "Index" });

// Route publisher
app.MapControllerRoute(
    name: "Publisher",
    pattern: "publisher/{Slug?}",
    defaults: new { controller = "Publisher", action = "Index" });

// Default
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SignalR Hubs
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<ChatHub>("/chatHub");    
app.MapHub<NotificationHub>("/hubs/notification");

app.Run();