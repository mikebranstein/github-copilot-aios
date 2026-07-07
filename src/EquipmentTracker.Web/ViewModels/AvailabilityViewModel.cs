using EquipmentTracker.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using ModelStatus = EquipmentTracker.Web.Models.EquipmentStatus;

namespace EquipmentTracker.Web.ViewModels;

public class AvailabilityItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public ModelStatus Status { get; init; }
    public string StatusDisplay => Status switch
    {
        ModelStatus.Available => "Available",
        ModelStatus.InUse => "In Use",
        ModelStatus.Reserved => "Reserved",
        ModelStatus.Maintenance => "Maintenance",
        _ => Status.ToString()
    };
    public string StatusBadgeClass => Status switch
    {
        ModelStatus.Available => "bg-success",
        ModelStatus.InUse => "bg-warning text-dark",
        ModelStatus.Reserved => "bg-info text-dark",
        ModelStatus.Maintenance => "bg-secondary",
        _ => "bg-light text-dark"
    };
    public string? SiteName { get; init; }
    public DateTime LastUpdatedAtUtc { get; init; }
}

public class AvailabilityViewModel
{
    public IReadOnlyList<AvailabilityItemViewModel> Items { get; init; } = [];
    public int? SelectedSiteId { get; init; }
    public string? SelectedStatus { get; init; }
    public IReadOnlyList<SelectListItem> SiteOptions { get; init; } = [];
    public IReadOnlyList<SelectListItem> StatusOptions { get; init; } =
    [
        new SelectListItem("All Statuses", ""),
        new SelectListItem("Available", "Available"),
        new SelectListItem("In Use", "InUse"),
        new SelectListItem("Reserved", "Reserved"),
        new SelectListItem("Maintenance", "Maintenance"),
    ];
}
