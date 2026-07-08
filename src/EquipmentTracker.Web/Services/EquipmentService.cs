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
        _items.Add(new EquipmentItem { Id = _nextItemId++, Name = "Laptop", Category = "Electronics", IsAvailable = true, Status = EquipmentStatus.Available, LifecycleStatus = EquipmentLifecycleStatus.Available });
        _items.Add(new EquipmentItem { Id = _nextItemId++, Name = "Projector", Category = "Electronics", IsAvailable = true, Status = EquipmentStatus.Available, LifecycleStatus = EquipmentLifecycleStatus.Available });
        _items.Add(new EquipmentItem { Id = _nextItemId++, Name = "Whiteboard Marker Set", Category = "Stationery", IsAvailable = true, Status = EquipmentStatus.Available, LifecycleStatus = EquipmentLifecycleStatus.Available });
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
            IsAvailable = true,
            Status = EquipmentStatus.Available,
            LifecycleStatus = EquipmentLifecycleStatus.Available,
            LastUpdatedAtUtc = DateTime.UtcNow
        };
        _items.Add(item);
        return item;
    }

    public bool Checkout(int itemId, string borrowerName, int? borrowerUserId = null, string? conditionNote = null, int? bulkCheckoutInitiatorId = null, int? newSiteId = null)
    {
        var item = GetItem(itemId);
        if (item is null || !item.IsAvailable)
            return false;

        item.IsAvailable = false;
        item.Status = EquipmentStatus.InUse;
        item.LifecycleStatus = EquipmentLifecycleStatus.CheckedOut;
        item.LastUpdatedAtUtc = DateTime.UtcNow;

        if (newSiteId.HasValue)
        {
            item.SiteId = newSiteId.Value;
            item.LastUpdatedAtUtc = DateTime.UtcNow;
        }

        _records.Add(new CheckoutRecord
        {
            Id = _nextRecordId++,
            EquipmentItemId = itemId,
            BorrowerName = borrowerName,
            BorrowerUserId = borrowerUserId,
            CheckedOutAtUtc = DateTime.UtcNow,
            ConditionNote = conditionNote,
            BulkCheckoutInitiatorId = bulkCheckoutInitiatorId
        });

        return true;
    }

    public bool Return(int itemId, string? returnConditionNote = null)
    {
        var item = GetItem(itemId);
        if (item is null || item.IsAvailable)
            return false;

        item.IsAvailable = true;
        item.Status = EquipmentStatus.Available;
        item.LifecycleStatus = EquipmentLifecycleStatus.Available;
        item.LastUpdatedAtUtc = DateTime.UtcNow;

        var record = _records
            .LastOrDefault(r => r.EquipmentItemId == itemId && r.ReturnedAtUtc is null);

        if (record is not null)
        {
            record.ReturnedAtUtc = DateTime.UtcNow;
            if (returnConditionNote is not null)
                record.ReturnConditionNote = returnConditionNote;
        }

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
        var itemNameById = _items.ToDictionary(i => i.Id, i => i.Name);

        return _records
            .OrderByDescending(r => r.CheckedOutAtUtc)
            .Select(r => new CheckoutHistoryEntry
            {
                ItemName = itemNameById.TryGetValue(r.EquipmentItemId, out var name) ? name : "(unknown)",
                HolderName = r.BorrowerName,
                CheckedOutAtUtc = r.CheckedOutAtUtc,
                ReturnedAtUtc = r.ReturnedAtUtc
            })
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<CheckoutHistoryEntry> GetCheckoutHistoryByUser(int userId, int limit = 30)
    {
        var itemNameById = _items.ToDictionary(i => i.Id, i => i.Name);

        return _records
            .Where(r => r.BorrowerUserId == userId)
            .OrderByDescending(r => r.CheckedOutAtUtc)
            .Take(limit)
            .Select(r => new CheckoutHistoryEntry
            {
                ItemName = itemNameById.TryGetValue(r.EquipmentItemId, out var name) ? name : "(unknown)",
                HolderName = r.BorrowerName,
                CheckedOutAtUtc = r.CheckedOutAtUtc,
                ReturnedAtUtc = r.ReturnedAtUtc
            })
            .ToList()
            .AsReadOnly();
    }

    public bool IsIdempotentCheckout(int itemId, int borrowerUserId)
    {
        return _records.Any(r =>
            r.EquipmentItemId == itemId &&
            r.BorrowerUserId == borrowerUserId &&
            r.ReturnedAtUtc is null &&
            (DateTime.UtcNow - r.CheckedOutAtUtc).TotalSeconds <= 60);
    }

    public CheckoutRecord? GetCheckoutRecordById(int recordId) =>
        _records.FirstOrDefault(r => r.Id == recordId);

    public IReadOnlyList<CheckoutRecord> GetAllRawCheckoutRecords() =>
        _records.OrderByDescending(r => r.CheckedOutAtUtc).ToList().AsReadOnly();

    public IReadOnlyList<EquipmentItem> GetItemsBySite(int? siteId)
    {
        var items = siteId.HasValue
            ? _items.Where(i => i.SiteId == siteId.Value)
            : _items;

        return items.ToList().AsReadOnly();
    }

    public IReadOnlyList<EquipmentItem> GetItemsByStatus(EquipmentStatus status) =>
        _items.Where(i => i.Status == status).ToList().AsReadOnly();

    public bool UpdateItemSite(int itemId, int? siteId)
    {
        var item = GetItem(itemId);
        if (item is null)
            return false;

        item.SiteId = siteId;
        item.LastUpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    public bool UpdateItemStatus(int itemId, EquipmentStatus status)
    {
        var item = GetItem(itemId);
        if (item is null)
            return false;

        item.Status = status;
        item.IsAvailable = status == EquipmentStatus.Available;
        item.LifecycleStatus = status switch
        {
            EquipmentStatus.Available => EquipmentLifecycleStatus.Available,
            EquipmentStatus.InUse => EquipmentLifecycleStatus.CheckedOut,
            EquipmentStatus.Maintenance => EquipmentLifecycleStatus.Maintenance,
            _ => item.LifecycleStatus
        };
        item.LastUpdatedAtUtc = DateTime.UtcNow;
        return true;
    }
}
