using System.Security.Claims;
using EquipmentTracker.Web.Controllers;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;

namespace EquipmentTracker.Web.Tests;

public class LocationEquipmentTests
{
    [Fact]
    public void AC1_Checkout_CanAssignNamedSite_AndPersistsOnItem()
    {
        var equipment = new EquipmentService();
        var sites = new SiteService();
        var site = sites.CreateSite("Auckland Yard");
        Assert.NotNull(site);

        var item = equipment.GetAllItems().First();

        var result = equipment.Checkout(item.Id, "Alice", newSiteId: site!.Id);

        Assert.True(result);
        Assert.Equal(site.Id, equipment.GetItem(item.Id)!.SiteId);
        Assert.Equal(EquipmentTracker.Web.Models.EquipmentStatus.InUse, equipment.GetItem(item.Id)!.Status);
    }

    [Fact]
    public void AC2_StatusField_ExposesExactlyFourValues_AndAutoTransitions()
    {
        var names = Enum.GetNames<EquipmentTracker.Web.Models.EquipmentStatus>();
        Assert.Equal(["Available", "InUse", "Reserved", "Maintenance"], names);

        var equipment = new EquipmentService();
        var item = equipment.GetAllItems().First();
        var before = item.LastUpdatedAtUtc;

        Assert.True(equipment.Checkout(item.Id, "Alice"));
        Assert.Equal(EquipmentTracker.Web.Models.EquipmentStatus.InUse, equipment.GetItem(item.Id)!.Status);

        Assert.True(equipment.Return(item.Id));
        var updated = equipment.GetItem(item.Id)!;
        Assert.Equal(EquipmentTracker.Web.Models.EquipmentStatus.Available, updated.Status);
        Assert.True(updated.LastUpdatedAtUtc >= before);
    }

    [Fact]
    public void AC3_And_AC4_AvailabilityView_FiltersBySiteAndStatus_AndShowsLastUpdated()
    {
        var equipment = new EquipmentService();
        var sites = new SiteService();
        var north = sites.CreateSite("North Depot");
        var south = sites.CreateSite("South Depot");
        Assert.NotNull(north);
        Assert.NotNull(south);

        var items = equipment.GetAllItems().ToList();
        equipment.UpdateItemSite(items[0].Id, north!.Id);
        equipment.UpdateItemSite(items[1].Id, north.Id);
        equipment.UpdateItemSite(items[2].Id, south!.Id);
        equipment.UpdateItemStatus(items[0].Id, EquipmentTracker.Web.Models.EquipmentStatus.Available);
        equipment.UpdateItemStatus(items[1].Id, EquipmentTracker.Web.Models.EquipmentStatus.Maintenance);
        equipment.UpdateItemStatus(items[2].Id, EquipmentTracker.Web.Models.EquipmentStatus.Available);

        var controller = new AvailabilityController(equipment, sites);

        var result = controller.Index(north.Id, "Available");
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AvailabilityViewModel>(view.Model);

        var onlyItem = Assert.Single(model.Items);
        Assert.Equal(items[0].Id, onlyItem.Id);
        Assert.Equal("North Depot", onlyItem.SiteName);
        Assert.Equal(EquipmentTracker.Web.Models.EquipmentStatus.Available, onlyItem.Status);
        Assert.True(onlyItem.LastUpdatedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void AC5_AvailabilityView_UsesMobileFriendlyResponsiveMarkup()
    {
        var repoRoot = GetRepoRoot();
        var viewPath = Path.Combine(repoRoot, "src", "EquipmentTracker.Web", "Views", "Availability", "Index.cshtml");
        var layoutPath = Path.Combine(repoRoot, "src", "EquipmentTracker.Web", "Views", "Shared", "_Layout.cshtml");

        var viewContent = File.ReadAllText(viewPath);
        var layoutContent = File.ReadAllText(layoutPath);

        Assert.Contains("d-md-none", viewContent);
        Assert.Contains("table-responsive", viewContent);
        Assert.Contains("col-12", viewContent);
        Assert.Contains("width=device-width, initial-scale=1.0", layoutContent);
    }

    [Fact]
    public void AC6_CheckoutPrompt_IncludesCurrentSiteAndActiveSiteDropdown()
    {
        var equipment = new EquipmentService();
        var sites = new SiteService();
        var activeSite = sites.CreateSite("Hamilton");
        var inactiveSite = sites.CreateSite("Old Yard");
        Assert.NotNull(activeSite);
        Assert.NotNull(inactiveSite);
        Assert.True(sites.DeactivateSite(inactiveSite!.Id));

        var item = equipment.GetAllItems().First();
        Assert.True(equipment.UpdateItemSite(item.Id, inactiveSite.Id));

        var controller = new EquipmentController(equipment, sites, BuildConfiguration());

        var result = controller.Checkout(item.Id);
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CheckoutViewModel>(view.Model);

        Assert.Equal("Old Yard", model.CurrentSiteName);
        Assert.Contains(model.SiteOptions, option => option.Text == "Hamilton");
        Assert.DoesNotContain(model.SiteOptions, option => option.Text == "Old Yard");
    }

    [Fact]
    public void AC7_AdminSiteManagement_CanCreateRenameDeactivateAndHideInactiveSitesFromDropdowns()
    {
        var siteService = new SiteService();
        var controller = BuildSettingsController(siteService);

        var createResult = controller.CreateSite(new CreateSiteViewModel { Name = "Rotorua" });
        Assert.IsType<RedirectToActionResult>(createResult);

        var site = Assert.Single(siteService.GetAllSites());
        Assert.Equal("Rotorua", site.Name);

        var renameResult = controller.RenameSite(new RenameSiteViewModel { SiteId = site.Id, NewName = "Rotorua Hub" });
        Assert.IsType<RedirectToActionResult>(renameResult);
        Assert.Equal("Rotorua Hub", siteService.GetSite(site.Id)!.Name);

        var deactivateResult = controller.DeactivateSite(site.Id);
        Assert.IsType<RedirectToActionResult>(deactivateResult);
        Assert.False(siteService.GetSite(site.Id)!.IsActive);
        Assert.Empty(siteService.GetActiveSites());
    }

    [Fact]
    public void SiteService_EnforcesMaximumOfFiftySites()
    {
        var service = new SiteService();
        for (var i = 1; i <= 50; i++)
        {
            Assert.NotNull(service.CreateSite($"Site {i}"));
        }

        Assert.Null(service.CreateSite("Site 51"));
    }

    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Checkout:OverdueThresholdDays"] = "7"
        })
        .Build();

    private static SettingsController BuildSettingsController(ISiteService siteService)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "coord"),
                new Claim("IsCoordinator", bool.TrueString)
            ], "TestAuth"))
        };

        var controller = new SettingsController(new UserService(), siteService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };

        return controller;
    }

    private static string GetRepoRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
