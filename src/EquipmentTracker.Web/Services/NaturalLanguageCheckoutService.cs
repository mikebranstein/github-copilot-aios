using System.Text.RegularExpressions;
using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Rule-based stub implementation of <see cref="INaturalLanguageCheckoutService"/>.
///
/// Phase 1 stub: performs local regex/heuristic entity extraction.
/// Intended to be replaced with an LLM-backed implementation once
/// Technical Spike #148 delivers provider selection and architecture.
///
/// Supported patterns (case-insensitive):
///   "check out [item] to me [due-date-expression]"
///   "checkout [item] for [N] days"
///   "check out [item] until [date]"
///   "[item]" alone (minimal utterance)
///
/// Due-date expressions recognised:
///   "until [weekday]"      → next occurrence of that day
///   "until [date]"         → parsed absolute date
///   "for [N] day(s)"       → today + N days
///   none                   → DueDate = null
/// </summary>
public class NaturalLanguageCheckoutService : INaturalLanguageCheckoutService
{
    private readonly IEquipmentService _equipmentService;
    private readonly ILogger<NaturalLanguageCheckoutService> _logger;

    // Regex patterns for entity extraction
    private static readonly Regex CheckoutPrefixPattern =
        new(@"^(?:check\s*out|checkout)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ToMePattern =
        new(@"\bto\s+me\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UntilWeekdayPattern =
        new(@"\buntil\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UntilDatePattern =
        new(@"\buntil\s+(\w+\s+\d{1,2}(?:,\s*\d{4})?|\d{1,2}[/\-]\d{1,2}(?:[/\-]\d{2,4})?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForDaysPattern =
        new(@"\bfor\s+(\d+)\s+days?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TrailingDueDateSuffix =
        new(@"\s*(?:until|for)\s+.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ToMeSuffix =
        new(@"\s+to\s+me\b.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] Weekdays =
        ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];

    public NaturalLanguageCheckoutService(
        IEquipmentService equipmentService,
        ILogger<NaturalLanguageCheckoutService> logger)
    {
        _equipmentService = equipmentService;
        _logger = logger;
    }

    public Task<NlParseResult> ParseAsync(string utterance, int currentUserId, string currentUserName)
    {
        if (string.IsNullOrWhiteSpace(utterance))
        {
            return Task.FromResult(new NlParseResult
            {
                Status = NlParseStatus.LowConfidence,
                Confidence = 0.0,
                OriginalUtterance = utterance ?? string.Empty,
                ErrorMessage = "I didn't understand that — try 'Check out [item] to me until [date]'"
            });
        }

        try
        {
            return Task.FromResult(ExtractEntities(utterance.Trim(), currentUserId, currentUserName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NL parse error for utterance: {Utterance}", utterance);
            return Task.FromResult(new NlParseResult
            {
                Status = NlParseStatus.LlmTimeout,
                Confidence = 0.0,
                OriginalUtterance = utterance,
                ErrorMessage = "Service temporarily unavailable — please try again or use standard checkout."
            });
        }
    }

    // ── Entity extraction ─────────────────────────────────────────────────────

    private NlParseResult ExtractEntities(string utterance, int currentUserId, string currentUserName)
    {
        var result = new NlParseResult
        {
            OriginalUtterance = utterance,
            AssigneeId = currentUserId,
            AssigneeName = currentUserName
        };

        // Extract due date first (before stripping suffixes)
        result.DueDate = ExtractDueDate(utterance, out var dueDateDisplay);
        result.DueDateDisplay = dueDateDisplay;

        // Strip checkout verb prefix and "to me" / due-date suffixes to isolate item reference
        var itemRef = utterance;
        itemRef = CheckoutPrefixPattern.Replace(itemRef, "");
        itemRef = ToMeSuffix.Replace(itemRef, "");
        itemRef = TrailingDueDateSuffix.Replace(itemRef, "");
        itemRef = itemRef.Trim().TrimEnd(',', '.', '!');

        if (string.IsNullOrWhiteSpace(itemRef))
        {
            result.Status = NlParseStatus.LowConfidence;
            result.Confidence = 0.0;
            result.ErrorMessage = "I didn't understand that — try 'Check out [item] to me until [date]'";
            return result;
        }

        // Resolve equipment item
        var allItems = _equipmentService.GetAllItems();
        var matches = FindItemMatches(itemRef, allItems);

        switch (matches.Count)
        {
            case 0:
                result.Status = NlParseStatus.ItemNotFound;
                result.Confidence = 0.9;
                result.ErrorMessage = $"Item not found: '{itemRef}'. Check the name and try again, or use standard checkout.";
                break;
            case 1:
                result.Status = NlParseStatus.Success;
                result.Confidence = 0.95;
                result.ResolvedItem = matches[0];
                break;
            default:
                result.Status = NlParseStatus.Ambiguous;
                result.Confidence = 0.85;
                result.AmbiguousMatches = matches;
                result.ErrorMessage = $"Multiple items match '{itemRef}'. Please select one:";
                break;
        }
        return result;
    }

    private static readonly HashSet<string> Stopwords =
        new(["the", "a", "an", "some", "our", "my", "your"], StringComparer.OrdinalIgnoreCase);

    private static List<EquipmentItem> FindItemMatches(string itemRef, IReadOnlyList<EquipmentItem> allItems)
    {
        // 1. Exact name match (case-insensitive)
        var exact = allItems
            .Where(i => i.Name.Equals(itemRef, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count > 0) return exact;

        // 2. Name contains all significant words (stopwords stripped, order-independent fuzzy match)
        var words = itemRef
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !Stopwords.Contains(w))
            .ToArray();

        if (words.Length > 0)
        {
            var fuzzy = allItems
                .Where(i => words.All(w => i.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (fuzzy.Count > 0) return fuzzy;
        }

        // 3. Name starts-with the item reference
        var startsWith = allItems
            .Where(i => i.Name.StartsWith(itemRef, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return startsWith;
    }

    private static DateTime? ExtractDueDate(string utterance, out string? display)
    {
        display = null;

        // "for N days"
        var forDays = ForDaysPattern.Match(utterance);
        if (forDays.Success && int.TryParse(forDays.Groups[1].Value, out var days))
        {
            var date = DateTime.UtcNow.Date.AddDays(days);
            display = date.ToString("dddd, MMMM d");
            return date;
        }

        // "until [weekday]"
        var untilWeekday = UntilWeekdayPattern.Match(utterance);
        if (untilWeekday.Success)
        {
            var weekdayName = untilWeekday.Groups[1].Value;
            var targetDay = (DayOfWeek)Array.FindIndex(Weekdays,
                w => w.Equals(weekdayName, StringComparison.OrdinalIgnoreCase));
            var today = DateTime.UtcNow.Date;
            var daysUntil = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;
            if (daysUntil == 0) daysUntil = 7; // "until Monday" means next Monday
            var date = today.AddDays(daysUntil);
            display = date.ToString("dddd, MMMM d");
            return date;
        }

        // "until [date]" — absolute date
        var untilDate = UntilDatePattern.Match(utterance);
        if (untilDate.Success)
        {
            var raw = untilDate.Groups[1].Value;
            // Use InvariantCulture to ensure consistent parsing regardless of system locale
            if (DateTime.TryParse(raw,
                    System.Globalization.CultureInfo.GetCultureInfo("en-US"),
                    System.Globalization.DateTimeStyles.None,
                    out var parsed))
            {
                // If no year specified and parsed date is in the past, advance by 1 year
                var date = parsed.Date;
                if (date < DateTime.UtcNow.Date)
                    date = date.AddYears(1);
                display = date.ToString("dddd, MMMM d, yyyy",
                    System.Globalization.CultureInfo.GetCultureInfo("en-US"));
                return date;
            }
        }

        return null;
    }
}
