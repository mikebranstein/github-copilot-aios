namespace EquipmentTracker.Web.Models;

/// <summary>
/// Represents the structured entities extracted from a natural language checkout utterance.
/// Used by the NL Entity Resolution prototype (Issue #148 spike).
/// </summary>
public class CheckoutIntent
{
    /// <summary>Raw utterance text submitted by the field worker.</summary>
    public string RawUtterance { get; init; } = string.Empty;

    /// <summary>
    /// Resolved equipment item, or null if extraction failed or was ambiguous.
    /// </summary>
    public EquipmentItem? ResolvedItem { get; set; }

    /// <summary>
    /// Candidate matches when the utterance is ambiguous (multiple items could match).
    /// Non-empty when <see cref="IsAmbiguous"/> is true.
    /// </summary>
    public IReadOnlyList<EquipmentItem> AmbiguousCandidates { get; set; } = Array.Empty<EquipmentItem>();

    /// <summary>
    /// The assignee user ID resolved from the utterance, or null to default to the current user.
    /// </summary>
    public int? AssigneeUserId { get; set; }

    /// <summary>
    /// Resolved due-date / return date. Null when no temporal expression was found.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>True when the utterance matched multiple possible items.</summary>
    public bool IsAmbiguous => AmbiguousCandidates.Count > 1;

    /// <summary>True when no matching item was found in the catalogue.</summary>
    public bool IsItemNotFound { get; set; }

    /// <summary>Confidence score in [0.0, 1.0] for the overall extraction.</summary>
    public double Confidence { get; set; }

    /// <summary>Human-readable description of the extraction result for debugging.</summary>
    public string? ExtractionNote { get; set; }
}
