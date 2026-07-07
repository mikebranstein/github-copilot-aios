using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Field Manager scan-to-cart bulk checkout and return flow.
///
/// Routes:
///   GET  /mobile/field-bulk-checkout/scan       — scan-to-cart screen (checkout)
///   POST /mobile/field-bulk-checkout/add-item   — add scanned item to checkout cart
///   POST /mobile/field-bulk-checkout/skip-item  — mark conflicted item as skipped
///   POST /mobile/field-bulk-checkout/remove-item — remove item from checkout cart
///   POST /mobile/field-bulk-checkout/clear      — clear the checkout cart
///   POST /mobile/field-bulk-checkout/confirm    — commit bulk checkout
///   GET  /mobile/field-bulk-checkout/success    — success confirmation screen
///
///   GET  /mobile/field-bulk-return/scan         — scan-to-cart screen (return)
///   POST /mobile/field-bulk-return/add-item     — add scanned item to return cart
///   POST /mobile/field-bulk-return/remove-item  — remove item from return cart
///   POST /mobile/field-bulk-return/clear        — clear the return cart
///   POST /mobile/field-bulk-return/confirm      — commit bulk return
///   GET  /mobile/field-bulk-return/success      — success confirmation screen
///
/// Added for Issue #114 — Bulk Checkout and Return Operations for Field Teams.
/// </summary>
[Authorize]
[Route("mobile/field-bulk-checkout")]
public class FieldBulkCheckoutController : Controller
{
    private readonly IFieldBulkCheckoutService _fieldBulkCheckoutService;
    private readonly IEquipmentService _equipmentService;
    private readonly IUserService _userService;

    public FieldBulkCheckoutController(
        IFieldBulkCheckoutService fieldBulkCheckoutService,
        IEquipmentService equipmentService,
        IUserService userService)
    {
        _fieldBulkCheckoutService = fieldBulkCheckoutService;
        _equipmentService = equipmentService;
        _userService = userService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    private string CurrentUserName =>
        User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

    // ── Checkout ─────────────────────────────────────────────────────────────

    // GET /mobile/field-bulk-checkout/scan
    [HttpGet("scan")]
    public IActionResult Scan()
    {
        var userId = CurrentUserId;
        var cart = _fieldBulkCheckoutService.GetCheckoutCart(userId);
        var borrowers = _userService.GetBorrowers().ToList();

        var vm = new FieldBulkCheckoutCartViewModel
        {
            Cart = cart,
            Borrowers = borrowers,
            SelectedBorrowerId = userId,
            SelectedBorrowerName = CurrentUserName
        };
        return View(vm);
    }

    // POST /mobile/field-bulk-checkout/add-item
    // Returns JSON for Ajax/HTMX scan response (glove-friendly UX — no page reload).
    [HttpPost("add-item")]
    [ValidateAntiForgeryToken]
    public IActionResult AddItem([FromForm] int itemId)
    {
        var userId = CurrentUserId;

        if (itemId <= 0)
            return BadRequest(new { error = "Invalid item ID." });

        var cartItem = _fieldBulkCheckoutService.AddItemToCheckoutCart(userId, itemId);

        if (cartItem is null)
        {
            var cart = _fieldBulkCheckoutService.GetCheckoutCart(userId);
            // Cart full?
            if (cart.ItemCount >= Models.BulkCart.MaxItems)
                return StatusCode(422, new { error = $"Cart is full (maximum {Models.BulkCart.MaxItems} items)." });

            return NotFound(new { error = $"Equipment item {itemId} not found." });
        }

        var updatedCart = _fieldBulkCheckoutService.GetCheckoutCart(userId);

        return Json(new
        {
            success = true,
            itemId = cartItem.EquipmentItemId,
            itemName = cartItem.ItemName,
            category = cartItem.Category,
            hasConflict = cartItem.HasConflict,
            conflictHolderName = cartItem.ConflictHolderName,
            cartCount = updatedCart.ItemCount
        });
    }

    // POST /mobile/field-bulk-checkout/skip-item
    [HttpPost("skip-item")]
    [ValidateAntiForgeryToken]
    public IActionResult SkipItem([FromForm] int itemId)
    {
        _fieldBulkCheckoutService.SkipConflictedItem(CurrentUserId, itemId);
        var cart = _fieldBulkCheckoutService.GetCheckoutCart(CurrentUserId);
        return Json(new { success = true, cartCount = cart.ItemCount });
    }

    // POST /mobile/field-bulk-checkout/remove-item
    [HttpPost("remove-item")]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveItem([FromForm] int itemId)
    {
        _fieldBulkCheckoutService.RemoveItemFromCheckoutCart(CurrentUserId, itemId);
        var cart = _fieldBulkCheckoutService.GetCheckoutCart(CurrentUserId);
        return Json(new { success = true, cartCount = cart.ItemCount });
    }

    // POST /mobile/field-bulk-checkout/clear
    [HttpPost("clear")]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        _fieldBulkCheckoutService.ClearCheckoutCart(CurrentUserId);
        return RedirectToAction(nameof(Scan));
    }

