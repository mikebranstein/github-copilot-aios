using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of IConditionAssessmentService.
/// Enforces application-level immutability for condition records (no update/delete).
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public class ConditionAssessmentService : IConditionAssessmentService
{
    private readonly IEquipmentService _equipmentService;

    private readonly List<ConditionRecord> _conditionRecords = new();
    private readonly List<ConditionPhoto> _photos = new();
    private readonly List<MaintenanceTicketDraft> _ticketDrafts = new();
    private readonly List<LostEquipmentFlag> _lostFlags = new();
    private readonly List<SchedulingConflictAlert> _conflictAlerts = new();
    private readonly List<EquipmentReservation> _reservations = new();

    private int _nextConditionRecordId = 1;
    private int _nextPhotoId = 1;
    private int _nextTicketDraftId = 1;
    private int _nextLostFlagId = 1;
    private int _nextConflictAlertId = 1;
    private int _nextReservationId = 1;

    private const int MaxPhotosPerReturn = 3;
    private const int MaxPhotoRetries = 3;

    public ConditionAssessmentService(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    // ─── Condition Record ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ConditionRecord CreateConditionRecord(
        int checkoutRecordId,
        int equipmentItemId,
        string equipmentName,
        int? operatorUserId,
        string operatorName,
        ConditionGrade grade,
        bool isOffline = false)
    {
        var record = new ConditionRecord
        {
            Id = _nextConditionRecordId++,
            CheckoutRecordId = checkoutRecordId,
            EquipmentItemId = equipmentItemId,
            EquipmentName = equipmentName,
            OperatorUserId = operatorUserId,
            OperatorName = operatorName,
            Grade = grade,
            // Server-side timestamp: always use UTC now, never device time
            ServerTimestampUtc = DateTime.UtcNow,
            SyncStatus = isOffline ? ReturnState.PendingPhotoSync : ReturnState.Complete
        };

        _conditionRecords.Add(record);
        return record;
    }

    /// <inheritdoc/>
    public ConditionRecord? GetConditionRecord(int checkoutRecordId) =>
        _conditionRecords.FirstOrDefault(r => r.CheckoutRecordId == checkoutRecordId);

    /// <inheritdoc/>
    public IReadOnlyList<ConditionRecord> GetConditionHistory(int equipmentItemId) =>
        _conditionRecords
            .Where(r => r.EquipmentItemId == equipmentItemId)
            .OrderByDescending(r => r.ServerTimestampUtc)
            .ToList()
            .AsReadOnly();

    // ─── Photos ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ConditionPhoto AttachPhoto(int conditionRecordId, string fileUrl,
        PhotoUploadStatus status = PhotoUploadStatus.Complete)
    {
        var existing = _photos.Count(p => p.ConditionRecordId == conditionRecordId);
        if (existing >= MaxPhotosPerReturn)
            throw new InvalidOperationException(
                $"Maximum of {MaxPhotosPerReturn} photos allowed per return.");

        var photo = new ConditionPhoto
        {
            Id = _nextPhotoId++,
            ConditionRecordId = conditionRecordId,
            FileUrl = fileUrl,
            ServerTimestampUtc = DateTime.UtcNow,
            UploadStatus = status,
            SyncQueuedAt = status == PhotoUploadStatus.Pending ? DateTime.UtcNow : null
        };

        _photos.Add(photo);

        // Attach to the navigation collection on the record
        var record = _conditionRecords.FirstOrDefault(r => r.Id == conditionRecordId);
        record?.Photos.Add(photo);

        return photo;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ConditionPhoto> GetPhotos(int conditionRecordId) =>
        _photos
            .Where(p => p.ConditionRecordId == conditionRecordId)
            .ToList()
            .AsReadOnly();

    /// <inheritdoc/>
    public bool MarkPhotoUploaded(int conditionPhotoId, string finalFileUrl)
    {
        var photo = _photos.FirstOrDefault(p => p.Id == conditionPhotoId);
        if (photo is null) return false;

        photo.FileUrl = finalFileUrl;
        photo.UploadStatus = PhotoUploadStatus.Complete;
        photo.SyncCompletedAt = DateTime.UtcNow;

        // If all photos on the condition record are complete, update sync status
        var record = _conditionRecords.FirstOrDefault(r => r.Id == photo.ConditionRecordId);
        if (record is not null)
        {
            var allPhotos = _photos.Where(p => p.ConditionRecordId == record.Id).ToList();
            if (allPhotos.All(p => p.UploadStatus == PhotoUploadStatus.Complete))
                record.SyncStatus = ReturnState.Complete;
        }

        return true;
    }

    /// <inheritdoc/>
    public bool IncrementPhotoRetry(int conditionPhotoId)
    {
        var photo = _photos.FirstOrDefault(p => p.Id == conditionPhotoId);
        if (photo is null) return false;
        if (photo.RetryCount >= MaxPhotoRetries) return false;

        photo.RetryCount++;
        return true;
    }

    // ─── Maintenance Ticket Draft ─────────────────────────────────────────────

    /// <inheritdoc/>
    public MaintenanceTicketDraft CreateMaintenanceTicketDraft(
        int conditionRecordId,
        int equipmentItemId,
        string equipmentName,
        int? reportedByUserId,
        string reportedByName,
        DateTime reportedAtUtc,
        List<int> photoIds)
    {
        // If maintenance module is considered unavailable (heuristic: always available in this in-memory impl),
        // place in HoldingQueue. For now we always use Draft state.
        var draft = new MaintenanceTicketDraft
        {
            Id = _nextTicketDraftId++,
            ConditionRecordId = conditionRecordId,
            EquipmentItemId = equipmentItemId,
            EquipmentName = equipmentName,
            ConditionGrade = ConditionGrade.Damaged,
            ReportedByUserId = reportedByUserId,
            ReportedByName = reportedByName,
            ReportedAtUtc = reportedAtUtc,
            PhotoIds = photoIds,
            State = MaintenanceTicketState.Draft,
            NotificationSent = false
        };

        _ticketDrafts.Add(draft);

        // Wire up navigation
        var record = _conditionRecords.FirstOrDefault(r => r.Id == conditionRecordId);
        if (record is not null)
            record.MaintenanceTicketDraft = draft;

        return draft;
    }

    /// <inheritdoc/>
    public MaintenanceTicketDraft? GetMaintenanceTicketDraft(int conditionRecordId) =>
        _ticketDrafts.FirstOrDefault(d => d.ConditionRecordId == conditionRecordId);

    /// <inheritdoc/>
    public IReadOnlyList<MaintenanceTicketDraft> GetAllMaintenanceTicketDrafts() =>
        _ticketDrafts.OrderByDescending(d => d.ReportedAtUtc).ToList().AsReadOnly();

    /// <inheritdoc/>
    public bool MarkMaintenanceNotificationSent(int draftId, DateTime sentAtUtc)
    {
        var draft = _ticketDrafts.FirstOrDefault(d => d.Id == draftId);
        if (draft is null) return false;

        draft.NotificationSent = true;
        draft.NotificationSentAtUtc = sentAtUtc;
        return true;
    }

    // ─── Lost Equipment ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public LostEquipmentFlag FlagEquipmentLost(
        int equipmentItemId,
        int conditionRecordId,
        int? flaggedByUserId,
        string flaggedByName)
    {
        var flag = new LostEquipmentFlag
        {
            Id = _nextLostFlagId++,
            EquipmentItemId = equipmentItemId,
            ConditionRecordId = conditionRecordId,
            FlaggedByUserId = flaggedByUserId,
            FlaggedByName = flaggedByName,
            FlaggedAtUtc = DateTime.UtcNow,
            NotificationSent = false
        };

        _lostFlags.Add(flag);

        // Update equipment status to Lost — NOT Available
        var item = _equipmentService.GetItem(equipmentItemId);
        if (item is not null)
        {
            item.Status = EquipmentLifecycleStatus.Lost;
            // IsAvailable remains false — item is returned physically but flagged as lost (not available for re-checkout)
            item.IsAvailable = false;
        }

        // Wire up navigation
        var record = _conditionRecords.FirstOrDefault(r => r.Id == conditionRecordId);
        if (record is not null)
            record.LostEquipmentFlag = flag;

        return flag;
    }

    /// <inheritdoc/>
    public LostEquipmentFlag? GetLostFlag(int equipmentItemId) =>
        _lostFlags
            .Where(f => f.EquipmentItemId == equipmentItemId && f.ReactivatedAtUtc is null)
            .OrderByDescending(f => f.FlaggedAtUtc)
            .FirstOrDefault();

    /// <inheritdoc/>
    public bool ReactivateLostEquipment(int equipmentItemId, int adminUserId)
    {
        var flag = GetLostFlag(equipmentItemId);
        if (flag is null) return false;

        flag.ReactivatedByUserId = adminUserId;
        flag.ReactivatedAtUtc = DateTime.UtcNow;

        var item = _equipmentService.GetItem(equipmentItemId);
        if (item is not null)
        {
            item.Status = EquipmentLifecycleStatus.Available;
            item.IsAvailable = true;
        }

        return true;
    }

    // ─── Scheduling Conflict ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public SchedulingConflictAlert? DetectAndCreateConflictAlert(
        int conditionRecordId,
        int equipmentItemId,
        string alertedTo)
    {
        var futureReservations = GetFutureReservations(equipmentItemId);
        if (!futureReservations.Any()) return null;

        var conflictDetails = futureReservations
            .Select(r => new ReservationConflictDetail
            {
                ReservationId = r.Id,
                OperatorName = r.OperatorName,
                SiteName = r.SiteName,
                ReservationStartUtc = r.ReservationStartUtc,
                ReservationEndUtc = r.ReservationEndUtc
            })
            .ToList();

        var alert = new SchedulingConflictAlert
        {
            Id = _nextConflictAlertId++,
            ConditionRecordId = conditionRecordId,
            EquipmentItemId = equipmentItemId,
            ConflictingReservationIds = futureReservations.Select(r => r.Id).ToList(),
            ConflictDetails = conflictDetails,
            AlertedTo = alertedTo,
            AlertedAtUtc = DateTime.UtcNow,
            IsAcknowledged = false
        };

        _conflictAlerts.Add(alert);

        var record = _conditionRecords.FirstOrDefault(r => r.Id == conditionRecordId);
        if (record is not null)
            record.SchedulingConflictAlert = alert;

        return alert;
    }

    /// <inheritdoc/>
    public SchedulingConflictAlert? GetConflictAlert(int conditionRecordId) =>
        _conflictAlerts.FirstOrDefault(a => a.ConditionRecordId == conditionRecordId);

    // ─── Reservations ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public EquipmentReservation AddReservation(int equipmentItemId, string operatorName, string siteName,
        DateTime startUtc, DateTime? endUtc = null)
    {
        var reservation = new EquipmentReservation
        {
            Id = _nextReservationId++,
            EquipmentItemId = equipmentItemId,
            OperatorName = operatorName,
            SiteName = siteName,
            ReservationStartUtc = startUtc,
            ReservationEndUtc = endUtc,
            IsCancelled = false
        };

        _reservations.Add(reservation);
        return reservation;
    }

    /// <inheritdoc/>
    public IReadOnlyList<EquipmentReservation> GetFutureReservations(int equipmentItemId) =>
        _reservations
            .Where(r => r.EquipmentItemId == equipmentItemId
                     && !r.IsCancelled
                     && r.ReservationStartUtc > DateTime.UtcNow)
            .OrderBy(r => r.ReservationStartUtc)
            .ToList()
            .AsReadOnly();
}
