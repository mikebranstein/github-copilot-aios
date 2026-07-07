using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

// Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout

/// <summary>
/// In-memory singleton for account-wide approval workflow configuration.
/// Thread-safe via lock.
/// </summary>
public class AccountSettingsService : IAccountSettingsService
{
    private readonly AccountSettings _settings = new();
    private readonly object _lock = new();

    public AccountSettings GetSettings()
    {
        lock (_lock)
        {
            return new AccountSettings
            {
                ApprovalTimeoutMinutes = _settings.ApprovalTimeoutMinutes,
                DelegateApproverId = _settings.DelegateApproverId
            };
        }
    }

    public void SetApprovalTimeout(int minutes)
    {
        if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes), "Timeout must be greater than zero.");
        lock (_lock)
        {
            _settings.ApprovalTimeoutMinutes = minutes;
        }
    }

    public void SetDelegateApprover(int? userId)
    {
        lock (_lock)
        {
            _settings.DelegateApproverId = userId;
        }
    }
}
