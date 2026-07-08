using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Manages Notify Me subscriptions and fires availability alerts.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public interface INotifyMeService
{
    /// <summary>
    /// Subscribes a user to be notified when a specific item becomes compound-available.
    /// If a matching active subscription already exists, returns the existing subscription (idempotent).
    /// </summary>
    Task<NotifyMeSubscription> SubscribeToItemAsync(int userId, int equipmentItemId);

    /// <summary>
    /// Subscribes a user to be notified when any item in the given category becomes compound-available.
    /// </summary>
    Task<NotifyMeSubscription> SubscribeToCategoryAsync(int userId, string category);

    /// <summary>
    /// Cancels an active subscription. Only the owning user may cancel.
    /// Returns false if the subscription was not found or not owned by this user.
    /// </summary>
    Task<bool> CancelSubscriptionAsync(int subscriptionId, int userId);

    /// <summary>
    /// Returns all active subscriptions for the given user.
    /// </summary>
    IReadOnlyList<NotifyMeSubscription> GetActiveSubscriptionsForUser(int userId);

    /// <summary>
    /// Evaluates and fires any pending notifications triggered by a specific item
    /// becoming compound-available. Sends push notification first; falls back to SMS
    /// if push delivery cannot be confirmed (per BA constraint).
    /// Called whenever an availability-relevant event occurs (checkout return, condition change,
    /// soft hold expiry, location assignment).
    /// </summary>
    Task FireAvailabilityAlertsAsync(int equipmentItemId);
}
