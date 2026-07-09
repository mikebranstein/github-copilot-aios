# Cost Model: Offline-First LLM Architecture

**Issue:** #148  
**Pricing snapshot date:** 2026-07-09 (prices subject to change; re-validate before Phase 1 estimate lock)

---

## Assumptions

| Parameter | Value |
|---|---|
| Average utterance tokens (input) | ~25 tokens |
| Average response tokens (output) | ~30 tokens |
| Cache hit rate | 70% (conservative; expect 75–80% after warm-up period) |
| Graceful-degradation rate (offline, no cache) | 5% |
| LLM API calls / transaction | 1 (cache miss path only) |

---

## Provider 1: OpenAI GPT-4o-mini

| Volume | Cache hits | LLM calls | Input tokens | Output tokens | Input cost | Output cost | **Total/day** | **Cost/transaction** |
|---|---|---|---|---|---|---|---|---|
| 1K/day | 700 | 300 | 7,500 | 9,000 | $0.000023 | $0.000054 | $0.077 | $0.000077 |
| 10K/day | 7,000 | 3,000 | 75,000 | 90,000 | $0.000225 | $0.000540 | $0.765 | $0.0000765 |
| 100K/day | 70,000 | 30,000 | 750,000 | 900,000 | $0.00225 | $0.00540 | $7.65 | $0.0000765 |

*GPT-4o-mini pricing (2026-07-09): $0.15/1M input tokens, $0.60/1M output tokens*

**Cost/transaction at 10K daily: $0.000077 — well within ≤$0.003 target (24× margin).**

---

## Provider 2: Google Gemini 1.5 Flash

| Volume | Cache hits | LLM calls | Input tokens | Output tokens | Input cost | Output cost | **Total/day** | **Cost/transaction** |
|---|---|---|---|---|---|---|---|---|
| 1K/day | 700 | 300 | 7,500 | 9,000 | $0.000011 | $0.000027 | $0.038 | $0.000038 |
| 10K/day | 7,000 | 3,000 | 75,000 | 90,000 | $0.000113 | $0.000270 | $0.383 | $0.0000383 |
| 100K/day | 70,000 | 30,000 | 750,000 | 900,000 | $0.001125 | $0.002700 | $3.825 | $0.0000383 |

*Gemini 1.5 Flash pricing (2026-07-09): $0.075/1M input tokens, $0.30/1M output tokens*

**Cost/transaction at 10K daily: $0.000038 — well within ≤$0.003 target (79× margin).**

---

## Worst-Case Scenario (No Cache)

If cache hit rate drops to 0% (e.g., highly variable utterances in the first week):

| Volume | GPT-4o-mini | Gemini 1.5 Flash |
|---|---|---|
| 1K/day | $0.00026/tx | $0.00013/tx |
| 10K/day | $0.00026/tx | $0.00013/tx |
| 100K/day | $0.00026/tx | $0.00013/tx |

**Even at zero cache hit rate, both providers remain below ≤$0.003 target.**

---

## On-Device Inference (Option A) — Reference Only

| Volume | Model download cost | Incremental API cost | Cost/transaction |
|---|---|---|---|
| Any volume | One-time ~$0.00 (model bundled) | $0.000 | $0.000 |

*Cost is effectively zero per transaction after model is deployed. However, Option A does not meet the ≥90% accuracy target (82–88% observed), making cost moot.*

---

## Summary

| Option | Cost/tx at 10K daily | Meets ≤$0.003 target? |
|---|---|---|
| Option A (On-Device) | ~$0.000 | ✅ Yes — but fails accuracy requirement |
| Option B: GPT-4o-mini | $0.000077 | ✅ Yes (24× margin) |
| Option B: Gemini 1.5 Flash | $0.000038 | ✅ Yes (79× margin) |

**Conclusion:** Cost target is confirmed achievable at all modelled transaction volumes. The ≤$0.003/transaction constraint is met with substantial margin. Provider selection should be driven by accuracy and SLA considerations rather than cost.
