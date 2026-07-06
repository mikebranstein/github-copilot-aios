using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IEquipmentService
{
    IReadOnlyList<EquipmentItem> GetAllItems();
    EquipmentItem? GetItem(int id);
    EquipmentItem CreateItem(string name, string category);

    /// <summary>Returns false if the item does not exist or is already checked out.</summary>
    bool Checkout(int itemId, string borrowerName);

    /// <summary>Returns false if the item does not exist or is already available.</summary>
    bool Return(int itemId);

    /// <summary>Returns the borrower name of the active (unreturned) checkout for the given item, or null if the item is available.</summary>
    string? GetCurrentHolder(int itemId);
}
