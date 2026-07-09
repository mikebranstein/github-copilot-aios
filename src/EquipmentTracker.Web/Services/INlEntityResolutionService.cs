using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Extracts structured checkout entities (item, assignee, due date) from a
/// natural-language utterance. Used by the Issue #148 technical spike to
/// validate ≥90% entity extraction accuracy on ≥50 representative utterances.
/// </summary>
public interface INlEntityResolutionService
{
    /// <summary>
    /// Parses <paramref name="utterance"/> and returns a <see cref="CheckoutIntent"/>
    /// containing the resolved item, assignee, and due date.
    /// </summary>
    /// <param name="utterance">Free-form text from the field worker.</param>
    /// <param name="currentUserId">
    /// ID of the authenticated user. Used as the default assignee when no
    /// explicit assignee is mentioned in the utterance.
    /// </param>
    /// <param name="referenceDate">
    /// The "now" anchor for relative date resolution.
    /// Defaults to <see cref="DateTime.UtcNow"/> when null.
    /// </param>
    CheckoutIntent Resolve(string utterance, int currentUserId, DateTime? referenceDate = null);

    /// <summary>
    /// Runs the prototype against a batch of labelled utterances and returns
    /// accuracy statistics. Used to validate the ≥90% accuracy acceptance criterion.
    /// </summary>
    SpikeAccuracyReport RunAccuracyTest(IReadOnlyList<LabelledUtterance> testSet, int currentUserId);
}

/// <summary>A single labelled test utterance for accuracy evaluation.</summary>
public class LabelledUtterance
{
    public string Utterance { get; init; } = string.Empty;
    public int? ExpectedItemId { get; init; }
    public int? ExpectedAssigneeUserId { get; init; }
    public DateTime? ExpectedDueDate { get; init; }
    public bool ExpectedAmbiguous { get; init; }
    public bool ExpectedNotFound { get; init; }
}

/// <summary>Aggregated accuracy report for the spike prototype.</summary>
public class SpikeAccuracyReport
{
    public int TotalUtterances { get; init; }
    public int CorrectExtractions { get; init; }
    public double AccuracyPercentage => TotalUtterances == 0 ? 0 : (double)CorrectExtractions / TotalUtterances * 100.0;
    public IReadOnlyList<string> FailedUtterances { get; init; } = Array.Empty<string>();
}
