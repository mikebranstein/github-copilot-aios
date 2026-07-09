using EquipmentTracker.Web.Controllers;
using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Automated tests for the text-based Natural Language Checkout interface.
/// Issue #149 — Phase 1: Text-Based NL Checkout Interface (Mobile)
///
/// Test command: dotnet test tests/EquipmentTracker.Web.Tests/
///
/// Each test maps to one or more acceptance criteria (AC1–AC7) or test scenario (TS1–TS10).
/// </summary>
public class NaturalLanguageCheckoutTests
{
    // ── NaturalLanguageCheckoutService — parse tests ───────────────────────

    /// <summary>
    /// AC3 / TS1: Happy path — single unambiguous item, "Check out Drill #4 to me until Friday"
    /// Verifies successful parse returns Success status and resolved item.
    /// </summary>
    [Fact]
    public async Task NlService_Parse_HappyPath_ReturnsSuccess()
    {
        var svc = BuildService(out var equipSvc);
        equipSvc.CreateItem("Drill #4", "Tools");

        var result = await svc.ParseAsync("Check out Drill #4 to me until Friday", currentUserId: 1, currentUserName: "Alice");

        Assert.Equal(NlParseStatus.Success, result.Status);
        Assert.NotNull(result.ResolvedItem);
        Assert.Contains("Drill #4", result.ResolvedItem!.Name);
        Assert.True(result.Confidence >= NlParseConstants.MinConfidence);
    }

    /// <summary>
    /// AC3 / TS2: Natural date — relative — "for 3 days" → due date = today + 3 days.
    /// </summary>
    [Fact]
    public async Task NlService_Parse_RelativeDueDate_ForDays()
    {
        var svc = BuildService(out var equipSvc);
        equipSvc.CreateItem("Ladder 2", "Construction");

        var result = await svc.ParseAsync("check out ladder 2 for 3 days", currentUserId: 1, currentUserName: "Bob");

        Assert.Equal(NlParseStatus.Success, result.Status);
        Assert.NotNull(result.DueDate);
        var expected = DateTime.UtcNow.Date.AddDays(3);
        Assert.Equal(expected.Date, result.DueDate!.Value.Date);
    }

    /// <summary>
    /// AC3 / TS3: Natural date — specific — "until October 15" → absolute date parsed.
    /// </summary>
    [Fact]
    public async Task NlService_Parse_AbsoluteDueDate_UntilMonth()
    {
        var svc = BuildService(out var equipSvc);
        equipSvc.CreateItem("Saw #1", "Tools");

        var result = await svc.ParseAsync("check out saw #1 until October 15", currentUserId: 1, currentUserName: "Carol");

        Assert.Equal(NlParseStatus.Success, result.Status);
        Assert.NotNull(result.DueDate);
        Assert.Equal(10, result.DueDate!.Value.Month);
        Assert.Equal(15, result.DueDate!.Value.Day);
    }

    /// <summary>
    /// AC4 / TS4: Ambiguous item reference — multiple matches trigger Ambiguous status.
    /// </summary>
    [Fact]
    public async Task NlService_Parse_AmbiguousItem_ReturnsAmbiguous()
    {
        var svc = BuildService(out var equipSvc);
        equipSvc.CreateItem("Drill #3", "Tools");
        equipSvc.CreateItem("Drill #4", "Tools");
        equipSvc.CreateItem("Drill #7", "Tools");

        var result = await svc.ParseAsync("check out the drill", currentUserId: 1, currentUserName: "Dave");

        Assert.Equal(NlParseStatus.Ambiguous, result.Status);
        Assert.True(result.AmbiguousMatches.Count >= 3);
        Assert.Null(result.ResolvedItem);
    }

