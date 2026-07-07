using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory approval service (singleton). Manages coordinator approval workflow.
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly List<ApprovalRequest> _requests = new();
    private readonly IEquipmentService _equipmentService;
    private int _nextId = 1;
    private readonly object _lock = new();

    public ApprovalService(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    public ApprovalRequest CreateRequest(int checkoutRecordId, int requestingUserId)
    {
        lock (_lock)
        {
            var request = new ApprovalRequest
            {
                Id = _nextId++,
                CheckoutRecordId = checkoutRecordId,
                RequestingUserId = requestingUserId,
                Status = ApprovalStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow
            };
            _requests.Add(request);
            return request;
        }
    }

    public bool Approve(int requestId, int coordinatorId)
    {
        lock (_lock)
        {
            var request = _requests.FirstOrDefault(r => r.Id == requestId);
            if (request is null || request.Status != ApprovalStatus.Pending)
                return false;

            request.Status = ApprovalStatus.Approved;
            request.ApprovingCoordinatorId = coordinatorId;
            request.DecidedAtUtc = DateTime.UtcNow;
            return true;
        }
    }

    public bool Deny(int requestId, int coordinatorId, string? reason)
    {
        lock (_lock)
        {
            var request = _requests.FirstOrDefault(r => r.Id == requestId);
            if (request is null || request.Status != ApprovalStatus.Pending)
                return false;

            request.Status = ApprovalStatus.Denied;
            request.ApprovingCoordinatorId = coordinatorId;
            request.DenialReason = reason;
            request.DecidedAtUtc = DateTime.UtcNow;

            // Void the checkout record and release the item
            var checkoutRecord = GetCheckoutRecord(request.CheckoutRecordId);
            if (checkoutRecord is not null)
            {
                checkoutRecord.IsVoided = true;
                _equipmentService.Return(checkoutRecord.EquipmentItemId);
            }

            return true;
        }
    }

    public void AutoApproveExpired(TimeSpan timeout)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - timeout;
            var expired = _requests
                .Where(r => r.Status == ApprovalStatus.Pending && r.RequestedAtUtc <= cutoff)
                .ToList();

            foreach (var request in expired)
            {
                request.Status = ApprovalStatus.AutoApproved;
                request.DecidedAtUtc = DateTime.UtcNow;
            }
        }
    }

    public IReadOnlyList<ApprovalRequest> GetPending()
    {
        lock (_lock)
        {
            return _requests.Where(r => r.Status == ApprovalStatus.Pending).ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<ApprovalRequest> GetAll()
    {
        lock (_lock)
        {
            return _requests.ToList().AsReadOnly();
        }
    }

    public ApprovalRequest? GetByCheckoutRecordId(int checkoutRecordId)
    {
        lock (_lock)
        {
            return _requests.FirstOrDefault(r => r.CheckoutRecordId == checkoutRecordId);
        }
    }

    // Helper: get the checkout record from EquipmentService internals via the service's history
    private CheckoutRecord? GetCheckoutRecord(int checkoutRecordId)
    {
        return _equipmentService.GetCheckoutRecordById(checkoutRecordId);
    }
}
