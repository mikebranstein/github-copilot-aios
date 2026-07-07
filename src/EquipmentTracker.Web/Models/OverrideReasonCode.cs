namespace EquipmentTracker.Web.Models;

/// <summary>Pre-defined reason codes a supervisor must select when overriding a blocked checkout.</summary>
public enum OverrideReasonCode
{
    EmergencyRenewalInProgress,
    OneTimeException,
    VerifiedExternally,
    TrainingInProgress,
    Other
}
