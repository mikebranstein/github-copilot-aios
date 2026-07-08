namespace EquipmentTracker.Web.ViewModels;

/// <summary>Phase 1 gate metric view model (AC-6).</summary>
public class MaintenanceGateMetricViewModel
{
    /// <summary>Percentage of enrolled assets with at least one logged maintenance event in the past 90 days.</summary>
    public double CompletionRatePercent { get; set; }

    /// <summary>Phase 2 gate threshold (40%).</summary>
    public double GateThresholdPercent { get; set; } = 40.0;

    public bool GateMet => CompletionRatePercent >= GateThresholdPercent;

    public string GateStatusMessage => GateMet
        ? "Phase 2 ML Features are UNLOCKED — gate criterion met!"
        : $"Phase 2 ML Features unlock at 40% log completion. Currently at {CompletionRatePercent:F1}%.";
}
