using System.Text.RegularExpressions;
using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Rule-based NL entity resolution prototype for the Issue #148 technical spike.
///
/// Purpose: Validate that checkout intent (item, assignee, due date) can be
/// reliably extracted from short, glove-friendly field utterances before
/// committing to an LLM-based Phase 1 implementation.
///
/// Approach: Deterministic regex + fuzzy-token matching against the equipment
/// catalogue so that the spike can run entirely offline without any LLM API call.
/// Phase 1 will replace (or augment) this with an LLM-powered variant once the
/// Go/No-Go recommendation is confirmed.
///
/// Design decision: Issue #148 — no production deployment; prototype only.
/// </summary>
public class NlEntityResolutionService : INlEntityResolutionService
{
    private readonly IEquipmentService _equipmentService;
    private readonly IUserService _userService;

    // Day-of-week name → DayOfWeek
    private static readonly Dictionary<string, DayOfWeek> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["monday"] = DayOfWeek.Monday,   ["mon"] = DayOfWeek.Monday,
        ["tuesday"] = DayOfWeek.Tuesday, ["tue"] = DayOfWeek.Tuesday, ["tues"] = DayOfWeek.Tuesday,
        ["wednesday"] = DayOfWeek.Wednesday, ["wed"] = DayOfWeek.Wednesday,
        ["thursday"] = DayOfWeek.Thursday,   ["thu"] = DayOfWeek.Thursday, ["thur"] = DayOfWeek.Thursday, ["thurs"] = DayOfWeek.Thursday,
        ["friday"] = DayOfWeek.Friday,   ["fri"] = DayOfWeek.Friday,
        ["saturday"] = DayOfWeek.Saturday,   ["sat"] = DayOfWeek.Saturday,
        ["sunday"] = DayOfWeek.Sunday,   ["sun"] = DayOfWeek.Sunday,
    };

    // Month abbreviations for absolute date parsing
    private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jan"] = 1, ["january"] = 1,
        ["feb"] = 2, ["february"] = 2,
        ["mar"] = 3, ["march"] = 3,
        ["apr"] = 4, ["april"] = 4,
        ["may"] = 5,
        ["jun"] = 6, ["june"] = 6,
        ["jul"] = 7, ["july"] = 7,
        ["aug"] = 8, ["august"] = 8,
        ["sep"] = 9, ["sept"] = 9, ["september"] = 9,
        ["oct"] = 10, ["october"] = 10,
        ["nov"] = 11, ["november"] = 11,
        ["dec"] = 12, ["december"] = 12,
    };

    public NlEntityResolutionService(IEquipmentService equipmentService, IUserService userService)
    {
        _equipmentService = equipmentService;
        _userService = userService;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public CheckoutIntent Resolve(string utterance, int currentUserId, DateTime? referenceDate = null)
    {
        var now = referenceDate ?? DateTime.UtcNow;
        var normalised = Normalise(utterance);

        var intent = new CheckoutIntent { RawUtterance = utterance };

        // 1. Resolve due date
        intent.DueDate = TryResolveDueDate(normalised, now);

        // 2. Resolve assignee (default: current user)
        intent.AssigneeUserId = TryResolveAssignee(normalised, currentUserId) ?? currentUserId;

        // 3. Resolve equipment item
        ResolveItem(intent, normalised);

        // 4. Compute overall confidence
        intent.Confidence = ComputeConfidence(intent);

        return intent;
    }

    public SpikeAccuracyReport RunAccuracyTest(IReadOnlyList<LabelledUtterance> testSet, int currentUserId)
    {
        int correct = 0;
        var failures = new List<string>();

        // Use a fixed reference date for reproducible date resolution in tests
        var referenceDate = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc); // Monday

        foreach (var labelled in testSet)
        {
            var intent = Resolve(labelled.Utterance, currentUserId, referenceDate);
            bool pass = EvaluatePrediction(intent, labelled, referenceDate);
            if (pass)
                correct++;
            else
                failures.Add($"FAIL: \"{labelled.Utterance}\" — " + DescribeMismatch(intent, labelled, referenceDate));
        }

        return new SpikeAccuracyReport
        {
            TotalUtterances = testSet.Count,
            CorrectExtractions = correct,
            FailedUtterances = failures.AsReadOnly()
        };
    }

    // ── Due-date resolution ───────────────────────────────────────────────────

    internal DateTime? TryResolveDueDate(string normalised, DateTime now)
    {
        // "for N days" / "for N day"
        var forDays = Regex.Match(normalised, @"\bfor\s+(\d+)\s+days?\b");
        if (forDays.Success && int.TryParse(forDays.Groups[1].Value, out int days))
            return now.Date.AddDays(days);

        // "for N weeks"
        var forWeeks = Regex.Match(normalised, @"\bfor\s+(\d+)\s+weeks?\b");
        if (forWeeks.Success && int.TryParse(forWeeks.Groups[1].Value, out int weeks))
            return now.Date.AddDays(weeks * 7);

        // "until|through|till next <day>" or "until|through|till <day>"
        var untilDay = Regex.Match(normalised, @"\b(?:until|through|till)\s+(next\s+)?(\w+)\b");
        if (untilDay.Success)
        {
            string dayToken = untilDay.Groups[2].Value;
            if (DayNames.TryGetValue(dayToken, out var targetDay))
                return NextWeekday(now, targetDay, forceNext: untilDay.Groups[1].Success);
        }

        // "until|through <month> <day>" e.g. "until Oct 15" / "through October 15"
        var untilDate = Regex.Match(normalised, @"\b(?:until|through|till)\s+([a-z]+)\s+(\d{1,2})\b");
        if (untilDate.Success && MonthNames.TryGetValue(untilDate.Groups[1].Value, out int month))
        {
            if (int.TryParse(untilDate.Groups[2].Value, out int day))
            {
                int year = now.Month > month || (now.Month == month && now.Day > day)
                    ? now.Year + 1
                    : now.Year;
                return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            }
        }

        // Bare day name at end of utterance — glove-friendly shorthand e.g. "fri"
        var trailingDay = Regex.Match(normalised, @"\b(\w{2,9})\s*$");
        if (trailingDay.Success && DayNames.TryGetValue(trailingDay.Groups[1].Value, out var trailingTarget))
            return NextWeekday(now, trailingTarget, forceNext: false);

        return null;
    }

    private static DateTime NextWeekday(DateTime from, DayOfWeek target, bool forceNext)
    {
        int daysUntil = ((int)target - (int)from.DayOfWeek + 7) % 7;
        if (daysUntil == 0 || forceNext) daysUntil += 7;
        return from.Date.AddDays(daysUntil);
    }

    // ── Assignee resolution ───────────────────────────────────────────────────

    internal int? TryResolveAssignee(string normalised, int currentUserId)
    {
        // "to me" / "for me" — explicit current-user reference
        if (Regex.IsMatch(normalised, @"\bto\s+me\b|\bfor\s+me\b"))
            return currentUserId;

        // "to <username>" — look up by username
        var toUser = Regex.Match(normalised, @"\bto\s+([a-z][a-z0-9_\-]{1,30})\b");
        if (toUser.Success)
        {
            string candidate = toUser.Groups[1].Value;
            // Skip date-related words that follow "to"
            if (!DayNames.ContainsKey(candidate) && !MonthNames.ContainsKey(candidate))
            {
                var user = _userService.GetByUsername(candidate);
                if (user is not null)
                    return user.Id;
            }
        }

        return null; // caller will default to currentUserId
    }

    // ── Item resolution ───────────────────────────────────────────────────────

    private void ResolveItem(CheckoutIntent intent, string normalised)
    {
        var allItems = _equipmentService.GetAllItems();

        // Strip temporal/assignee fragments from the utterance to isolate item tokens
        string itemPortion = StripNonItemFragments(normalised);

        // Build a match score for each item
        var scored = allItems
            .Select(item => (item, score: ScoreItem(item, itemPortion)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ToList();

        if (scored.Count == 0)
        {
            intent.IsItemNotFound = true;
            intent.ExtractionNote = "No catalogue match found.";
            return;
        }

        double topScore = scored[0].score;
        // Ambiguous: multiple items share the top score
        var topCandidates = scored.Where(x => x.score == topScore).Select(x => x.item).ToList();

        if (topCandidates.Count > 1)
        {
            intent.AmbiguousCandidates = topCandidates.AsReadOnly();
            intent.ExtractionNote = $"Ambiguous — {topCandidates.Count} items share the top match score.";
        }
        else
        {
            intent.ResolvedItem = topCandidates[0];
            intent.ExtractionNote = $"Matched '{topCandidates[0].Name}' (ID {topCandidates[0].Id}) with score {topScore:F2}.";
        }
    }

    private static double ScoreItem(EquipmentItem item, string itemPortion)
    {
        double score = 0;
        string nameLower = item.Name.ToLowerInvariant();
        string[] nameTokens = Tokenise(nameLower);
        string[] portionTokens = Tokenise(itemPortion);

        // Exact name match
        if (itemPortion.Contains(nameLower, StringComparison.Ordinal))
            return 10.0;

        // Token overlap
        int overlap = nameTokens.Count(nt => portionTokens.Any(pt => pt == nt || pt.StartsWith(nt, StringComparison.Ordinal)));
        score += overlap * 2.0;

        // Numeric catalogue ID match (e.g. "#4" or "4")
        var idMatch = Regex.Match(itemPortion, @"#?(\d+)\b");
        if (idMatch.Success && int.TryParse(idMatch.Groups[1].Value, out int idNum) && idNum == item.Id)
            score += 5.0;

        return score;
    }

    private static string StripNonItemFragments(string normalised)
    {
        // Remove temporal phrases
        string s = Regex.Replace(normalised, @"\b(?:until|through|till|for)\s+\S+(?:\s+\d+)?", " ");
        s = Regex.Replace(s, @"\b(?:next\s+)?\b(?:mon|tue|tues|wed|thu|thur|thurs|fri|sat|sun|monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b", " ");
        // Remove assignee phrases
        s = Regex.Replace(s, @"\bto\s+(?:me|\S+)\b", " ");
        s = Regex.Replace(s, @"\bfor\s+me\b", " ");
        // Remove checkout/check-out verb
        s = Regex.Replace(s, @"\b(?:check\s*out|checkout)\b", " ");
        // Remove articles
        s = Regex.Replace(s, @"\b(?:the|a|an)\b", " ");
        return s.Trim();
    }

    private static string Normalise(string text)
    {
        // Lowercase, collapse whitespace, normalise "check out" → "checkout"
        var s = text.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"\s+", " ");
        s = Regex.Replace(s, @"\bcheck\s+out\b", "checkout");
        return s;
    }

    private static string[] Tokenise(string text)
        => Regex.Split(text, @"[\s\-_#]+")
                .Where(t => t.Length > 0)
                .ToArray();

    private static double ComputeConfidence(CheckoutIntent intent)
    {
        if (intent.IsItemNotFound) return 0.1;
        if (intent.IsAmbiguous) return 0.4;
        double c = 0.6;
        if (intent.ResolvedItem is not null) c += 0.2;
        if (intent.DueDate.HasValue) c += 0.1;
        if (intent.AssigneeUserId.HasValue) c += 0.1;
        return Math.Min(c, 1.0);
    }

    // ── Accuracy evaluation helpers ──────────────────────────────────────────

    private bool EvaluatePrediction(CheckoutIntent intent, LabelledUtterance label, DateTime referenceDate)
    {
        // Item check
        if (label.ExpectedNotFound)
        {
            if (!intent.IsItemNotFound) return false;
        }
        else if (label.ExpectedAmbiguous)
        {
            if (!intent.IsAmbiguous) return false;
        }
        else if (label.ExpectedItemId.HasValue)
        {
            if (intent.ResolvedItem?.Id != label.ExpectedItemId.Value) return false;
        }

        // Assignee check (only if label specifies one)
        if (label.ExpectedAssigneeUserId.HasValue &&
            intent.AssigneeUserId != label.ExpectedAssigneeUserId.Value)
            return false;

        // Due date check (tolerance: same calendar day)
        if (label.ExpectedDueDate.HasValue)
        {
            if (!intent.DueDate.HasValue) return false;
            if (intent.DueDate.Value.Date != label.ExpectedDueDate.Value.Date) return false;
        }

        return true;
    }

    private static string DescribeMismatch(CheckoutIntent intent, LabelledUtterance label, DateTime referenceDate)
    {
        var parts = new List<string>();
        if (label.ExpectedNotFound && !intent.IsItemNotFound)
            parts.Add($"expected not-found but got item ID {intent.ResolvedItem?.Id}");
        else if (label.ExpectedAmbiguous && !intent.IsAmbiguous)
            parts.Add($"expected ambiguous but got item ID {intent.ResolvedItem?.Id}");
        else if (label.ExpectedItemId.HasValue && intent.ResolvedItem?.Id != label.ExpectedItemId)
            parts.Add($"item mismatch: expected {label.ExpectedItemId} got {intent.ResolvedItem?.Id}");
        if (label.ExpectedAssigneeUserId.HasValue && intent.AssigneeUserId != label.ExpectedAssigneeUserId)
            parts.Add($"assignee mismatch: expected {label.ExpectedAssigneeUserId} got {intent.AssigneeUserId}");
        if (label.ExpectedDueDate.HasValue && intent.DueDate?.Date != label.ExpectedDueDate?.Date)
            parts.Add($"date mismatch: expected {label.ExpectedDueDate:yyyy-MM-dd} got {intent.DueDate?.Date:yyyy-MM-dd}");
        return string.Join("; ", parts);
    }
}