    // POST /mobile/field-bulk-checkout/confirm
    [HttpPost("confirm")]
    [ValidateAntiForgeryToken]
    public IActionResult Confirm(
        [FromForm] int borrowerUserId,
        [FromForm] string borrowerName)
    {
        var userId = CurrentUserId;
        var cart = _fieldBulkCheckoutService.GetCheckoutCart(userId);

        if (cart.ItemCount == 0)
        {
            TempData["ErrorMessage"] = "Cart is empty — nothing to check out.";
            return RedirectToAction(nameof(Scan));
        }

        // Resolve borrower
        int effectiveBorrowerId = borrowerUserId > 0 ? borrowerUserId : userId;
        string effectiveBorrowerName = borrowerName;

        if (string.IsNullOrWhiteSpace(effectiveBorrowerName))
        {
            var user = _userService.GetById(effectiveBorrowerId);
            effectiveBorrowerName = user?.Username ?? CurrentUserName;
        }

        var result = _fieldBulkCheckoutService.ConfirmBulkCheckout(
            userId,
            effectiveBorrowerId,
            effectiveBorrowerName);

        TempData["BulkCheckoutBatchId"] = result.BatchTransactionId;
        TempData["BulkCheckoutSuccessCount"] = result.SuccessCount;
        TempData["BulkCheckoutFailedCount"] = result.FailedCount;
        TempData["BulkCheckoutBorrowerName"] = effectiveBorrowerName;

        return RedirectToAction(nameof(Success));
    }

    // GET /mobile/field-bulk-checkout/success
    [HttpGet("success")]
    public IActionResult Success()
    {
        ViewBag.BatchId = TempData["BulkCheckoutBatchId"] as string;
        ViewBag.SuccessCount = TempData["BulkCheckoutSuccessCount"];
        ViewBag.FailedCount = TempData["BulkCheckoutFailedCount"];
        ViewBag.BorrowerName = TempData["BulkCheckoutBorrowerName"] as string;
        return View();
    }
}

/// <summary>
/// Field Manager bulk RETURN flow. Separate route prefix to keep concerns distinct.
/// Added for Issue #114.
/// </summary>
[Authorize]
[Route("mobile/field-bulk-return")]
public class FieldBulkReturnController : Controller
{
    private readonly IFieldBulkCheckoutService _fieldBulkCheckoutService;
    private readonly IEquipmentService _equipmentService;

    public FieldBulkReturnController(
        IFieldBulkCheckoutService fieldBulkCheckoutService,
        IEquipmentService equipmentService)
    {
        _fieldBulkCheckoutService = fieldBulkCheckoutService;
        _equipmentService = equipmentService;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    // GET /mobile/field-bulk-return/scan
    [HttpGet("scan")]
    public IActionResult Scan()
    {
        var cart = _fieldBulkCheckoutService.GetReturnCart(CurrentUserId);
        var vm = new FieldBulkReturnCartViewModel { Cart = cart };
        return View(vm);
    }

    // POST /mobile/field-bulk-return/add-item
    [HttpPost("add-item")]
    [ValidateAntiForgeryToken]
    public IActionResult AddItem([FromForm] int itemId)
    {
        var userId = CurrentUserId;

        if (itemId <= 0)
            return BadRequest(new { error = "Invalid item ID." });

        var cartItem = _fieldBulkCheckoutService.AddItemToReturnCart(userId, itemId);

        if (cartItem is null)
        {
            // Could be: item not found, already available (returned), or cart full
            var item = _equipmentService.GetItem(itemId);
            if (item is null)
                return NotFound(new { error = $"Equipment item {itemId} not found." });

            if (item.IsAvailable)
                return StatusCode(422, new { error = $"'{item.Name}' is already available — not currently checked out." });

            var cart2 = _fieldBulkCheckoutService.GetReturnCart(userId);
            if (cart2.ItemCount >= Models.BulkCart.MaxItems)
                return StatusCode(422, new { error = $"Return cart is full (maximum {Models.BulkCart.MaxItems} items)." });

            return StatusCode(422, new { error = "Cannot add item to return cart." });
        }

        var cart = _fieldBulkCheckoutService.GetReturnCart(userId);

        return Json(new
        {
            success = true,
            itemId = cartItem.EquipmentItemId,
            itemName = cartItem.ItemName,
            category = cartItem.Category,
            currentHolder = cartItem.ConflictHolderName,
            cartCount = cart.ItemCount
        });
    }

    // POST /mobile/field-bulk-return/remove-item
    [HttpPost("remove-item")]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveItem([FromForm] int itemId)
    {
        _fieldBulkCheckoutService.RemoveItemFromReturnCart(CurrentUserId, itemId);
        var cart = _fieldBulkCheckoutService.GetReturnCart(CurrentUserId);
        return Json(new { success = true, cartCount = cart.ItemCount });
    }

    // POST /mobile/field-bulk-return/clear
    [HttpPost("clear")]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        _fieldBulkCheckoutService.ClearReturnCart(CurrentUserId);
        return RedirectToAction(nameof(Scan));
    }

    // POST /mobile/field-bulk-return/confirm
    [HttpPost("confirm")]
    [ValidateAntiForgeryToken]
    public IActionResult Confirm()
    {
        var userId = CurrentUserId;
        var cart = _fieldBulkCheckoutService.GetReturnCart(userId);

        if (cart.ItemCount == 0)
        {
            TempData["ErrorMessage"] = "Return cart is empty — nothing to return.";
            return RedirectToAction(nameof(Scan));
        }

        var result = _fieldBulkCheckoutService.ConfirmBulkReturn(userId);

        TempData["BulkReturnBatchId"] = result.BatchTransactionId;
        TempData["BulkReturnSuccessCount"] = result.SuccessCount;
        TempData["BulkReturnFailedCount"] = result.FailedCount;

        return RedirectToAction(nameof(Success));
    }

    // GET /mobile/field-bulk-return/success
    [HttpGet("success")]
    public IActionResult Success()
    {
        ViewBag.BatchId = TempData["BulkReturnBatchId"] as string;
        ViewBag.SuccessCount = TempData["BulkReturnSuccessCount"];
        ViewBag.FailedCount = TempData["BulkReturnFailedCount"];
        return View();
    }
}
