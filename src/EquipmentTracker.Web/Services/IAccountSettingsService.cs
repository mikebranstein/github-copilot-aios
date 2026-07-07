using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

// Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout

public interface IAccountSettingsService
{
    /// <summary>Returns the current account-wide settings.</summary>
    AccountSettings GetSettings();

    /// <summary>Updates the approval timeout window (in minutes). Must be &gt; 0.</summary>
    void SetApprovalTimeout(int minutes);

    /// <summary>Sets the delegate/backup approver user ID. Pass null to clear.</summary>
    void SetDelegateApprover(int? userId);
}
