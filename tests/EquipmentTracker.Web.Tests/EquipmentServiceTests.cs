using EquipmentTracker.Web.Services;
using Xunit;

namespace EquipmentTracker.Web.Tests;

public class EquipmentServiceTests
{
    private static EquipmentService CreateService() => new EquipmentService();

    // ── AvailableCount ─────────────────────────────────────────────────────────

    [Fact]
    public void GetAllItems_AvailableCount_DecreasesAfterCheckout()
    {
        var service = CreateService();
        int before = service.GetAllItems().Count(i => i.IsAvailable);

        var item = service.GetAllItems().First(i => i.IsAvailable);
        service.Checkout(item.Id, "Alice");

        int after = service.GetAllItems().Count(i => i.IsAvailable);
        Assert.Equal(before - 1, after);
    }

    // ── CreateItem ────────────────────────────────────────────────────────────

    [Fact]
    public void CreateItem_AddsItemToList()
    {
        var service = CreateService();
        int countBefore = service.GetAllItems().Count;

        var item = service.CreateItem("Laser Pointer", "Electronics");

        Assert.Equal(countBefore + 1, service.GetAllItems().Count);
        Assert.Equal("Laser Pointer", item.Name);
        Assert.Equal("Electronics", item.Category);
        Assert.True(item.IsAvailable);
    }

    // ── Checkout ──────────────────────────────────────────────────────────────

    [Fact]
    public void Checkout_ReturnsTrue_WhenItemIsAvailable()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);

        var result = service.Checkout(item.Id, "Alice");

        Assert.True(result);
        Assert.False(service.GetItem(item.Id)!.IsAvailable);
    }

    [Fact]
    public void Checkout_ReturnsFalse_WhenItemIsAlreadyCheckedOut()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);

        service.Checkout(item.Id, "Alice");
        var secondAttempt = service.Checkout(item.Id, "Bob");

        Assert.False(secondAttempt);
    }

    // ── Return ────────────────────────────────────────────────────────────────

    [Fact]
    public void Return_ReturnsTrue_WhenItemIsCheckedOut()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);
        service.Checkout(item.Id, "Alice");

        var result = service.Return(item.Id);

        Assert.True(result);
        Assert.True(service.GetItem(item.Id)!.IsAvailable);
    }

    [Fact]
    public void Return_ReturnsFalse_WhenItemIsAlreadyAvailable()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);

        // Item has never been checked out
        var result = service.Return(item.Id);

        Assert.False(result);
    }

    // ── GetCurrentHolder ──────────────────────────────────────────────────────

    [Fact]
    public void GetCurrentHolder_ReturnsNull_WhenItemIsAvailable()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);

        var holder = service.GetCurrentHolder(item.Id);

        Assert.Null(holder);
    }

    [Fact]
    public void GetCurrentHolder_ReturnsBorrowerName_WhenItemIsCheckedOut()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);
        service.Checkout(item.Id, "Alice");

        var holder = service.GetCurrentHolder(item.Id);

        Assert.Equal("Alice", holder);
    }

    [Fact]
    public void GetCurrentHolder_ReturnsNull_AfterItemIsReturned()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);
        service.Checkout(item.Id, "Alice");
        service.Return(item.Id);

        var holder = service.GetCurrentHolder(item.Id);

        Assert.Null(holder);
    }

    // ── GetCheckoutHistory ────────────────────────────────────────────────────

    [Fact]
    public void GetCheckoutHistory_ReturnsEmpty_WhenItemHasNeverBeenCheckedOut()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);

        var history = service.GetCheckoutHistory(item.Id);

        Assert.Empty(history);
    }

    [Fact]
    public void GetCheckoutHistory_ReturnsSingleRecord_AfterOneCheckout()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);
        service.Checkout(item.Id, "Alice");

        var history = service.GetCheckoutHistory(item.Id);

        Assert.Single(history);
        Assert.Equal("Alice", history[0].BorrowerName);
    }

    [Fact]
    public void GetCheckoutHistory_ReturnsNewestFirst_AfterMultipleCheckouts()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);

        service.Checkout(item.Id, "Alice");
        service.Return(item.Id);
        service.Checkout(item.Id, "Bob");

        var history = service.GetCheckoutHistory(item.Id);

        Assert.Equal(2, history.Count);
        // Newest (Bob) must be first
        Assert.Equal("Bob", history[0].BorrowerName);
        Assert.Equal("Alice", history[1].BorrowerName);
    }

    [Fact]
    public void GetCheckoutHistory_ShowsReturnedAtUtc_AfterItemIsReturned()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);
        service.Checkout(item.Id, "Alice");
        service.Return(item.Id);

        var history = service.GetCheckoutHistory(item.Id);

        Assert.Single(history);
        Assert.NotNull(history[0].ReturnedAtUtc);
    }

    [Fact]
    public void GetCheckoutHistory_ActiveCheckout_HasNullReturnedAtUtc()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);
        service.Checkout(item.Id, "Alice");

        var history = service.GetCheckoutHistory(item.Id);

        Assert.Single(history);
        Assert.Null(history[0].ReturnedAtUtc);
    }

    // ── GetActiveCheckoutRecord ───────────────────────────────────────────────

    [Fact]
    public void GetActiveCheckoutRecord_ReturnsNull_WhenItemIsAvailable()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);

        var record = service.GetActiveCheckoutRecord(item.Id);

        Assert.Null(record);
    }

    [Fact]
    public void GetActiveCheckoutRecord_ReturnsRecord_WhenItemIsCheckedOut()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);
        service.Checkout(item.Id, "Alice");

        var record = service.GetActiveCheckoutRecord(item.Id);

        Assert.NotNull(record);
        Assert.Equal("Alice", record!.BorrowerName);
        Assert.Equal(item.Id, record.EquipmentItemId);
        Assert.Null(record.ReturnedAtUtc);
    }

    [Fact]
    public void GetActiveCheckoutRecord_ReturnsNull_AfterItemIsReturned()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);
        service.Checkout(item.Id, "Alice");
        service.Return(item.Id);

        var record = service.GetActiveCheckoutRecord(item.Id);

        Assert.Null(record);
    }

    [Fact]
    public void GetActiveCheckoutRecord_ReturnsNull_ForNonExistentItem()
    {
        var service = CreateService();

        var record = service.GetActiveCheckoutRecord(9999);

        Assert.Null(record);
    }

    // ── Overdue computation ───────────────────────────────────────────────────

    [Fact]
    public void GetActiveCheckoutRecord_CheckedOutAtUtc_IsRecentAfterCheckout()
    {
        var service = CreateService();
        var item = service.GetAllItems().First(i => i.IsAvailable);
        var before = DateTime.UtcNow.AddSeconds(-1);

        service.Checkout(item.Id, "Bob");

        var record = service.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        Assert.True(record!.CheckedOutAtUtc >= before);
        Assert.True(record.CheckedOutAtUtc <= DateTime.UtcNow.AddSeconds(1));
    }
}
