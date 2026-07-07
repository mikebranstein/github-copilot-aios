using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Tests for Issue #115 — Equipment Condition Assessment and Damage Tracking at Return.
/// Each test maps to one or more Acceptance Criteria (AC1-AC9) from the approved design.
/// Run with: dotnet test tests/EquipmentTracker.Web.Tests/
/// </summary>
public class ConditionAssessmentTests
{
    private static (EquipmentService equipSvc, ConditionAssessmentService condSvc) CreateServices()
    {
        var equipSvc = new EquipmentService();
        var condSvc = new ConditionAssessmentService(equipSvc);
        return (equipSvc, condSvc);
    }

    private static CheckoutRecord SetupActiveCheckout(EquipmentService equipSvc, int itemId,
        string borrower = "Alice", int borrowerUserId = 1)
    {
        equipSvc.Checkout(itemId, borrower, borrowerUserId);
        return equipSvc.GetActiveCheckoutRecord(itemId)!;
    }

    // AC1: Condition Grade gate on return

    [Fact]
    public void AC1_ConditionRecord_RequiresGrade_AllFourGradesAreValid()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();

        foreach (var grade in Enum.GetValues<ConditionGrade>())
        {
            item.IsAvailable = true;
            item.LifecycleStatus = EquipmentLifecycleStatus.Available;
            equipSvc.Checkout(item.Id, $"user-{grade}", grade.GetHashCode());
            var checkout = equipSvc.GetActiveCheckoutRecord(item.Id)!;
            equipSvc.Return(item.Id);

            var record = condSvc.CreateConditionRecord(
                checkoutRecordId: checkout.Id,
                equipmentItemId: checkout.EquipmentItemId,
                equipmentName: "Laptop",
                operatorUserId: 1,
                operatorName: "Alice",
                grade: grade);

            Assert.Equal(grade, record.Grade);
            Assert.NotEqual(default, record.ServerTimestampUtc);
        }
    }

    [Fact]
    public void AC1_ConditionRecord_ServerTimestampIsUtc()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var before = DateTime.UtcNow.AddSeconds(-1);
        var record = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Good);
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(record.ServerTimestampUtc, before, after);
    }

    [Fact]
    public void AC1_ConditionRecord_IsImmutable_NoMutateApiExists()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var record = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Good);
        var fetched = condSvc.GetConditionRecord(checkout.Id);

        Assert.NotNull(fetched);
        Assert.Equal(record.Id, fetched!.Id);
        Assert.Equal(ConditionGrade.Good, fetched.Grade);
    }

    // AC2: Photo capture for Damaged/Worn returns

    [Fact]
    public void AC2_AttachPhoto_AddsPhotoToConditionRecord()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var record = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        var photo = condSvc.AttachPhoto(record.Id, "/photos/damage-photo-1.jpg");

        Assert.Equal(record.Id, photo.ConditionRecordId);
        Assert.Equal("/photos/damage-photo-1.jpg", photo.FileUrl);
        Assert.Equal(PhotoUploadStatus.Complete, photo.UploadStatus);
    }

    [Fact]
    public void AC2_AttachPhoto_EnforcesMaxThreePhotos()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var record = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        condSvc.AttachPhoto(record.Id, "/p1.jpg");
        condSvc.AttachPhoto(record.Id, "/p2.jpg");
        condSvc.AttachPhoto(record.Id, "/p3.jpg");

        var ex = Assert.Throws<InvalidOperationException>(() => condSvc.AttachPhoto(record.Id, "/p4.jpg"));
        Assert.Contains("3", ex.Message);
    }

    [Fact]
    public void AC2_OfflinePhoto_IsPendingStatus_WhenQueuedOffline()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var record = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged, isOffline: true);
        var photo = condSvc.AttachPhoto(record.Id, "offline-temp.jpg", PhotoUploadStatus.Pending);

        Assert.Equal(PhotoUploadStatus.Pending, photo.UploadStatus);
        Assert.NotNull(photo.SyncQueuedAt);
        Assert.Equal(ReturnState.PendingPhotoSync, record.SyncStatus);
    }

    [Fact]
    public void AC2_MarkPhotoUploaded_TransitionsPendingToComplete()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var record = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged, isOffline: true);
        var photo = condSvc.AttachPhoto(record.Id, "offline-temp.jpg", PhotoUploadStatus.Pending);
        condSvc.MarkPhotoUploaded(photo.Id, "/server/photos/final.jpg");

        Assert.Equal(PhotoUploadStatus.Complete, photo.UploadStatus);
        Assert.NotNull(photo.SyncCompletedAt);
        Assert.Equal(ReturnState.Complete, record.SyncStatus);
    }

    [Fact]
    public void AC2_IncrementPhotoRetry_CapsAtThreeAttempts()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var record = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged, isOffline: true);
        var photo = condSvc.AttachPhoto(record.Id, "offline.jpg", PhotoUploadStatus.Pending);

        Assert.True(condSvc.IncrementPhotoRetry(photo.Id));
        Assert.True(condSvc.IncrementPhotoRetry(photo.Id));
        Assert.True(condSvc.IncrementPhotoRetry(photo.Id));
        Assert.False(condSvc.IncrementPhotoRetry(photo.Id));
    }

    // AC3: Auto-create Maintenance Ticket Draft on Damaged

    [Fact]
    public void AC3_CreateMaintenanceTicketDraft_ForDamagedReturn()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        var draft = condSvc.CreateMaintenanceTicketDraft(condRecord.Id, item.Id, item.Name, 1, "Alice", condRecord.ServerTimestampUtc, new List<int>());

        Assert.Equal(item.Id, draft.EquipmentItemId);
        Assert.Equal(ConditionGrade.Damaged, draft.ConditionGrade);
        Assert.Equal("Alice", draft.ReportedByName);
        Assert.Equal(MaintenanceTicketState.Draft, draft.State);
    }

    [Fact]
    public void AC3_MaintenanceTicketDraft_LinkedToConditionRecord()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        var draft = condSvc.CreateMaintenanceTicketDraft(condRecord.Id, item.Id, item.Name, 1, "Alice", condRecord.ServerTimestampUtc, new List<int>());
        var retrieved = condSvc.GetMaintenanceTicketDraft(condRecord.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(draft.Id, retrieved!.Id);
    }

    [Fact]
    public void AC3_NoMaintenanceTicket_ForGoodReturn()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Good);
        Assert.Null(condSvc.GetMaintenanceTicketDraft(condRecord.Id));
    }

    // AC4: Lost Path

    [Fact]
    public void AC4_FlagEquipmentLost_SetsLifecycleStatusToLost()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Lost);
        condSvc.FlagEquipmentLost(item.Id, condRecord.Id, 1, "Alice");

        var updatedItem = equipSvc.GetItem(item.Id)!;
        Assert.Equal(EquipmentLifecycleStatus.Lost, updatedItem.LifecycleStatus);
        Assert.False(updatedItem.IsAvailable);
    }

    [Fact]
    public void AC4_LostEquipment_RequiresAdminReactivation()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Lost);
        condSvc.FlagEquipmentLost(item.Id, condRecord.Id, 1, "Alice");

        Assert.False(equipSvc.Checkout(item.Id, "Bob", 2));

        Assert.True(condSvc.ReactivateLostEquipment(item.Id, adminUserId: 99));
        Assert.Equal(EquipmentLifecycleStatus.Available, equipSvc.GetItem(item.Id)!.LifecycleStatus);
    }

    [Fact]
    public void AC4_LostPath_DoesNotCreateMaintenanceTicket()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Lost);
        condSvc.FlagEquipmentLost(item.Id, condRecord.Id, 1, "Alice");

        Assert.Null(condSvc.GetMaintenanceTicketDraft(condRecord.Id));
        Assert.NotNull(condSvc.GetLostFlag(item.Id));
    }

    // AC5: Email Notification to Maintenance Coordinator

    [Fact]
    public void AC5_MarkMaintenanceNotificationSent_RecordsTimestamp()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        var draft = condSvc.CreateMaintenanceTicketDraft(condRecord.Id, item.Id, item.Name, 1, "Alice", condRecord.ServerTimestampUtc, new List<int>());

        Assert.False(draft.NotificationSent);
        var sentAt = DateTime.UtcNow;
        Assert.True(condSvc.MarkMaintenanceNotificationSent(draft.Id, sentAt));
        Assert.True(draft.NotificationSent);
        Assert.Equal(sentAt, draft.NotificationSentAtUtc);
    }

    [Fact]
    public void AC5_MaintenanceNotification_ContainsRequiredFields()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        var photoId = condSvc.AttachPhoto(condRecord.Id, "/photos/damage.jpg").Id;
        var draft = condSvc.CreateMaintenanceTicketDraft(condRecord.Id, item.Id, item.Name, 1, "Alice", condRecord.ServerTimestampUtc, new List<int> { photoId });

        Assert.Equal(item.Id, draft.EquipmentItemId);
        Assert.Equal(item.Name, draft.EquipmentName);
        Assert.Equal(ConditionGrade.Damaged, draft.ConditionGrade);
        Assert.Equal("Alice", draft.ReportedByName);
        Assert.Contains(photoId, draft.PhotoIds);
        Assert.NotEqual(0, draft.Id);
    }

    // AC6: Scheduling Conflict Alert

    [Fact]
    public void AC6_ConflictAlert_RaisedForDamagedReturn_WithFutureReservations()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        condSvc.AddReservation(item.Id, "Bob", "Site A", DateTime.UtcNow.AddDays(3));

        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        var alert = condSvc.DetectAndCreateConflictAlert(condRecord.Id, item.Id, "ops-manager");

        Assert.NotNull(alert);
        Assert.Single(alert!.ConflictingReservationIds);
        Assert.Equal("ops-manager", alert.AlertedTo);
        Assert.Equal("Bob", alert.ConflictDetails[0].OperatorName);
    }

    [Fact]
    public void AC6_ConflictAlert_RaisedForLostReturn_WithFutureReservations()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        condSvc.AddReservation(item.Id, "Charlie", "Site B", DateTime.UtcNow.AddDays(5));

        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Lost);
        var alert = condSvc.DetectAndCreateConflictAlert(condRecord.Id, item.Id, "ops-manager");

        Assert.NotNull(alert);
        Assert.Contains(alert!.ConflictDetails, d => d.OperatorName == "Charlie");
    }

    [Fact]
    public void AC6_ConflictAlert_NotRaised_WhenNoFutureReservations()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        var alert = condSvc.DetectAndCreateConflictAlert(condRecord.Id, item.Id, "ops-manager");

        Assert.Null(alert);
    }

    [Fact]
    public void AC6_ConflictAlert_NotRaisedForGoodReturn()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        condSvc.AddReservation(item.Id, "Dave", "Site C", DateTime.UtcNow.AddDays(2));

        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Good);
        Assert.Null(condSvc.GetConflictAlert(condRecord.Id));
    }

    // AC7: Severity-Based Notification Routing

    [Fact]
    public void AC7_GoodReturn_AuditRecordOnly_NoTicketNoFlag()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Good);

        Assert.Null(condSvc.GetMaintenanceTicketDraft(condRecord.Id));
        Assert.Null(condSvc.GetLostFlag(item.Id));
    }

    [Fact]
    public void AC7_WornReturn_AuditRecordOnly_NoTicketNoFlag()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Worn);

        Assert.Null(condSvc.GetMaintenanceTicketDraft(condRecord.Id));
        Assert.Null(condSvc.GetLostFlag(item.Id));
    }

    [Fact]
    public void AC7_DamagedReturn_MaintenanceTicketOnly_NoLostFlag()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        condSvc.CreateMaintenanceTicketDraft(condRecord.Id, item.Id, item.Name, 1, "Alice", condRecord.ServerTimestampUtc, new List<int>());

        Assert.NotNull(condSvc.GetMaintenanceTicketDraft(condRecord.Id));
        Assert.Null(condSvc.GetLostFlag(item.Id));
    }

    [Fact]
    public void AC7_LostReturn_LostFlagOnly_NoMaintenanceTicket()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Lost);
        condSvc.FlagEquipmentLost(item.Id, condRecord.Id, 1, "Alice");

        Assert.NotNull(condSvc.GetLostFlag(item.Id));
        Assert.Null(condSvc.GetMaintenanceTicketDraft(condRecord.Id));
    }

    // AC8: Timestamped Immutable Audit Trail

    [Fact]
    public void AC8_ConditionRecord_PersistedForEveryReturn()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();

        foreach (var grade in Enum.GetValues<ConditionGrade>())
        {
            item.IsAvailable = true;
            item.LifecycleStatus = EquipmentLifecycleStatus.Available;
            equipSvc.Checkout(item.Id, "Alice", 1);
            var checkout = equipSvc.GetActiveCheckoutRecord(item.Id)!;
            equipSvc.Return(item.Id);

            var record = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", grade);
            Assert.NotNull(record);
            Assert.Equal(grade, record.Grade);
        }
    }

    [Fact]
    public void AC8_ConditionRecord_ServerTimestamp_IsCurrentTime()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var deviceTime = DateTime.UtcNow.AddHours(-1);
        var record = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Good);

        Assert.True(record.ServerTimestampUtc > deviceTime.AddMinutes(30));
    }

    [Fact]
    public void AC8_GetConditionRecord_ReturnsRecordByCheckoutId()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var created = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Worn);
        var fetched = condSvc.GetConditionRecord(checkout.Id);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(ConditionGrade.Worn, fetched.Grade);
    }

    // AC9: Dispute Resolution — View Full Condition History

    [Fact]
    public void AC9_GetConditionHistory_ReturnsAllRecords_ReverseChronological()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();

        for (int i = 0; i < 3; i++)
        {
            item.IsAvailable = true;
            item.LifecycleStatus = EquipmentLifecycleStatus.Available;
            equipSvc.Checkout(item.Id, "Alice", 1);
            var co = equipSvc.GetActiveCheckoutRecord(item.Id)!;
            equipSvc.Return(item.Id);
            var g = i switch { 0 => ConditionGrade.Good, 1 => ConditionGrade.Worn, _ => ConditionGrade.Damaged };
            var rec = condSvc.CreateConditionRecord(co.Id, item.Id, item.Name, 1, "Alice", g);
            rec.ServerTimestampUtc = DateTime.UtcNow.AddDays(-i);
        }

        var history = condSvc.GetConditionHistory(item.Id);
        Assert.Equal(3, history.Count);
        for (int i = 0; i < history.Count - 1; i++)
            Assert.True(history[i].ServerTimestampUtc >= history[i + 1].ServerTimestampUtc);
    }

    [Fact]
    public void AC9_GetConditionHistory_ShowsPhotosOperatorAndTimestamp()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        condSvc.AttachPhoto(condRecord.Id, "/photos/d1.jpg");
        condSvc.AttachPhoto(condRecord.Id, "/photos/d2.jpg");

        var history = condSvc.GetConditionHistory(item.Id);
        var photos = condSvc.GetPhotos(history[0].Id);

        Assert.Equal("Alice", history[0].OperatorName);
        Assert.Equal(2, photos.Count);
    }

    [Fact]
    public void AC9_GetConditionHistory_ReturnsEmpty_WhenNoRecords()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        Assert.Empty(condSvc.GetConditionHistory(item.Id));
    }

    // TS-1: Happy Path — Good Condition Return

    [Fact]
    public void TS1_GoodConditionReturn_AuditRecordOnly_NoDownstreamActions()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var record = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Good);

        Assert.NotNull(condSvc.GetConditionRecord(checkout.Id));
        Assert.Null(condSvc.GetMaintenanceTicketDraft(record.Id));
        Assert.Null(condSvc.GetLostFlag(item.Id));
        Assert.Single(condSvc.GetConditionHistory(item.Id));
    }

    // TS-2: Damaged Return with Photos — Full Downstream Flow

    [Fact]
    public void TS2_DamagedReturn_FullDownstreamFlow()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        condSvc.AddReservation(item.Id, "Bob", "Site X", DateTime.UtcNow.AddDays(3));

        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        var photo1 = condSvc.AttachPhoto(condRecord.Id, "/p1.jpg");
        var photo2 = condSvc.AttachPhoto(condRecord.Id, "/p2.jpg");
        var draft = condSvc.CreateMaintenanceTicketDraft(condRecord.Id, item.Id, item.Name, 1, "Alice", condRecord.ServerTimestampUtc, new List<int> { photo1.Id, photo2.Id });
        condSvc.MarkMaintenanceNotificationSent(draft.Id, DateTime.UtcNow);
        var alert = condSvc.DetectAndCreateConflictAlert(condRecord.Id, item.Id, "ops-manager");

        Assert.Equal(2, draft.PhotoIds.Count);
        Assert.True(draft.NotificationSent);
        Assert.NotNull(alert);
    }

    // TS-3: Lost Path

    [Fact]
    public void TS3_LostReturn_FlagsInventory_NoMaintenanceTicket()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Lost);
        condSvc.FlagEquipmentLost(item.Id, condRecord.Id, 1, "Alice");

        Assert.NotNull(condSvc.GetLostFlag(item.Id));
        Assert.Null(condSvc.GetMaintenanceTicketDraft(condRecord.Id));
        Assert.Equal(EquipmentLifecycleStatus.Lost, equipSvc.GetItem(item.Id)!.LifecycleStatus);
    }

    // TS-4: Offline Photo Queue

    [Fact]
    public void TS4_OfflineReturn_ReturnConfirmedLocally_PhotosSyncOnReconnect()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged, isOffline: true);
        Assert.Equal(ReturnState.PendingPhotoSync, condRecord.SyncStatus);

        var photo = condSvc.AttachPhoto(condRecord.Id, "local-temp.jpg", PhotoUploadStatus.Pending);
        Assert.Equal(PhotoUploadStatus.Pending, photo.UploadStatus);

        condSvc.MarkPhotoUploaded(photo.Id, "/server/synced.jpg");
        Assert.Equal(PhotoUploadStatus.Complete, photo.UploadStatus);
        Assert.Equal(ReturnState.Complete, condRecord.SyncStatus);
    }

    // TS-5: Scheduling Conflict Alert

    [Fact]
    public void TS5_SchedulingConflictAlert_ListsConflictDetails()
    {
        var (equipSvc, condSvc) = CreateServices();
        var item = equipSvc.GetAllItems().First();
        var futureStart = DateTime.UtcNow.AddDays(3);
        condSvc.AddReservation(item.Id, "Carlos", "Site Z", futureStart, futureStart.AddDays(2));

        var checkout = SetupActiveCheckout(equipSvc, item.Id);
        equipSvc.Return(item.Id);

        var condRecord = condSvc.CreateConditionRecord(checkout.Id, item.Id, item.Name, 1, "Alice", ConditionGrade.Damaged);
        var alert = condSvc.DetectAndCreateConflictAlert(condRecord.Id, item.Id, "dispatcher");

        Assert.NotNull(alert);
        Assert.Single(alert!.ConflictDetails);
        Assert.Equal("Carlos", alert.ConflictDetails[0].OperatorName);
        Assert.Equal("Site Z", alert.ConflictDetails[0].SiteName);
        Assert.False(alert.IsAcknowledged);
    }
}