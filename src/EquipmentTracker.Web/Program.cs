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

// Account settings service (Issue #117 - Approval Workflow for Restricted Equipment Checkout)
builder.Services.AddSingleton<IAccountSettingsService, AccountSettingsService>();

// Offline sync service (Issue #41)
builder.Services.AddSingleton<IOfflineSyncService, OfflineSyncService>();
builder.Services.AddSingleton<ICertificationService, CertificationService>();

builder.Services.AddSingleton<IWaitlistService, WaitlistService>();
builder.Services.AddHostedService<QueueConfirmationExpiryJob>();

// Reservation & scheduling calendar (Issue #123)
builder.Services.AddSingleton<IReservationNotificationService, ReservationNotificationService>();
builder.Services.AddSingleton<IReservationService, ReservationService>();

// Photo-backed checkout & return (Issue #58)
builder.Services.AddSingleton<ICameraService, MobileCameraService>();
builder.Services.AddSingleton<IPhotoStorageService, LocalPhotoStorageService>();
builder.Services.AddSingleton<IPhotoSyncService, PhotoSyncService>();

builder.Services.AddHostedService<OverdueNotificationJob>();
builder.Services.AddSingleton<IUtilizationService, UtilizationService>();
builder.Services.AddSingleton<IRentalCostService, RentalCostService>();
builder.Services.AddSingleton<IBuyRentRecommendationService, BuyRentRecommendationService>();
builder.Services.AddSingleton<ICfoReportService, CfoReportService>();

// Availability Dashboard — Issue #118
builder.Services.AddSingleton<ISoftHoldService, SoftHoldService>();
builder.Services.AddSingleton<IAvailabilityDashboardService, AvailabilityDashboardService>();
builder.Services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
builder.Services.AddSingleton<ISmsService, StubSmsService>();
builder.Services.AddSingleton<INotifyMeService, NotifyMeService>();
builder.Services.AddHostedService<SoftHoldExpiryJob>();

// Smart Maintenance Scheduling — Issue #119 (Phase 1, rule-based only, no ML)
builder.Services.AddSingleton<IMaintenanceService, MaintenanceService>();

// NL Checkout — Issue #149 (Phase 1: text-based NL interface; LLM provider TBD from spike #148)
builder.Services.AddSingleton<INaturalLanguageCheckoutService, NaturalLanguageCheckoutService>();

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

builder.Services.AddSingleton<IConditionAssessmentService, ConditionAssessmentService>();

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
