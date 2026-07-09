using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Parses natural language checkout utterances and resolves equipment items.
/// Issue #149 — Phase 1: Text-Based NL Checkout Interface (Mobile).
///
/// In Phase 1 the implementation uses a rule-based stub parser.
/// The real LLM provider will be wired in once Technical Spike #148 delivers its
/// Go/No-Go and architecture recommendation. The interface is defined here so that
/// the LLM-backed implementation can be swapped in without changing any callers.
/// </summary>
public interface INaturalLanguageCheckoutService
{
    /// <summary>
    /// Parses <paramref name="utterance"/> and resolves equipment items against the current
    /// inventory. Returns a <see cref="NlParseResult"/> describing the outcome.
    /// Never throws; all error paths are encoded in <see cref="NlParseResult.Status"/>.
    /// </summary>
    /// <param name="utterance">Raw text entered by the field worker.</param>
    /// <param name="currentUserId">ID of the authenticated user (used as default assignee).</param>
    /// <param name="currentUserName">Display name of the authenticated user.</param>
    Task<NlParseResult> ParseAsync(string utterance, int currentUserId, string currentUserName);
}
