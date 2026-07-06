# Prioritization Frameworks for Product Managers

When evaluating multiple opportunities, use the right framework for your context. This guide covers three major frameworks and when to use each.

---

## RICE Framework: Reach, Impact, Confidence, Effort

**Best for:** Objective comparison of many initiatives (15+ opportunities at once)

**Formula:** (Reach × Impact × Confidence) / Effort = Priority Score

### Inputs

**Reach:** How many customers affected per quarter?
- Estimate customers or user segments impacted
- Example: 500 customers = 500

**Impact:** What's the value created per customer?
- 3x = 3 months of value per user
- 2x = 2 months of value per user
- 1x = 1 month of value per user
- 0.5x = minimal impact
- Example: Reduces churn by 10% = 2x value

**Confidence:** How confident are you in the estimate?
- 100% = certain (data-backed)
- 50% = moderate uncertainty
- 25% = very uncertain (guessing)
- Example: Customer feedback strong = 80%

**Effort:** How many weeks of engineering effort?
- Example: 6 weeks

### Calculation

Score = (Reach × Impact × Confidence) / Effort

**Example:**

```
Initiative A: GPS Equipment Tracking
- Reach: 150 customers (30% of base)
- Impact: Reduces support tickets by 20% = 2x
- Confidence: 60% (guessing on adoption)
- Effort: 6 weeks

Score: (150 × 2 × 0.6) / 6 = 30

Initiative B: Mobile App
- Reach: 400 customers (80% of base)
- Impact: Increases retention by 5% = 1.5x
- Confidence: 80% (strong customer demand)
- Effort: 10 weeks

Score: (400 × 1.5 × 0.8) / 10 = 48

Ranking: Mobile App (48) > GPS Tracking (30)
```

### Score Interpretation

- **>100:** Quick win (do immediately)
- **50-100:** High priority
- **25-50:** Medium priority
- **<25:** Low priority (defer)

### Strengths & Limitations

✅ **Strengths:**
- Objective, numbers-based
- Accounts for uncertainty
- Easy to explain to stakeholders
- Handles 15+ initiatives well

❌ **Limitations:**
- Inputs are estimates (garbage in = garbage out)
- Doesn't account for dependencies
- Can miss strategic importance
- Requires good data

---

## Value vs. Effort Matrix: 2x2 Visual Prioritization

**Best for:** Quick team prioritization (8-12 initiatives); collaborative workshops

**Format:** 2x2 matrix with axes:
- X-axis: Effort (Low → High)
- Y-axis: Value (Low → High)

### Quadrants & Actions

| Quadrant | Name | Action |
|----------|------|--------|
| **High Value, Low Effort** | QUICK WINS | 🎯 Do these first. Easy momentum. |
| **High Value, High Effort** | STRATEGIC BETS | 📋 Plan carefully. Multi-quarter commitment. |
| **Low Value, Low Effort** | NICE-TO-HAVE | ⏳ Do if time permits. Low ROI. |
| **Low Value, High Effort** | TIME WASTERS | 🚫 Avoid. Don't waste time/budget. |

### Example

```
Effort (X-axis)          Low        High
                    ┌──────────┬──────────┐
              High  │ QUICK    │ STRATEG. │
              Value │ WINS     │ BETS     │
                    ├──────────┼──────────┤
               Low  │ NICE-TO- │ TIME     │
              Value │ HAVE     │ WASTERS  │
                    └──────────┴──────────┘

Quick Wins (do first):
- Mobile notifications (5 pts value, 1 week effort)
- Onboarding tutorial (7 pts value, 2 weeks effort)

Strategic Bets (plan next):
- Mobile app (9 pts value, 10 weeks effort)
- API ecosystem (8 pts value, 12 weeks effort)

Nice-to-Have:
- Dark mode (3 pts value, 1 week effort)

Avoid:
- Legacy system rewrite (2 pts value, 20 weeks effort)
```

### How to Use

1. List all initiatives on sticky notes
2. Estimate value (1-10 scale) and effort (weeks)
3. Place each on the matrix
4. Discuss and adjust collectively
5. Quick wins first, strategic bets second

