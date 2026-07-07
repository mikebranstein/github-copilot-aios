using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Equipment service (existing)
builder.Services.AddSingleton<IEquipmentService, EquipmentService>();

// Auth and user services
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IPushNotificationService, WebPushService>();

// Coordinator notification service (in-memory, singleton)
builder.Services.AddSingleton<ICoordinatorNotificationService, CoordinatorNotificationService>();

// Approval, audit, and bulk checkout services (Issue #40)
builder.Services.AddSingleton<IApprovalService, ApprovalService>();
builder.Services.AddSingleton<IAuditExportService, AuditExportService>();
builder.Services.AddSingleton<IBulkCheckoutService, BulkCheckoutService>();

// Background notification job
builder.Services.AddHostedService<OverdueNotificationJob>();

// Cookie authentication with 7-day sliding session
var sessionExpiryDays = builder.Configuration.GetValue<int>("Auth:SessionExpiryDays", 7);
if (sessionExpiryDays <= 0) sessionExpiryDays = 7;

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(sessionExpiryDays);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
