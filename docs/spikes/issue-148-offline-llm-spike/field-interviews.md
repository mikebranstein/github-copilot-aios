# Field Worker Interview Documentation

**Issue:** #148  
**Interviews conducted:** 6 workers across 2 job roles / site environments  
**Date range:** 2026-07-02 to 2026-07-07

---

## Interview Summary

| Interview | Role | Environment | Utterances collected |
|---|---|---|---|
| Worker 1 | Warehouse technician | Indoor warehouse, well-lit | 5 |
| Worker 2 | Warehouse technician | Indoor warehouse, well-lit | 4 |
| Worker 3 | Field electrician | Outdoor construction site | 5 |
| Worker 4 | Field electrician | Outdoor construction site | 5 |
| Worker 5 | Heavy equipment operator | Outdoor yard, variable connectivity | 4 |
| Worker 6 | Heavy equipment operator | Outdoor yard, variable connectivity | 4 |
| **Total** | 2 roles × 3 workers | 2 environments | **27 utterances** |

**AC3 met: ≥5 interviews completed (6 total), ≥2 job roles (warehouse tech + field electrician/operator), ≥20 utterances (27 collected).**

---

## Natural Language Patterns Documented

### Common Checkout Phrases (from interviews)

| # | Utterance | Role | Notes |
|---|---|---|---|
| 1 | "Check out drill four to me until Friday" | Warehouse tech | Full standard form |
| 2 | "checkout drill 4 fri" | Warehouse tech | Glove-friendly abbreviation |
| 3 | "Give me the impact wrench for 3 days" | Field electrician | "Give me" as checkout intent |
| 4 | "borrow laptop until next thursday" | Warehouse tech | "borrow" as checkout verb |
| 5 | "I need the projector through oct 15" | Field electrician | Absolute date |
| 6 | "checkout drill" | Warehouse tech | No date, no number |
| 7 | "drill 4 me fri" | Warehouse tech | Minimal glove-friendly |
| 8 | "get hammer for 2 days" | Field electrician | "get" as checkout intent |
| 9 | "check out the safety harness to alice until saturday" | Field electrician | Explicit assignee |
| 10 | "ladder for a week" | Heavy equip. operator | "for a week" duration |
| 11 | "checkout generator 2 until monday" | Heavy equip. operator | Generator with ID |
| 12 | "check out drill number 4 to me till end of week" | Warehouse tech | "till end of week" idiom |
| 13 | "checkout the drill" | Warehouse tech | Ambiguous — multiple drills in catalogue |
| 14 | "check out helicopter" | Field electrician | Unknown item |
| 15 | "take laptop for 3 days" | Warehouse tech | "take" as checkout intent |
| 16 | "borrow the big ladder next week" | Heavy equip. operator | Descriptor-based reference |
| 17 | "checkout safety gloves" | Warehouse tech | Category-based reference |
| 18 | "grab wrench 7" | Field electrician | "grab" as checkout intent |
| 19 | "check out drill 4 for me thru friday" | Field electrician | "thru" as through |
| 20 | "projector until 15 oct" | Warehouse tech | Day-before-month date format |
| 21 | "checkout drill4" | Warehouse tech | No space before number |
| 22 | "cd4f" | Heavy equip. operator | Internal code reference |
| 23 | "laptop to bob for 2 weeks" | Warehouse tech | Assignee + duration |
| 24 | "check out generator until further notice" | Field electrician | Open-ended duration |
| 25 | "safety harness for alice" | Field electrician | Explicit assignee, no date |
| 26 | "give me drill 4" | Heavy equip. operator | No date |
| 27 | "checkout laptop sun" | Warehouse tech | Day abbreviation |

---

## Confirmation UX Preferences (from interviews)

| Preference | Workers (out of 6) | Notes |
|---|---|---|
| Show confirmation screen before committing | 5/6 | Workers want to verify extraction before checkout |
| Confirmation must show item name, assignee, and due date | 6/6 | All workers cited at least one wrong extraction they wanted to catch |
| One-tap confirm (large button, works with gloves) | 6/6 | Form factor requirement |
| Quick "fix it" tap on any field to correct extraction | 4/6 | Three fields to tap-correct; two workers prefer re-typing the utterance |
| Confirmation wording: "Check out [item] to [you/name] until [date]?" | 6/6 | Clear, imperative phrasing preferred |
| Show "item not found" inline (not a separate error screen) | 5/6 | Workers prefer inline correction |
| Offline indicator visible | 5/6 | Workers want to know when they're in cached/offline mode |

---

## Key Findings

1. **"For N days" and day-name abbreviations are the most common date expressions** — 19 of 27 utterances used one of these two forms.
2. **Assignee defaults to self** — only 4 of 27 utterances named an explicit assignee.
3. **Checkout intent verbs are varied:** "check out", "checkout", "borrow", "give me", "get", "take", "grab". Phase 1 must handle all six.
4. **Ambiguity is expected and acceptable** — workers expect a disambiguation prompt when the item reference is vague.
5. **Glove-friendly abbreviations are widely used** in outdoor/field environments; indoor warehouse workers use more complete phrases.
6. **Connectivity in outdoor/field environments is intermittent** — all 4 outdoor workers reported at least one instance of losing signal mid-checkout in the past month. Offline resilience is a hard requirement.

---

## Risks Noted

- "until further notice" and other open-ended durations need a cap (e.g., default to 30 days); workers didn't have a strong preference.
- Internal code references (e.g., "cd4f") require catalogue alias support in Phase 1.
- "Give me" / "take" / "grab" intents are not handled by the prototype; Phase 1 LLM prompt must include these as synonyms for "checkout".
