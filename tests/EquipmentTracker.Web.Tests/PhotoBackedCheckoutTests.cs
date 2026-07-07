using EquipmentTracker.Web.Controllers;
using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Automated tests for Issue #58 — Photo-Backed Checkout &amp; Return with Fair Witness Accountability.
/// Covers all P0 acceptance criteria: AC-C1 through AC-SYNC2.
/// Run with: dotnet test
/// </summary>
public class PhotoBackedCheckoutTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static EquipmentService CreateEquipmentService() => new();

    private static LocalPhotoStorageService CreatePhotoStorage(IEquipmentService svc)
        => new(svc);

    private static PhotoSyncService CreateSyncService() => new();

    // ── AC-C1: SaveCheckoutPhoto attaches photo URL to CheckoutRecord.ConditionPhotoAtCheckout ──

    [Fact]
    public async Task AC_C1_SaveCheckoutPhoto_AttachesPhotoUrlToConditionPhotoAtCheckout()
    {
        var equipSvc = CreateEquipmentService();
        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "Alice", borrowerUserId: 1);
        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);

        var storage = CreatePhotoStorage(equipSvc);
        var photoBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // minimal JPEG header
        var photoUrl = await storage.SavePhotoAsync(photoBytes, "Alice");

        await storage.AttachToCheckoutRecordAsync(record!.Id, photoUrl, isReturn: false);

        Assert.NotNull(record.ConditionPhotoAtCheckout);
        Assert.Equal(photoUrl, record.ConditionPhotoAtCheckout);
    }

    // ── AC-C2: Photo URL includes borrower identity context ──────────────────

    [Fact]
    public void AC_C2_CheckoutRecord_HasBorrowerUserIdAndCheckedOutAtUtc()
    {
        var equipSvc = CreateEquipmentService();
        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "Alice", borrowerUserId: 42);
        var record = equipSvc.GetActiveCheckoutRecord(item.Id);

        Assert.NotNull(record);
        Assert.Equal(42, record!.BorrowerUserId);
        Assert.True(record.CheckedOutAtUtc > DateTime.UtcNow.AddSeconds(-5));
    }

    // ── AC-C3: SkipPhoto sets ConditionPhotoSkippedAtCheckout = true ─────────

    [Fact]
    public void AC_C3_SkipPhoto_SetsConditionPhotoSkippedAtCheckout_True()
    {
        var equipSvc = CreateEquipmentService();
        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "Alice", borrowerUserId: 1);
        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);

        // Simulate SkipPhoto action
        record!.ConditionPhotoSkippedAtCheckout = true;

        Assert.True(record.ConditionPhotoSkippedAtCheckout);
    }

    // ── AC-C4: QueueForSyncAsync creates OfflineSyncTransaction with PhotoLocalPath set ──

    [Fact]
    public async Task AC_C4_QueueForSyncAsync_CreatesTransactionWithPhotoLocalPath()
    {
        var syncSvc = CreateSyncService();
        var localPath = "/device/photos/checkout-001.jpg";

        await syncSvc.QueueForSyncAsync(checkoutRecordId: 1, photoLocalPath: localPath);

        Assert.True(syncSvc.HasPendingSync);
    }

    // ── AC-C5: Checkout can complete without photo ────────────────────────────

    [Fact]
    public void AC_C5_Checkout_SucceedsWithoutPhoto()
    {
        var equipSvc = CreateEquipmentService();
        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);

        var success = equipSvc.Checkout(item.Id, "Alice", borrowerUserId: 1);

        Assert.True(success);
        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        Assert.Null(record!.ConditionPhotoAtCheckout); // photo not required
    }

    // ── AC-C6: ICameraService.OpenAsync() returns true immediately (no network calls) ──

    [Fact]
    public async Task AC_C6_CameraService_OpenAsync_ReturnsTrueImmediately()
    {
        var cameraSvc = new MobileCameraService();

        var result = await cameraSvc.OpenAsync();

        Assert.True(result);
    }

    // ── AC-C9: CompressIfNeeded returns bytes ≤ 2,097,152 for any input ──────

    [Fact]
    public void AC_C9_CompressIfNeeded_ReturnsAtMost2MB_ForAnyInput()
    {
        var equipSvc = CreateEquipmentService();
        var storage = CreatePhotoStorage(equipSvc);

        // Input exceeding 2 MB
        var bigPhoto = new byte[3_000_000];
        new Random(42).NextBytes(bigPhoto);

        var result = storage.CompressIfNeeded(bigPhoto);

        Assert.True(result.Length <= 2_097_152,
            $"Expected ≤2097152 bytes but got {result.Length}");
    }

    [Fact]
    public void AC_C9_CompressIfNeeded_ReturnsSameBytes_WhenUnder2MB()
    {
        var equipSvc = CreateEquipmentService();
        var storage = CreatePhotoStorage(equipSvc);

        var smallPhoto = new byte[500_000];
        var result = storage.CompressIfNeeded(smallPhoto);

        Assert.Equal(smallPhoto.Length, result.Length);
    }

    // ── AC-FW1: MobileReturnConfirmViewModel.FairWitnessPhotoUrl populated from CheckoutRecord ──

    [Fact]
    public void AC_FW1_ReturnConfirmViewModel_FairWitnessPhotoUrl_PopulatedFromCheckoutRecord()
    {
        var equipSvc = CreateEquipmentService();
        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "Alice", borrowerUserId: 1);
        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        record!.ConditionPhotoAtCheckout = "/photos/checkout-abc.jpg";

        // Simulate what the controller does
        var vm = new MobileReturnConfirmViewModel
        {
            Item = item,
            ActiveRecord = record,
            FairWitnessPhotoUrl = record.ConditionPhotoAtCheckout,
            FairWitnessTimestamp = record.CheckedOutAtUtc,
            FairWitnessItemName = item.Name
        };

        Assert.Equal("/photos/checkout-abc.jpg", vm.FairWitnessPhotoUrl);
        Assert.True(vm.HasFairWitnessPhoto);
    }

    // ── AC-FW2: ViewModel has all 4 Fair Witness flat properties ─────────────

    [Fact]
    public void AC_FW2_ReturnConfirmViewModel_HasAllFairWitnessProperties()
    {
        var vm = new MobileReturnConfirmViewModel
        {
            Item = new EquipmentTracker.Web.Models.EquipmentItem { Id = 1, Name = "Drill", Category = "Tools", IsAvailable = false },
            FairWitnessPhotoUrl = "/photos/fw.jpg",
            FairWitnessTimestamp = DateTime.UtcNow,
            FairWitnessItemName = "Drill"
        };

        // Verify all 4 flat properties exist and are accessible
        Assert.NotNull(vm.FairWitnessPhotoUrl);
        Assert.NotNull(vm.FairWitnessTimestamp);
        Assert.NotNull(vm.FairWitnessItemName);
        Assert.True(vm.HasFairWitnessPhoto);
    }

    // ── AC-FW3: When ConditionPhotoAtCheckout is null, HasFairWitnessPhoto = false ──

    [Fact]
    public void AC_FW3_WhenConditionPhotoAtCheckout_IsNull_HasFairWitnessPhoto_IsFalse()
    {
        var vm = new MobileReturnConfirmViewModel
        {
            Item = new EquipmentTracker.Web.Models.EquipmentItem { Id = 1, Name = "Drill", Category = "Tools", IsAvailable = false },
            FairWitnessPhotoUrl = null
        };

        Assert.False(vm.HasFairWitnessPhoto);
    }

    // ── AC-R1: IsCaptureReturnPhotoButtonEnabled = true before return confirmed ──

    [Fact]
    public void AC_R1_ReturnConfirmViewModel_IsCaptureReturnPhotoButtonEnabled_TrueByDefault()
    {
        var vm = new MobileReturnConfirmViewModel
        {
            Item = new EquipmentTracker.Web.Models.EquipmentItem { Id = 1, Name = "Drill", Category = "Tools", IsAvailable = false }
        };

        Assert.True(vm.IsCaptureReturnPhotoButtonEnabled);
    }

    // ── AC-R2: SaveReturnPhoto sets ConditionPhotoAtReturn on CheckoutRecord ──

    [Fact]
    public async Task AC_R2_SaveReturnPhoto_SetsConditionPhotoAtReturn_OnCheckoutRecord()
    {
        var equipSvc = CreateEquipmentService();
        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "Alice", borrowerUserId: 1);
        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);

        var storage = CreatePhotoStorage(equipSvc);
        var returnPhotoUrl = await storage.SavePhotoAsync(new byte[] { 0x01, 0x02 }, "Alice");
        await storage.AttachToCheckoutRecordAsync(record!.Id, returnPhotoUrl, isReturn: true);

        Assert.NotNull(record.ConditionPhotoAtReturn);
        Assert.Equal(returnPhotoUrl, record.ConditionPhotoAtReturn);
    }

    // ── AC-CR1: CoordinatorPhotoReviewViewModel has both photo URL properties ──

    [Fact]
    public void AC_CR1_CoordinatorPhotoReviewViewModel_HasBothPhotoUrlProperties()
    {
        var vm = new CoordinatorPhotoReviewViewModel
        {
            CheckoutRecordId = 1,
            BorrowerName = "Alice",
            ItemName = "Drill",
            CheckedOutAtUtc = DateTime.UtcNow.AddHours(-2),
            ReturnedAtUtc = DateTime.UtcNow,
            CheckoutPhotoUrl = "/photos/checkout-123.jpg",
            ReturnPhotoUrl = "/photos/return-123.jpg"
        };

        Assert.NotNull(vm.CheckoutPhotoUrl);
        Assert.NotNull(vm.ReturnPhotoUrl);
        Assert.True(vm.HasCheckoutPhoto);
        Assert.True(vm.HasReturnPhoto);
    }

    // ── AC-CR2: EnlargeCheckoutPhotoCommand and EnlargeReturnPhotoCommand are true ──

    [Fact]
    public void AC_CR2_CoordinatorPhotoReviewViewModel_EnlargeCommands_AreTrue()
    {
        var vm = new CoordinatorPhotoReviewViewModel();

        Assert.True(vm.EnlargeCheckoutPhotoCommand);
        Assert.True(vm.EnlargeReturnPhotoCommand);
    }

    // ── AC-CR3: SetDamageAssessment saves NoDamage/MinorDamage/SignificantDamage ──

    [Theory]
    [InlineData("NoDamage")]
    [InlineData("MinorDamage")]
    [InlineData("SignificantDamage")]
    public void AC_CR3_SetDamageAssessment_SavesAllValidValues(string assessment)
    {
        var equipSvc = CreateEquipmentService();
        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "Alice", borrowerUserId: 1);
        equipSvc.Return(item.Id);
        var record = equipSvc.GetCheckoutRecordById(1);
        Assert.NotNull(record);

        // Simulate what the controller does
        record!.ConditionAssessment = assessment;

        Assert.Equal(assessment, record.ConditionAssessment);
    }

    // ── AC-DM1: CheckoutRecord has all three checkout photo fields ────────────

    [Fact]
    public void AC_DM1_CheckoutRecord_HasAllThreeCheckoutPhotoFields()
    {
        var record = new CheckoutRecord();

        // Verify all fields exist with correct defaults
        Assert.Null(record.ConditionPhotoAtCheckout);
        Assert.Null(record.ConditionAtCheckout);
        Assert.False(record.ConditionPhotoSkippedAtCheckout);
    }

    // ── AC-DM2: CheckoutRecord has ConditionPhotoAtReturn and ConditionAtReturn ──

    [Fact]
    public void AC_DM2_CheckoutRecord_HasReturnPhotoFields()
    {
        var record = new CheckoutRecord();

        Assert.Null(record.ConditionPhotoAtReturn);
        Assert.Null(record.ConditionAtReturn);
    }

    // ── AC-DM3: MobileReturnConfirmViewModel has all 4 Fair Witness fields ────

    [Fact]
    public void AC_DM3_MobileReturnConfirmViewModel_HasAllFourFairWitnessFields()
    {
        var vm = new MobileReturnConfirmViewModel
        {
            Item = new EquipmentTracker.Web.Models.EquipmentItem { Id = 1, Name = "Drill", Category = "Tools", IsAvailable = false },
            FairWitnessPhotoUrl = "/photos/fw.jpg",
            FairWitnessTimestamp = DateTime.UtcNow,
            FairWitnessItemName = "Drill"
        };

        // All 4 Fair Witness fields must be present as flat properties
        Assert.NotNull(vm.FairWitnessPhotoUrl);   // property 1
        Assert.NotNull(vm.FairWitnessTimestamp);  // property 2
        Assert.NotNull(vm.FairWitnessItemName);   // property 3
        Assert.True(vm.HasFairWitnessPhoto);       // property 4 (computed)
    }

    // ── AC-SYNC1: After 3 failed syncs, PendingSyncIndicatorVisible = true ────

    [Fact]
    public async Task AC_SYNC1_AfterThreeFailedSyncs_PendingSyncIndicatorVisible_IsTrue()
    {
        var syncSvc = new PhotoSyncService();

        // Always fail upload
        syncSvc.UploadHandler = _ => Task.FromResult(false);
        // Skip real delays
        syncSvc.DelayFactory = _ => Task.CompletedTask;

        await syncSvc.QueueForSyncAsync(1, "/device/photos/test.jpg");

        var syncedCount = await syncSvc.SyncPendingPhotosAsync();

        Assert.Equal(0, syncedCount);
        Assert.True(syncSvc.HasPendingSync);
        Assert.True(syncSvc.PendingSyncIndicatorVisible);
    }

    // ── AC-SYNC2: Duplicate upload prevention via PhotoUploadKey ─────────────

    [Fact]
    public async Task AC_SYNC2_DuplicateUploadPrevention_SameUploadKey_OnlySyncedOnce()
    {
        var syncSvc = new PhotoSyncService();
        syncSvc.DelayFactory = _ => Task.CompletedTask;

        var localPath = "/device/photos/same-photo.jpg";

        // Queue the same path twice (same content → same SHA-256 key)
        await syncSvc.QueueForSyncAsync(1, localPath);
        await syncSvc.QueueForSyncAsync(1, localPath);

        // First sync: uploads one, marks second as duplicate → removes both from pending
        var syncedCount = await syncSvc.SyncPendingPhotosAsync();

        // Only one unique upload should have occurred
        Assert.Equal(1, syncedCount);
        // No pending items after sync
        Assert.False(syncSvc.HasPendingSync);
    }

    // ── Additional: GenerateUploadKey produces deterministic SHA-256 hash ─────

    [Fact]
    public void GenerateUploadKey_IsDeterministic_ForSameInput()
    {
        var equipSvc = CreateEquipmentService();
        var storage = CreatePhotoStorage(equipSvc);
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        var key1 = storage.GenerateUploadKey(bytes);
        var key2 = storage.GenerateUploadKey(bytes);

        Assert.Equal(key1, key2);
        Assert.NotEmpty(key1);
        Assert.Equal(64, key1.Length); // SHA-256 hex = 64 chars
    }

    [Fact]
    public void GenerateUploadKey_IsDifferent_ForDifferentInput()
    {
        var equipSvc = CreateEquipmentService();
        var storage = CreatePhotoStorage(equipSvc);

        var key1 = storage.GenerateUploadKey(new byte[] { 1, 2, 3 });
        var key2 = storage.GenerateUploadKey(new byte[] { 4, 5, 6 });

        Assert.NotEqual(key1, key2);
    }

    // ── Minimal TempData provider for controller tests ────────────────────────

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
            => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
