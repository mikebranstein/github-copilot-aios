using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IWaitlistService
{
    Task<WaitlistEntry> JoinQueueAsync(int equipmentItemId, int userId, string userName, WaitlistTier tier = WaitlistTier.Standard);
    Task<bool> CancelEntryAsync(int entryId, int requestingUserId);
    Task<bool> ConfirmReservationAsync(int entryId, int userId);
    Task AdvanceQueueAsync(int equipmentItemId);
    Task ExpireTimedOutReservationsAsync();
    Task<bool> OverridePositionAsync(int entryId, int newPosition, string reason, string coordinatorName);
    Task<bool> RemoveEntryAsync(int entryId, string reason, string coordinatorName);
    Task<bool> MarkUrgentAsync(int entryId, string coordinatorName);
    Task<List<WaitlistEntry>> GetQueueForItemAsync(int equipmentItemId);
    Task<WaitlistEntry?> GetEntryAsync(int entryId);
    Task<(int Position, string EtaDisplay)> GetPositionAndEtaAsync(int entryId);
    Task<List<(int EquipmentItemId, List<WaitlistEntry> Queue)>> GetAllActiveQueuesAsync();
    Task<List<WaitlistEntry>> GetHistoryForItemAsync(int equipmentItemId);
    Task<List<EquipmentItem>> GetAlternativesForCategoryAsync(int equipmentItemId);
    IReadOnlyList<QueueAuditEvent> GetAuditLog(int equipmentItemId);
}
