using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public class EquipmentService : IEquipmentService
{
    private readonly List<EquipmentItem> _items = new();
    private readonly List<CheckoutRecord> _records = new();
    private int _nextItemId = 1;
    private int _nextRecordId = 1;

    public EquipmentService()
    {
        _items.Add(new EquipmentItem { Id = _nextItemId++, Name = "Laptop", Category = "Electronics", IsAvailable = true });
        _items.Add(new EquipmentItem { Id = _nextItemId++, Name = "Projector", Category = "Electronics", IsAvailable = true });
        _items.Add(new EquipmentItem { Id = _nextItemId++, Name = "Whiteboard Marker Set", Category = "Stationery", IsAvailable = true });
    }

    public IReadOnlyList<EquipmentItem> GetAllItems() => _items.AsReadOnly();

    public EquipmentItem? GetItem(int id) => _items.FirstOrDefault(i => i.Id == id);

    public EquipmentItem CreateItem(string name, string category)
    {
        var item = new EquipmentItem
        {
            Id = _nextItemId++,
            Name = name,
            Category = category,
            IsAvailable = true
        };
        _items.Add(item);
        return item;
    }

    public bool Checkout(int itemId, string borrowerName, int? borrowerUserId = null)
    {
        var item = GetItem(itemId);
        if (item is null || !item.IsAvailable)
            return false;

        item.IsAvailable = false;

        _records.Add(new CheckoutRecord
        {
            Id = _nextRecordId++,
            EquipmentItemId = itemId,
            BorrowerName = borrowerName,
            BorrowerUserId = borrowerUserId,
            CheckedOutAtUtc = DateTime.UtcNow
        });

        return true;
    }

    public bool Return(int itemId)
    {
        var item = GetItem(itemId);
        if (item is null || item.IsAvailable)
            return false;

        item.IsAvailable = true;

        var record = _records
            .LastOrDefault(r => r.EquipmentItemId == itemId && r.ReturnedAtUtc is null);

        if (record is not null)
            record.ReturnedAtUtc = DateTime.UtcNow;

        return true;
    }

    public string? GetCurrentHolder(int itemId)
    {
        return _records
            .LastOrDefault(r => r.EquipmentItemId == itemId && r.ReturnedAtUtc is null)
            ?.BorrowerName;
    }

    public IReadOnlyList<CheckoutRecord> GetCheckoutHistory(int itemId)
    {
        return _records
            .Where(r => r.EquipmentItemId == itemId)
            .OrderByDescending(r => r.CheckedOutAtUtc)
            .ToList()
            .AsReadOnly();
    }

    public CheckoutRecord? GetActiveCheckoutRecord(int itemId) =>
        _records.LastOrDefault(r => r.EquipmentItemId == itemId && r.ReturnedAtUtc is null);

    public IReadOnlyList<CheckoutHistoryEntry> GetAllCheckoutHistory()
    {
        // Build a lookup so we can resolve item names without repeated scans
        var itemNameById = _items.ToDictionary(i => i.Id, i => i.Name);

        return _records
            .OrderByDescending(r => r.CheckedOutAtUtc)
            .Select(r => new CheckoutHistoryEntry
            {
                ItemName       = itemNameById.TryGetValue(r.EquipmentItemId, out var name) ? name : "(unknown)",
                HolderName     = r.BorrowerName,
                CheckedOutAtUtc = r.CheckedOutAtUtc,
                ReturnedAtUtc  = r.ReturnedAtUtc
            })
            .ToList()
            .AsReadOnly();
    }
}
