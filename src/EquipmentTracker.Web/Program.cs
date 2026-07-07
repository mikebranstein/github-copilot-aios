using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IEquipmentService, EquipmentService>();
builder.Services.AddSingleton<ISiteService, SiteService>();

builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IPushNotificationService, WebPushService>();

builder.Services.AddSingleton<ICoordinatorNotificationService, CoordinatorNotificationService>();

builder.Services.AddSingleton<IApprovalService, ApprovalService>();
builder.Services.AddSingleton<IAuditExportService, AuditExportService>();
builder.Services.AddSingleton<IBulkCheckoutService, BulkCheckoutService>();
builder.Services.AddSingleton<IFieldBulkCheckoutService, FieldBulkCheckoutService>();
builder.Services.AddSingleton<IOfflineSyncService, OfflineSyncService>();
builder.Services.AddSingleton<ICertificationService, CertificationService>();

builder.Services.AddSingleton<IWaitlistService, WaitlistService>();
builder.Services.AddHostedService<QueueConfirmationExpiryJob>();

builder.Services.AddSingleton<ICameraService, MobileCameraService>();
builder.Services.AddSingleton<IPhotoStorageService, LocalPhotoStorageService>();
builder.Services.AddSingleton<IPhotoSyncService, PhotoSyncService>();

builder.Services.AddHostedService<OverdueNotificationJob>();
builder.Services.AddSingleton<IUtilizationService, UtilizationService>();
builder.Services.AddSingleton<IRentalCostService, RentalCostService>();
builder.Services.AddSingleton<IBuyRentRecommendationService, BuyRentRecommendationService>();
builder.Services.AddSingleton<ICfoReportService, CfoReportService>();
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
