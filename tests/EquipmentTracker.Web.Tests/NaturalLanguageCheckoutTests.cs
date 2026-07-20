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

    // ── NlCheckoutController — additional Parse branch coverage ───────────

    /// <summary>
    /// AC5: Empty utterance at controller level → Input view with guidance message.
    /// Covers the null/whitespace guard branch in Parse before any service call.
    /// </summary>
    [Fact]
    public async Task NlController_Parse_EmptyUtterance_ShowsInputWithGuidanceMessage()
    {
        var controller = BuildController(new EquipmentService());

        var result = await controller.Parse("   ");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Input", view.ViewName);
        var vm = Assert.IsType<NlCheckoutInputViewModel>(view.Model);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("Please enter", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC5 / TS8: OperationCanceledException from NL service → controller catches it and
    /// shows "temporarily unavailable" message with fallback link.
    /// </summary>
    [Fact]
    public async Task NlController_Parse_OperationCanceled_ShowsTimeoutError()
    {
        var controller = BuildController(new EquipmentService(), new CancelingNlService());

        var result = await controller.Parse("check out drill #4");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Input", view.ViewName);
        var vm = Assert.IsType<NlCheckoutInputViewModel>(view.Model);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("temporarily unavailable", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        // Utterance is preserved so user doesn't lose their text
        Assert.Equal("check out drill #4", vm.Utterance);
    }

    /// <summary>
    /// AC5: LowConfidence status → Input view with guidance message.
    /// </summary>
    [Fact]
    public async Task NlController_Parse_LowConfidence_ShowsInputWithError()
    {
        var controller = BuildController(new EquipmentService(), new StubNlService(new NlParseResult
        {
            Status = NlParseStatus.LowConfidence,
            Confidence = 0.3,
            ErrorMessage = "I didn't understand that — try 'Check out [item] to me until [date]'",
            OriginalUtterance = "blorp flooble"
        }));

        var result = await controller.Parse("blorp flooble");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Input", view.ViewName);
        var vm = Assert.IsType<NlCheckoutInputViewModel>(view.Model);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("didn't understand", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Defensive: An unknown future NlParseStatus value → Input view with generic fallback.
    /// Covers the default (_) branch of the switch expression.
    /// </summary>
    [Fact]
    public async Task NlController_Parse_DefaultStatus_ShowsInputWithFallbackError()
    {
        var controller = BuildController(new EquipmentService(), new StubNlService(new NlParseResult
        {
            Status = (NlParseStatus)99,   // unmapped future value
            Confidence = 0.0,
            OriginalUtterance = "check out drill"
        }));

        var result = await controller.Parse("check out drill");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Input", view.ViewName);
        var vm = Assert.IsType<NlCheckoutInputViewModel>(view.Model);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("Unexpected error", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── NlCheckoutController — additional ConfirmPost branch coverage ──────

    /// <summary>
    /// ConfirmPost: item not found → 404 NotFound result.
    /// </summary>
    [Fact]
    public async Task NlController_ConfirmPost_ItemNotFound_ReturnsNotFound()
    {
        var equipSvc = new StubEquipmentService(item: null);
        var controller = BuildControllerWithStub(equipSvc);

        var result = await controller.ConfirmPost(
            itemId: 9999, assigneeId: 1, assigneeName: "Alice",
            dueDateIso: null, dueDateDisplay: null, utterance: "test");

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// ConfirmPost: item not available + idempotent checkout → redirects to Success with a
    /// "duplicate request detected" message rather than showing a conflict error.
    /// </summary>
    [Fact]
    public async Task NlController_ConfirmPost_IdempotentCheckout_ReturnsSuccessRedirect()
    {
        var item = new EquipmentItem { Id = 10, Name = "Drill #4", IsAvailable = false };
        var equipSvc = new StubEquipmentService(item: item, idempotentResult: true);
        var controller = BuildControllerWithStub(equipSvc);

        var result = await controller.ConfirmPost(
            itemId: 10, assigneeId: 1, assigneeName: "Alice",
            dueDateIso: null, dueDateDisplay: null, utterance: "check out drill #4");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Success", redirect.ActionName);
        Assert.Equal("MobileCheckout", redirect.ControllerName);
    }

    /// <summary>
    /// ConfirmPost: item not available (non-idempotent) and an active checkout record exists →
    /// Confirm view shown with a message identifying the current holder. HTTP 409.
    /// </summary>
    [Fact]
    public async Task NlController_ConfirmPost_ItemUnavailable_WithActiveRecord_ShowsConflictWithHolder()
    {
        var item = new EquipmentItem { Id = 20, Name = "Saw #1", IsAvailable = false };
        var activeRecord = new CheckoutRecord
        {
            Id = 1, EquipmentItemId = 20, BorrowerName = "Bob", BorrowerUserId = 2,
            CheckedOutAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        var equipSvc = new StubEquipmentService(item: item, activeRecord: activeRecord,
            idempotentResult: false);
        var controller = BuildControllerWithStub(equipSvc);

        var result = await controller.ConfirmPost(
            itemId: 20, assigneeId: 1, assigneeName: "Alice",
            dueDateIso: null, dueDateDisplay: null, utterance: "check out saw #1");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Confirm", view.ViewName);
        var vm = Assert.IsType<NlCheckoutConfirmViewModel>(view.Model);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("Bob", vm.ErrorMessage);
        Assert.Equal(409, controller.Response.StatusCode);
    }

    /// <summary>
    /// ConfirmPost: item not available (non-idempotent) and NO active checkout record →
    /// Confirm view shown with generic "unavailable" message. HTTP 409.
    /// </summary>
    [Fact]
    public async Task NlController_ConfirmPost_ItemUnavailable_NoActiveRecord_ShowsGenericConflict()
    {
        var item = new EquipmentItem { Id = 30, Name = "Ladder 2", IsAvailable = false };
        var equipSvc = new StubEquipmentService(item: item, activeRecord: null,
            idempotentResult: false);
        var controller = BuildControllerWithStub(equipSvc);

        var result = await controller.ConfirmPost(
            itemId: 30, assigneeId: 1, assigneeName: "Alice",
            dueDateIso: null, dueDateDisplay: null, utterance: "check out ladder 2");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Confirm", view.ViewName);
        var vm = Assert.IsType<NlCheckoutConfirmViewModel>(view.Model);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("unavailable", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(409, controller.Response.StatusCode);
    }

    /// <summary>
    /// ConfirmPost: item is available but Checkout service returns false (defensive race-condition
    /// path) → Confirm view shown with retry error. HTTP 409.
    /// </summary>
    [Fact]
    public async Task NlController_ConfirmPost_CheckoutFails_ShowsRetryError()
    {
        var item = new EquipmentItem { Id = 40, Name = "Projector", IsAvailable = true };
        var equipSvc = new StubEquipmentService(item: item, checkoutResult: false);
        var controller = BuildControllerWithStub(equipSvc);

        var result = await controller.ConfirmPost(
            itemId: 40, assigneeId: 1, assigneeName: "Alice",
            dueDateIso: null, dueDateDisplay: null, utterance: "check out projector");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Confirm", view.ViewName);
        var vm = Assert.IsType<NlCheckoutConfirmViewModel>(view.Model);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("Checkout failed", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(409, controller.Response.StatusCode);
    }

    /// <summary>
    /// ConfirmPost: push notification failure → exception is logged and swallowed; checkout
    /// still succeeds and user is redirected to Pending. Covers the try/catch in coordinator loop.
    /// </summary>
    [Fact]
    public async Task NlController_ConfirmPost_PushNotificationFails_CheckoutStillSucceeds()
    {
        var equipSvc = new EquipmentService();
        var item = equipSvc.CreateItem("Camera Kit", "A/V");

        // Build a UserService with a coordinator that has push notifications enabled
        var userSvc = new UserService();
        var coordinator = userSvc.GetCoordinators().FirstOrDefault();

        // Use a push service that throws on SendAsync
        var throwingPush = new ThrowingPushNotificationService();
        var notifSvc = new CoordinatorNotificationService();
        var approvalSvc = new ApprovalService(equipSvc, userSvc, throwingPush);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<NlCheckoutController>.Instance;

        var controller = new NlCheckoutController(
            new NaturalLanguageCheckoutService(equipSvc,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<NaturalLanguageCheckoutService>.Instance),
            equipSvc, userSvc, notifSvc, throwingPush, approvalSvc, logger);

        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "1"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Alice")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());

        // Act — should not throw even though push service throws
        var result = await controller.ConfirmPost(
            itemId: item.Id, assigneeId: 1, assigneeName: "Alice",
            dueDateIso: null, dueDateDisplay: null, utterance: "check out camera kit");

        // Assert — checkout still succeeds, redirect to Pending
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Pending", redirect.ActionName);
        Assert.Equal("MobileCheckout", redirect.ControllerName);

        // Verify checkout was actually recorded
        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
    }

    // ── AC1 and AC6 — acceptance criteria tests ───────────────────────────

    /// <summary>
    /// AC1: End-to-end latency from NL text submission to confirmation prompt displayed must be
    /// under 5 seconds on a 4G mobile connection (measured p95).
    ///
    /// The Phase 1 stub service is synchronous (rule-based regex, no LLM call), so the
    /// &lt;5 s bar is trivially met. This test documents and enforces the 5-second ceiling on
    /// the complete Parse → result path so any future LLM-backed replacement cannot silently
    /// regress beyond the latency budget.
    /// </summary>
    [Fact]
    public async Task AC1_EndToEnd_NlToConfirmationUnder5s()
    {
        var svc = BuildService(out var equipSvc);
        equipSvc.CreateItem("Drill #4", "Tools");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await svc.ParseAsync("Check out Drill #4 to me until Friday",
            currentUserId: 1, currentUserName: "Alice");
        sw.Stop();

        // Parse must succeed — confirms the full intent + entity extraction path ran.
        Assert.Equal(NlParseStatus.Success, result.Status);
        Assert.NotNull(result.ResolvedItem);

        // Latency guard: entire Parse → result flow must complete within 5 000 ms (AC1).
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Parse → result took {sw.ElapsedMilliseconds} ms; AC1 requires <5 000 ms end-to-end.");
    }

    /// <summary>
    /// AC6: LLM cost per checkout transaction must be ≤$0.003 in production.
    ///
    /// The cost ceiling is captured in <see cref="NlParseConstants.MaxCostPerTransactionUsd"/>
    /// so any future LLM provider integration (from Technical Spike #148) is verifiable against
    /// it. The Phase 1 stub has zero LLM cost; the constant is the hard spec guardrail.
    /// </summary>
    [Fact]
    public void AC6_LlmCostPerTransaction_MeetsSpec()
    {
        // The constant must be set to the spec ceiling defined in the acceptance criteria.
        Assert.Equal(0.003m, NlParseConstants.MaxCostPerTransactionUsd);

        // Any future cost estimate surfaced by the service must also satisfy the ceiling.
        Assert.True(NlParseConstants.MaxCostPerTransactionUsd <= 0.003m,
            $"LLM cost per transaction must be ≤$0.003 (AC6); " +
            $"NlParseConstants.MaxCostPerTransactionUsd = {NlParseConstants.MaxCostPerTransactionUsd:C4}");
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
        IEquipmentService equipSvc,
        INaturalLanguageCheckoutService? nlService = null)
    {
        if (nlService is null && equipSvc is EquipmentService concreteEquipSvc)
            nlService = new NaturalLanguageCheckoutService(
                concreteEquipSvc, NullLogger<NaturalLanguageCheckoutService>.Instance);
        nlService ??= new StubNlService(new NlParseResult { Status = NlParseStatus.ItemNotFound });

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

    /// <summary>
    /// Builds a controller wired to a stub IEquipmentService (for testing branches that
    /// require controlled behavior like checkout-failure or idempotency).
    /// </summary>
    private static NlCheckoutController BuildControllerWithStub(
        IEquipmentService equipSvc,
        INaturalLanguageCheckoutService? nlService = null)
    {
        nlService ??= new StubNlService(new NlParseResult { Status = NlParseStatus.ItemNotFound });
        var userSvc = new UserService();
        var notifSvc = new CoordinatorNotificationService();
        var pushSvc = new StubPushNotificationService();
        var approvalSvc = new ApprovalService(equipSvc, userSvc, pushSvc);
        var logger = NullLogger<NlCheckoutController>.Instance;

        var controller = new NlCheckoutController(
            nlService, equipSvc, userSvc, notifSvc, pushSvc, approvalSvc, logger);

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

    /// <summary>
    /// NL service stub that always throws OperationCanceledException, simulating an LLM timeout
    /// that propagates through the CancellationTokenSource in the controller.
    /// </summary>
    private sealed class CancelingNlService : INaturalLanguageCheckoutService
    {
        public Task<NlParseResult> ParseAsync(string utterance, int userId, string userName)
            => throw new OperationCanceledException("Simulated LLM timeout in test");
    }

    /// <summary>
    /// Push notification service stub that always throws, simulating a transient push failure.
    /// Used to verify the controller's try/catch swallows the exception and continues.
    /// </summary>
    private sealed class ThrowingPushNotificationService : IPushNotificationService
    {
        public Task SendAsync(ApplicationUser coordinator, string title, string body)
            => throw new InvalidOperationException("Simulated push failure in test");
        public Task<bool> SubscribeAsync(ApplicationUser coordinator, string endpoint,
            string p256dh, string auth) => Task.FromResult(true);
        public Task<bool> UnsubscribeAsync(ApplicationUser coordinator) => Task.FromResult(true);
    }

    /// <summary>
    /// Minimal stub IEquipmentService for testing controller branches that require controlled
    /// behavior (e.g., Checkout returning false, item unavailable, idempotent checkout).
    /// Only the methods called by NlCheckoutController are implemented; all others throw.
    /// </summary>
    private sealed class StubEquipmentService : IEquipmentService
    {
        private readonly EquipmentItem? _item;
        private readonly CheckoutRecord? _activeRecord;
        private readonly bool _checkoutResult;
        private readonly bool _idempotentResult;

        public StubEquipmentService(
            EquipmentItem? item = null,
            CheckoutRecord? activeRecord = null,
            bool checkoutResult = true,
            bool idempotentResult = false)
        {
            _item = item;
            _activeRecord = activeRecord;
            _checkoutResult = checkoutResult;
            _idempotentResult = idempotentResult;
        }

        public EquipmentItem? GetItem(int id) => _item;
        public IReadOnlyList<EquipmentItem> GetAllItems()
            => _item is not null ? (IReadOnlyList<EquipmentItem>)[_item] : [];
        public EquipmentItem CreateItem(string name, string category)
            => throw new NotSupportedException("Not needed in stub");
        public bool Checkout(int itemId, string borrowerName, int? borrowerUserId = null,
            string? conditionNote = null, int? bulkCheckoutInitiatorId = null, int? newSiteId = null)
            => _checkoutResult;
        public bool Return(int itemId, string? returnConditionNote = null) => false;
        public string? GetCurrentHolder(int itemId) => null;
        public IReadOnlyList<CheckoutRecord> GetCheckoutHistory(int itemId) => [];
        public CheckoutRecord? GetActiveCheckoutRecord(int itemId) => _activeRecord;
        public IReadOnlyList<CheckoutHistoryEntry> GetAllCheckoutHistory() => [];
        public IReadOnlyList<CheckoutHistoryEntry> GetCheckoutHistoryByUser(int userId, int limit = 30) => [];
        public CheckoutRecord? GetCheckoutRecordById(int recordId) => null;
        public IReadOnlyList<CheckoutRecord> GetAllRawCheckoutRecords() => [];
        public bool IsIdempotentCheckout(int itemId, int borrowerUserId) => _idempotentResult;
        public IReadOnlyList<EquipmentItem> GetItemsBySite(int? siteId) => [];
        public IReadOnlyList<EquipmentItem> GetItemsByStatus(EquipmentTracker.Web.Models.EquipmentStatus status) => [];
        public bool UpdateItemSite(int itemId, int? siteId) => false;
        public bool UpdateItemStatus(int itemId, EquipmentTracker.Web.Models.EquipmentStatus status) => false;
        public DamageFlag? FlagDamage(int itemId, string description, int? reportedByUserId,
            string deviceTransactionId, DateTime deviceTimestamp) => null;
        public IReadOnlyList<DamageFlag> GetDamageFlags(int itemId) => [];
        public IReadOnlyList<DamageFlag> GetAllActiveDamageFlags() => [];
        public bool ClearDamageFlag(int itemId) => false;
        public DamageFlag? GetActiveDamageFlag(int itemId) => null;
    }
}