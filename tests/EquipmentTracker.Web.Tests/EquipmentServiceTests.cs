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
}
