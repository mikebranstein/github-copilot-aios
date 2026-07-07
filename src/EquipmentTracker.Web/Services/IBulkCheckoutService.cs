using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IBulkCheckoutService
{
    /// <summary>
    /// Performs coordinator-initiated checkout of a single item to one crew member.
    /// Sets BulkCheckoutInitiatorId to initiatorCoordinatorId.
    /// Creates an ApprovalRequest that is immediately auto-approved (coordinator IS the authority).
    /// Returns the created CheckoutRecord, or null if checkout failed.
    /// </summary>
    CheckoutRecord? BulkCheckout(int itemId, int borrowerUserId, string borrowerName, int initiatorCoordinatorId, string? conditionNote = null);
}