### Strengths & Limitations

✅ **Strengths:**
- Visual, easy to understand
- Great for team alignment
- Fast (30-60 min workshop)
- Reveals low-hanging fruit

❌ **Limitations:**
- Subjective (team dependent)
- Doesn't handle dependencies well
- Can miss strategic alignment
- Not great for 20+ initiatives

---

## OKR-Based Prioritization: Strategic Alignment First

**Best for:** Ensuring every initiative serves strategy; avoiding random feature creep

**Method:**

1. Define annual OKRs (Objectives & Key Results)
2. For each initiative: "Which OKR does this ladder to?"
3. Prioritize initiatives by OKR importance
4. Within each OKR, order by effort (quick wins first)

### Example

```
OKR 1: Achieve 60% mobile adoption (Strategic Priority = Highest)
- Mobile app (required for OKR) → Priority 1
- Push notifications (enables discovery) → Priority 2
- Mobile redesign (improves stickiness) → Priority 3

OKR 2: Build API ecosystem (Strategic Priority = Medium)
- Salesforce integration (10 customers requesting) → Priority 4
- Slack bot (nice-to-have) → Priority 7

OKR 3: Land Fortune 500 customers (Strategic Priority = Medium)
- Enterprise SSO (required for deals) → Priority 5
- Advanced analytics dashboard (differentiator) → Priority 6

Not laddering to OKR:
- Dark mode (nice-to-have) → Priority 10 (only if spare capacity)
```

### Prioritized List

1. Mobile app (OKR 1, critical path)
2. Enterprise SSO (OKR 3, required to unlock market)
3. Push notifications (OKR 1, accelerator)
4. Salesforce integration (OKR 2, high value)
5. Advanced analytics (OKR 3, differentiator)
6. Mobile redesign (OKR 1, optimization)
7. Slack bot (OKR 2, ecosystem)
8-10. Other low-OKR-impact items

### Strengths & Limitations

✅ **Strengths:**
- Ensures strategic alignment
- Prevents feature creep
- Easy to communicate ("serves OKR X")
- Great for board/exec buy-in

❌ **Limitations:**
- Requires clear OKRs first
- Can miss urgent market needs
- Less useful for optimizations
- Doesn't account for dependencies

---

## Comparison: Which Framework When?

| Situation | Best Framework | Why |
|-----------|---|---|
| Comparing 15+ initiatives objectively | RICE | Numbers are unbiased; scales to many items |
| Quick team prioritization; collaborative | Value vs. Effort | Visual; interactive; fast (30 min) |
| Aligning to strategy; preventing creep | OKR-based | Ensures every initiative serves north star |
| Mix of new features + tech debt | RICE + OKR | RICE for initial ranking; OKR for alignment check |
| Executive presentation | OKR-based | Simplest story; board understands strategic fit |
| Quarterly sprint planning | Value vs. Effort | Fast workshop with team |
| Annual roadmap planning | All three | Use RICE for data, V vs E for team consensus, OKR for strategy fit |

---

## Recommendation: Hybrid Approach

**Quarterly planning process:**

1. **Month 1:** Gather all opportunities (15-20 items)
2. **Week 1:** Use RICE to score all items objectively
3. **Week 2:** Use Value vs. Effort to visualize and discuss with team
4. **Week 3:** Use OKR-based framework to ensure strategic alignment
5. **Week 4:** Present ranked roadmap to exec team

**Why hybrid?**
- RICE gives you objective data
- Value vs. Effort builds team consensus
- OKR-based ensures you're not optimizing locally (missing strategy)
- Together, they reduce bias and increase buy-in

---

## Implementation Checklist

- [ ] Gather all opportunities/requests for the quarter (15-20 items)
- [ ] Estimate reach/effort for RICE scoring
- [ ] Run RICE calculations (spreadsheet)
- [ ] Schedule Value vs. Effort workshop (60 min)
- [ ] Map each initiative to OKRs
- [ ] Review for dependencies (are any blocked?)
- [ ] Present prioritized roadmap to team + execs
- [ ] Document decision rationale
- [ ] Review quarterly (adjust based on learnings)
