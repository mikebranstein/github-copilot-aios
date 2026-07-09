using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// ViewModel for the NL checkout input screen.
/// Issue #149 — Phase 1: Text-Based NL Checkout Interface (Mobile)
/// </summary>
public class NlCheckoutInputViewModel
{
    /// <summary>Pre-populated utterance (e.g. from a previous failed parse that the user edits).</summary>
    public string? Utterance { get; set; }

    /// <summary>Error message shown after a failed parse attempt.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// ViewModel for the disambiguation screen (multiple items matched the utterance).
/// AC4: Ambiguous item references trigger a disambiguation prompt listing matching items.
/// </summary>
public class NlCheckoutDisambiguateViewModel
{
    /// <summary>The original utterance that led to ambiguity.</summary>
    public string Utterance { get; set; } = string.Empty;

    /// <summary>The item reference fragment that was ambiguous.</summary>
    public string ItemRef { get; set; } = string.Empty;

    /// <summary>Candidate items the user must choose from.</summary>
    public IReadOnlyList<EquipmentItem> Candidates { get; set; } = [];

    /// <summary>Parsed due date carried forward from the original utterance (may be null).</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Human-readable due date display string.</summary>
    public string? DueDateDisplay { get; set; }

    /// <summary>Assignee name carried forward.</summary>
    public string? AssigneeName { get; set; }

    /// <summary>Assignee user ID carried forward.</summary>
    public int AssigneeId { get; set; }
}

/// <summary>
/// ViewModel for the NL checkout confirmation screen.
/// AC2: Confirmation prompt displays all extracted entities before any transaction is recorded.
/// </summary>
public class NlCheckoutConfirmViewModel
{
    /// <summary>Resolved equipment item.</summary>
    public EquipmentItem Item { get; set; } = null!;

    /// <summary>Assignee display name.</summary>
    public string AssigneeName { get; set; } = string.Empty;

    /// <summary>Assignee user ID.</summary>
    public int AssigneeId { get; set; }

    /// <summary>Parsed due date (null = no due date specified).</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Human-readable due date string for display.</summary>
    public string? DueDateDisplay { get; set; }

    /// <summary>The original NL utterance (for display context).</summary>
    public string OriginalUtterance { get; set; } = string.Empty;

    /// <summary>Error message shown when the item is unavailable.</summary>
    public string? ErrorMessage { get; set; }
}
