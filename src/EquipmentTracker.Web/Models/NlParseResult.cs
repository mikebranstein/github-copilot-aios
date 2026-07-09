namespace EquipmentTracker.Web.Models;

/// <summary>
/// Result of a natural language parse for a checkout utterance.
/// Issue #149 — Phase 1: Text-Based NL Checkout Interface (Mobile)
/// </summary>
public class NlParseResult
{
    /// <summary>Overall parse outcome.</summary>
    public NlParseStatus Status { get; set; }

    /// <summary>
    /// Confidence score 0.0–1.0. Below <see cref="NlParseConstants.MinConfidence"/>
    /// the result is treated as low-confidence and surfaced as an error to the user.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>Matched equipment item (single unambiguous match).</summary>
    public EquipmentItem? ResolvedItem { get; set; }

    /// <summary>Candidate items when the item reference is ambiguous (multiple matches).</summary>
    public IReadOnlyList<EquipmentItem> AmbiguousMatches { get; set; } = [];

    /// <summary>Assignee display name. Defaults to the authenticated user's name.</summary>
    public string? AssigneeName { get; set; }

    /// <summary>Assignee user ID. 0 means "current user".</summary>
    public int AssigneeId { get; set; }

    /// <summary>Parsed due date, if present in the utterance. Null if no due date detected.</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Human-readable representation of the due date for the confirmation prompt.</summary>
    public string? DueDateDisplay { get; set; }

    /// <summary>The raw utterance that was parsed.</summary>
    public string OriginalUtterance { get; set; } = string.Empty;

    /// <summary>Error or guidance message shown to the user on failure states.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Possible outcomes of a natural language parse attempt.</summary>
public enum NlParseStatus
{
    /// <summary>Single item resolved, assignee resolved, ready for confirmation.</summary>
    Success,

    /// <summary>Multiple equipment items match the item reference; user must select one.</summary>
    Ambiguous,

    /// <summary>No equipment item matched the reference in the utterance.</summary>
    ItemNotFound,

    /// <summary>Parse confidence is below the acceptable threshold.</summary>
    LowConfidence,

    /// <summary>The LLM API call timed out or returned an error.</summary>
    LlmTimeout,

    /// <summary>Network connectivity was lost before or during the LLM call.</summary>
    ConnectivityError
}

/// <summary>Configuration constants for the NL checkout feature.</summary>
public static class NlParseConstants
{
    /// <summary>Minimum confidence score to treat a parse as usable (AC3: ≥97% accuracy target).</summary>
    public const double MinConfidence = 0.70;

    /// <summary>LLM call timeout in seconds (AC1: end-to-end <5 s on 4G).</summary>
    public const int LlmTimeoutSeconds = 3;
}
