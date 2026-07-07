using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>Admin service interval configuration view model (AC-3).</summary>
public class ServiceIntervalAdminViewModel
{
    public IReadOnlyList<ServiceInterval> Intervals { get; set; } = Array.Empty<ServiceInterval>();
}

/// <summary>Form model for creating/editing a service interval.</summary>
public class UpsertServiceIntervalViewModel
{
    public string Category { get; set; } = string.Empty;
    public IntervalType IntervalType { get; set; } = IntervalType.Hours;
    public double IntervalValue { get; set; }
    public int LeadTimeDays { get; set; } = 14;
}