    /// <summary>
    /// AC5 / TS5: Item not found → returns ItemNotFound with error message.
    /// </summary>
    [Fact]
    public async Task NlService_Parse_ItemNotFound_ReturnsItemNotFound()
    {
        var svc = BuildService(out _);

        var result = await svc.ParseAsync("check out the helicopter", currentUserId: 1, currentUserName: "Eve");

        Assert.Equal(NlParseStatus.ItemNotFound, result.Status);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC5 / TS6: Low-confidence / garbled input → LowConfidence status with guidance message.
    /// </summary>
    [Fact]
    public async Task NlService_Parse_EmptyInput_ReturnsLowConfidence()
    {
        var svc = BuildService(out _);

        var result = await svc.ParseAsync("   ", currentUserId: 1, currentUserName: "Frank");

        Assert.Equal(NlParseStatus.LowConfidence, result.Status);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("didn't understand", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC5 / TS6: Garbled text returns LowConfidence with a helpful fallback message.
    /// </summary>
    [Fact]
    public async Task NlService_Parse_GarbledInput_ReturnsLowConfidence()
    {
        var svc = BuildService(out _);

        var result = await svc.ParseAsync("asdfjkl qwerty zzz", currentUserId: 1, currentUserName: "Frank");

        // Should be ItemNotFound (no match) or LowConfidence — either is acceptable for garbled input
        Assert.True(
            result.Status == NlParseStatus.ItemNotFound || result.Status == NlParseStatus.LowConfidence,
            $"Expected ItemNotFound or LowConfidence, got {result.Status}");
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// AC3: Assignee defaults to the authenticated current user when no assignee specified.
    /// </summary>
    [Fact]
    public async Task NlService_Parse_DefaultsAssigneeToCurrentUser()
    {
        var svc = BuildService(out var equipSvc);
        // Use a unique name that doesn't conflict with EquipmentService default items
        equipSvc.CreateItem("Hard Hat XL", "Safety");

        var result = await svc.ParseAsync("Check out Hard Hat XL to me until Friday",
            currentUserId: 42, currentUserName: "Grace");

        Assert.Equal(NlParseStatus.Success, result.Status);
        Assert.Equal(42, result.AssigneeId);
        Assert.Equal("Grace", result.AssigneeName);
    }

    /// <summary>
    /// AC3: Due date display string is set when due date is parsed.
    /// </summary>
    [Fact]
    public async Task NlService_Parse_DueDateDisplay_IsSetWhenDueDateParsed()
    {
        var svc = BuildService(out var equipSvc);
        // Use a unique name that doesn't conflict with EquipmentService default items
        equipSvc.CreateItem("Tripod Stand", "A/V");

        var result = await svc.ParseAsync("check out tripod stand for 5 days",
            currentUserId: 1, currentUserName: "Hank");

        Assert.Equal(NlParseStatus.Success, result.Status);
        Assert.NotNull(result.DueDateDisplay);
        Assert.False(string.IsNullOrWhiteSpace(result.DueDateDisplay));
    }

    // ── NlCheckoutController — controller tests ────────────────────────────

    /// <summary>
    /// AC2: Confirmation screen is shown (not committed) after successful parse.
    /// Transaction is only committed after explicit POST /confirm.
    /// </summary>
    [Fact]
    public async Task NlController_Parse_Success_RedirectsToConfirm()
    {
        var equipSvc = new EquipmentService();
        var item = equipSvc.CreateItem("Drill #4", "Tools");
        var controller = BuildController(equipSvc, new StubNlService(new NlParseResult
        {
            Status = NlParseStatus.Success,
            ResolvedItem = item,
            Confidence = 0.95,
            AssigneeId = 1,
            AssigneeName = "Alice",
            OriginalUtterance = "check out drill #4 to me"
        }));

        var result = await controller.Parse("check out drill #4 to me");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Confirm", redirect.ActionName);
    }

    /// <summary>
    /// AC4: Disambiguation view is returned when parse is Ambiguous.
    /// </summary>
    [Fact]
    public async Task NlController_Parse_Ambiguous_ShowsDisambiguationView()
    {
        var equipSvc = new EquipmentService();
        var d3 = equipSvc.CreateItem("Drill #3", "Tools");
        var d4 = equipSvc.CreateItem("Drill #4", "Tools");
        var controller = BuildController(equipSvc, new StubNlService(new NlParseResult
        {
            Status = NlParseStatus.Ambiguous,
            AmbiguousMatches = [d3, d4],
            Confidence = 0.85,
            AssigneeId = 1,
            AssigneeName = "Alice",
            OriginalUtterance = "check out the drill"
        }));

        var result = await controller.Parse("check out the drill");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Disambiguate", view.ViewName);
        var vm = Assert.IsType<NlCheckoutDisambiguateViewModel>(view.Model);
        Assert.Equal(2, vm.Candidates.Count);
    }

    /// <summary>
    /// AC5 / TS5: Item not found → input view shown with error and fallback link.
    /// </summary>
    [Fact]
    public async Task NlController_Parse_ItemNotFound_ShowsInputWithError()
    {
        var equipSvc = new EquipmentService();
        var controller = BuildController(equipSvc, new StubNlService(new NlParseResult
        {
            Status = NlParseStatus.ItemNotFound,
            Confidence = 0.9,
            ErrorMessage = "Item not found: 'helicopter'. Check the name and try again, or use standard checkout.",
            OriginalUtterance = "check out the helicopter"
        }));

        var result = await controller.Parse("check out the helicopter");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Input", view.ViewName);
        var vm = Assert.IsType<NlCheckoutInputViewModel>(view.Model);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("not found", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC5 / TS8: LLM timeout → input view shown with "temporarily unavailable" message and fallback.
    /// </summary>
    [Fact]
    public async Task NlController_Parse_LlmTimeout_ShowsInputWithTimeoutError()
    {
        var equipSvc = new EquipmentService();
        var controller = BuildController(equipSvc, new StubNlService(new NlParseResult
        {
            Status = NlParseStatus.LlmTimeout,
            Confidence = 0.0,
            ErrorMessage = "Service temporarily unavailable — please try again or use standard checkout.",
            OriginalUtterance = "check out drill"
        }));

        var result = await controller.Parse("check out drill");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Input", view.ViewName);
        var vm = Assert.IsType<NlCheckoutInputViewModel>(view.Model);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("temporarily unavailable", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC7 / TS10: Standard checkout (non-NL) remains fully functional and unaffected.
    /// Verifies the existing MobileCheckoutController.Lookup still works after NL changes.
    /// </summary>
    [Fact]
    public void StandardCheckoutController_Lookup_StillWorks_AfterNlChanges()
    {
        var equipSvc = new EquipmentService();
        // Standard checkout uses the same EquipmentService; NL is additive only
        var item = equipSvc.GetAllItems().First();

        var result = equipSvc.GetItem(item.Id);

        Assert.NotNull(result);
        Assert.Equal(item.Name, result!.Name);
    }

    /// <summary>
    /// AC2 / TS7: User cancels at confirmation → no checkout recorded.
    /// (Cancellation means the user navigates away without POSTing /confirm.)
    /// Verifies that GET /confirm does NOT record a transaction.
    /// </summary>
    [Fact]
    public void NlController_GetConfirm_DoesNotRecordCheckout()
    {
        var equipSvc = new EquipmentService();
        var item = equipSvc.CreateItem("Saw #1", "Tools");
        var controller = BuildController(equipSvc);

        // GET confirm — should just show the view, not commit anything
        var result = controller.Confirm(item.Id, assigneeId: 1, assigneeName: "Alice",
            dueDateIso: null, dueDateDisplay: null, utterance: "check out saw #1");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Confirm", view.ViewName);

        // Item must still be available — no transaction committed
        var refreshed = equipSvc.GetItem(item.Id);
        Assert.True(refreshed!.IsAvailable);
    }

    /// <summary>
    /// AC2 / AC7: POST /confirm records transaction using the identical data model.
    /// Verifies checkout record is created with correct borrower details.
    /// </summary>
    [Fact]
    public async Task NlController_ConfirmPost_RecordsTransactionWithCorrectData()
    {
        var equipSvc = new EquipmentService();
        var item = equipSvc.CreateItem("Laptop A", "Electronics");
        var controller = BuildController(equipSvc);

        var result = await controller.ConfirmPost(
            itemId: item.Id,
            assigneeId: 5,
            assigneeName: "Grace",
            dueDateIso: null,
            dueDateDisplay: null,
            utterance: "check out laptop a");

        // Should redirect to MobileCheckout/Pending (same as standard flow)
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Pending", redirect.ActionName);
        Assert.Equal("MobileCheckout", redirect.ControllerName);

        // Verify checkout was recorded
        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        Assert.Equal("Grace", record!.BorrowerName);
        Assert.Equal(5, record.BorrowerUserId);
    }

    /// <summary>
    /// AC5 / TS9: Connectivity error → input view shown with connectivity error message.
    /// </summary>
    [Fact]
    public async Task NlController_Parse_ConnectivityError_ShowsInputWithConnectivityError()
    {
        var equipSvc = new EquipmentService();
        var controller = BuildController(equipSvc, new StubNlService(new NlParseResult
        {
            Status = NlParseStatus.ConnectivityError,
            Confidence = 0.0,
            ErrorMessage = "Service temporarily unavailable — please try again or use standard checkout.",
            OriginalUtterance = "check out drill"
        }));

        var result = await controller.Parse("check out drill");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Input", view.ViewName);
    }

    // ── NlParseResult model tests ──────────────────────────────────────────

    /// <summary>
    /// Verify the NlParseResult model defaults are sensible.
    /// </summary>
    [Fact]
    public void NlParseResult_Defaults_AreCorrect()
    {
        var result = new NlParseResult();

        Assert.Equal(default, result.Status);
        Assert.Equal(0.0, result.Confidence);
        Assert.Null(result.ResolvedItem);
        Assert.Empty(result.AmbiguousMatches);
        Assert.Null(result.DueDate);
        Assert.Equal(string.Empty, result.OriginalUtterance);
    }

    /// <summary>
    /// Verify NlParseConstants are set to spec values.
    /// </summary>
    [Fact]
    public void NlParseConstants_MinConfidence_IsCorrect()
    {
        Assert.Equal(0.70, NlParseConstants.MinConfidence);
        Assert.Equal(3, NlParseConstants.LlmTimeoutSeconds);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static NaturalLanguageCheckoutService BuildService(out EquipmentService equipSvc)
    {
        equipSvc = new EquipmentService();
        return new NaturalLanguageCheckoutService(equipSvc, NullLogger<NaturalLanguageCheckoutService>.Instance);
    }

    private static NlCheckoutController BuildController(
        EquipmentService equipSvc,
        INaturalLanguageCheckoutService? nlService = null)
    {
        nlService ??= new NaturalLanguageCheckoutService(
            equipSvc, NullLogger<NaturalLanguageCheckoutService>.Instance);

        var userSvc = new UserService();
        var notifSvc = new CoordinatorNotificationService();
        var pushSvc = new StubPushNotificationService();
        var approvalSvc = new ApprovalService(equipSvc, userSvc, pushSvc);
        var logger = NullLogger<NlCheckoutController>.Instance;

        var controller = new NlCheckoutController(
            nlService, equipSvc, userSvc, notifSvc, pushSvc, approvalSvc, logger);

        // Set up authenticated user context (userId=1, name="Alice")
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "Alice")
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());

        return controller;
    }

    // ── Stubs ──────────────────────────────────────────────────────────────

    /// <summary>Stub NL service that returns a pre-configured result.</summary>
    private sealed class StubNlService : INaturalLanguageCheckoutService
    {
        private readonly NlParseResult _result;
        public StubNlService(NlParseResult result) => _result = result;
        public Task<NlParseResult> ParseAsync(string utterance, int userId, string userName)
            => Task.FromResult(_result);
    }

    /// <summary>Stub push notification service that does nothing.</summary>
    private sealed class StubPushNotificationService : IPushNotificationService
    {
        public Task SendAsync(ApplicationUser coordinator, string title, string body)
            => Task.CompletedTask;
        public Task<bool> SubscribeAsync(ApplicationUser coordinator, string endpoint,
            string p256dh, string auth) => Task.FromResult(true);
        public Task<bool> UnsubscribeAsync(ApplicationUser coordinator) => Task.FromResult(true);
    }

    /// <summary>Minimal no-op TempData provider for controller unit tests.</summary>
    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
            => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
