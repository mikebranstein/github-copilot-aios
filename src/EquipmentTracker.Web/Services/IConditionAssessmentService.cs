using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Contract for the condition assessment and damage tracking workflow.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public interface IConditionAssessmentService
{
    // ─── Condition Record ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates an immutable condition record for the completed return.
    /// Server-side timestamp is applied here, not from the device.
    /// </summary>
    ConditionRecord CreateConditionRecord(
        int checkoutRecordId,
        int equipmentItemId,
        string equipmentName,
        int? operatorUserId,
        string operatorName,
        ConditionGrade grade,
        bool isOffline = false);

    /// <summary>Returns the condition record for the specified checkout, or null if none.</summary>
    ConditionRecord? GetConditionRecord(int checkoutRecordId);

    /// <summary>
    /// Returns all condition records for a given equipment item, reverse chronological order.
    /// Used for the dispute-resolution condition history view (AC9).
    /// </summary>
    IReadOnlyList<ConditionRecord> GetConditionHistory(int equipmentItemId);

    // ─── Photos ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a photo to an existing ConditionRecord.
    /// Maximum 3 photos per record; each up to 10 MB (enforced by caller).
    /// </summary>
    ConditionPhoto AttachPhoto(int conditionRecordId, string fileUrl, PhotoUploadStatus status = PhotoUploadStatus.Complete);

    /// <summary>Returns all photos for the specified condition record.</summary>
    IReadOnlyList<ConditionPhoto> GetPhotos(int conditionRecordId);

    /// <summary>Marks an offline-queued photo as uploaded (transitions PENDING → COMPLETE).</summary>
    bool MarkPhotoUploaded(int conditionPhotoId, string finalFileUrl);

    /// <summary>Increments retry count for a failed photo upload. Returns false if cap (3) is exceeded.</summary>
    bool IncrementPhotoRetry(int conditionPhotoId);

    // ─── Maintenance Ticket Draft ────────────────────────────────────────────────

    /// <summary>
    /// Auto-creates a maintenance ticket draft for a Damaged return.
    /// If maintenance module is unavailable, state is set to HoldingQueue.
    /// </summary>
    MaintenanceTicketDraft CreateMaintenanceTicketDraft(
        int conditionRecordId,
        int equipmentItemId,
        string equipmentName,
        int? reportedByUserId,
        string reportedByName,
        DateTime reportedAtUtc,
        List<int> photoIds);

    /// <summary>Returns the maintenance ticket draft for the given condition record, or null.</summary>
    MaintenanceTicketDraft? GetMaintenanceTicketDraft(int conditionRecordId);

    /// <summary>Returns all maintenance ticket drafts (for coordinator dashboard).</summary>
    IReadOnlyList<MaintenanceTicketDraft> GetAllMaintenanceTicketDrafts();

    /// <summary>Marks the coordinator email notification as sent.</summary>
    bool MarkMaintenanceNotificationSent(int draftId, DateTime sentAtUtc);

    // ─── Lost Equipment ──────────────────────────────────────────────────────────

    /// <summary>
    /// Flags equipment as Lost and updates its status.
    /// Equipment is NOT reactivated to Available on this path — only an explicit admin action can do that.
    /// </summary>
    LostEquipmentFlag FlagEquipmentLost(
        int equipmentItemId,
        int conditionRecordId,
        int? flaggedByUserId,
        string flaggedByName);

    /// <summary>Returns the current lost flag for the equipment item, or null if not lost.</summary>
    LostEquipmentFlag? GetLostFlag(int equipmentItemId);

    /// <summary>
    /// Admin-only: reactivates a Lost equipment item to Available.
    /// Records who performed the reactivation and when.
    /// </summary>
    bool ReactivateLostEquipment(int equipmentItemId, int adminUserId);

    // ─── Scheduling Conflict ─────────────────────────────────────────────────────

    /// <summary>
    /// Detects future reservations that conflict with the Damaged/Lost equipment item
    /// and creates a SchedulingConflictAlert if any are found.
    /// </summary>
    SchedulingConflictAlert? DetectAndCreateConflictAlert(
        int conditionRecordId,
        int equipmentItemId,
        string alertedTo);

    /// <summary>Returns the scheduling conflict alert for the given condition record, or null.</summary>
    SchedulingConflictAlert? GetConflictAlert(int conditionRecordId);

    // ─── Reservations (lightweight model for conflict detection) ─────────────────

    /// <summary>Adds a future reservation for an equipment item.</summary>
    EquipmentReservation AddReservation(int equipmentItemId, string operatorName, string siteName,
        DateTime startUtc, DateTime? endUtc = null);

    /// <summary>Returns all active (non-cancelled) future reservations for the equipment item.</summary>
    IReadOnlyList<EquipmentReservation> GetFutureReservations(int equipmentItemId);
}
