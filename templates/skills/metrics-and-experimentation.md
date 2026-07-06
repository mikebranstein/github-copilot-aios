# Metrics & Experimentation for Product Owners

Use data to make backlog decisions, not opinion. This guide covers AARRR framework, funnel analysis, cohort analysis, and A/B testing.

---

## AARRR Framework: Identify What's Broken First

Before shipping new features, identify which metric is broken. Then prioritize fixes to that metric.

### The Five Metrics

**Acquisition:** Are new users arriving?
- Measure: New users per month, CAC (cost per acquisition)
- Target: Growing at target rate (e.g., +10% month-over-month)
- If broken: Improve marketing, sales, or awareness

**Activation:** Do new users complete onboarding?
- Measure: % of new users who [complete onboarding / sign up / try first feature]
- Target: 40-60% (varies by product)
- If broken: Improve onboarding flow, reduce friction

**Retention:** Do users come back?
- Measure: % of users active after 7 days, 30 days, 90 days
- Target: 50%+ at day 7, 30%+ at day 30
- If broken: Improve core product, add engagement features

**Referral:** Do users invite others?
- Measure: % of users who refer someone, viral coefficient
- Target: 10-20% viral (depends on business model)
- If broken: Improve referral mechanics, incentives

**Revenue:** Are users generating revenue?
- Measure: Total ARR, ARPU (average revenue per user), LTV:CAC ratio
- Target: LTV > 3x CAC
- If broken: Improve pricing, upsell, reduce churn

### Using AARRR to Prioritize Backlog

**Scenario 1: Retention is broken**
```
Current state:
- Acquisition: ✅ Growing 15% MoM
- Activation: ✅ 50% completing onboarding
- Retention: ❌ Only 25% active at day 7 (target: 50%)
- Referral: ⚠️ 5% viral (target: 15%)
- Revenue: ⚠️ $100K ARR (target: $200K)

Action:
- Prioritize retention features (engagement loops, notifications, personalization)
- Hold new acquisition marketing (no point growing if users churn)
- Defer new features that don't impact retention
- Expected impact: If we get retention to 50%, revenue grows 2x without new acquisition
```

**Scenario 2: Activation is broken**
```
Current state:
- Acquisition: ✅ Growing 20% MoM
- Activation: ❌ 20% completing onboarding (target: 50%)
- Retention: ⚠️ 40% at day 7 (decent, but low activation compounds)
- Referral: ❌ 2% viral (broken because not enough users activated)
- Revenue: ❌ $50K ARR (low because activation is gateway)

Action:
- Prioritize onboarding improvements (simpler signup, better tutorials)
- Cut feature complexity (users can't navigate to your features)
- Defer acquisition growth until activation improves
- Expected impact: If we get to 50% activation, retention and referral both improve
```

### AARRR Analysis Questions

1. Which metric is worst vs. industry benchmark?
2. How does that metric correlate with revenue?
3. What product changes would improve that metric?
4. How quickly can we test those changes?

---

## Funnel Analysis: Where Do We Lose Users?

Identify the bottleneck in your user flow. Fix the biggest drop-off first.

### Common Funnels

**Acquisition Funnel:**
```
Landing page visitor: 1,000
↓ (80% click)
Sign-up page: 800
↓ (50% complete sign-up)
Email verified: 400
↓ (60% download app / first login)
Active user: 240
```

Drop-off at email verification is biggest leak. Fix that first.

**Checkout Funnel:**
```
Cart page: 500 users
↓ (85% click checkout)
Checkout page: 425
↓ (92% complete shipping info)
Shipping options: 391
↓ (50% select shipping)  ← BIGGEST DROP-OFF
Payment info: 195
↓ (95% complete payment)
Order confirmation: 185
```

Shipping options screen loses 50% of users. Problem: Too many options? Confusing UX? Wrong estimate? Investigate and fix.

### Funnel Analysis Template

```
Funnel: [Name]

Stage 1: [Step name] - 1,000 users
Stage 2: [Step name] - 800 users (80% conversion)
Stage 3: [Step name] - 600 users (75% conversion)
  → DROP-OFF: 200 users lost here (25% drop)
Stage 4: [Step name] - 180 users (30% conversion)
  → BIGGEST DROP-OFF: 420 users lost (70% drop) ← FIX THIS FIRST

Diagnosis: 
- Session recording shows [specific issue]
- Survey data shows [user feedback]
- A/B test shows [design performs better]

Action:
- Change [X] to improve conversion
- Re-measure in 1 week
```

### Prioritizing Funnel Fixes

- **Largest leak:** 70% drop-off on stage 4 (impacts most users)
- **Biggest pain:** Even though 40% drop on stage 2 (affects more people overall)
- **Easiest fix:** Stage 1 drop-off might be one-line copy change

Consider: Impact × Ease. Fix high-impact, lower-effort items first.

---

## Cohort Analysis: Are New Users Better Than Old Users?

Compare user cohorts to understand if your product is improving.

### Example Cohort Analysis

```
Cohort: Users who signed up in [Month]

January 2026 Cohort (old):
- Week 1 retention: 60%
- Week 4 retention: 45%
- Week 12 retention: 25%

April 2026 Cohort (new, after onboarding redesign):
- Week 1 retention: 72% ↑ (+12%)
- Week 4 retention: 58% ↑ (+13%)
- Week 12 retention: 40% ↑ (+15%)

Conclusion:
- Onboarding redesign improved retention 12-15% across all stages
- Positive signal: Newer users stickier than older users
- Action: Deploy more features like this
```

### Cohort Comparison Questions

1. Are newer cohorts stickier (better retention)?
2. Are newer cohorts more engaged (higher usage)?
3. Are newer cohorts more profitable (higher ARPU)?
4. What changed between cohorts? (Feature, onboarding, messaging, pricing)
5. Can we replicate what worked?

### Reading Cohort Tables

```
             Week 1    Week 4    Week 12   Trend
Jan cohort   60%       45%       25%       Declining (bad)
Feb cohort   59%       46%       26%       Declining (still bad)
Mar cohort   65%       48%       28%       Slight improvement
Apr cohort   72%       58%       40%       Major improvement (good!)
May cohort   70%       56%       38%       Sustained improvement
```

April cohort is better than January. What changed? Find it. Keep doing it.

---

## Pre-Launch Metrics: Define Success Before You Build

Don't wait until after launch to decide what success looks like.

### Primary Metrics (Must Hit)

These are non-negotiable. If you miss, feature triggers iteration or kill decision.

**Example:**
```
Feature: Mobile Checkout

Primary Metric 1: Adoption
- Definition: % of DAU who try mobile checkout in first month
- Target: 10% (based on similar feature: web checkout 8%)
- If miss: Investigate discovery/UX, relaunch or kill

Primary Metric 2: Completion Rate
- Definition: % of users who start mobile checkout that complete purchase
- Target: 85% (based on web checkout: 90%, accounting for mobile friction)
- If miss: Improve UX/flow, reduce friction

Primary Metric 3: No Regression
- Definition: Churn among mobile checkout users vs. non-users
- Target: Equal or better (not higher)
- If miss: Feature is causing harm; rollback and fix
```

### Secondary Metrics (Validate Design)

Validate your assumptions about how users interact.

**Example:**
```
Secondary Metric 1: Feature usage patterns
- % of mobile users who use [specific feature]
- Expected: Certain buttons more used than others
- Insight: If users ignore feature, UX may be wrong

Secondary Metric 2: Performance
- Average page load time on mobile checkout
- Target: <3 seconds
- If miss: Optimize performance

Secondary Metric 3: Customer satisfaction
- NPS of mobile checkout users
- Target: 7+/10 (similar to web)
- If miss: User experience issue
```

### Setting Targets (How to Decide)

1. **Use proxy/baseline:** "Similar feature had 8% adoption. We expect 10% (25% improvement) because we made it easier."
2. **Industry benchmark:** "Enterprise software onboarding typically 40% completion. We're targeting 50%."
3. **Stretch goal:** "We'd be thrilled with 20%, but would kill at 5%."

### Measurement Plan

```
Feature: Mobile Checkout

Success Metrics:
1. Adoption (10%) - measure from day 1
2. Completion rate (85%) - measure from day 1
3. Churn regression - compare mobile vs. non-mobile users at week 2

Measurement frequency:
- Day 1: Any blockers/errors?
- Week 1: Early adoption signal (on track to 10%?)
- Week 4: Adoption plateau or growing?
- Month 3: Full metric assessment

Decision points:
- Week 1: If <2% adoption → Investigate (discovery broken?)
- Week 4: If <5% adoption → Consider adjusting or kill
- Month 1: Final decision (iterate, scale, or kill)
```

---

## Funnel Analysis for Backlog Prioritization

When backlog is full, identify biggest drop-offs and prioritize fixes.

### Example: Identify Top Priority

```
Current Funnel Analysis:

[Landing page] 1,000 visitors
↓ (50% click) ← Big leak
[Sign-up page] 500 visitors
↓ (80% sign up)
[Email confirm] 400 users
↓ (70% complete profile)
[Active user] 280 users
↓ (40% make first purchase) ← Bigger leak
[Customer] 112 users

Backlog options:
A. Improve landing page messaging (reduce 50% leak to 70%)
B. Improve sign-up form (reduce 20% leak to 10%)
C. Simplify purchase flow (reduce 60% leak to 40%)

Impact analysis:
- Option A: 1,000 → 700 = +200 active users (+71%)
- Option B: 500 → 600 = +100 active users (+20%)
- Option C: 280 → 168 = +48 customers (+43% revenue impact)

Priority: Option A (biggest impact on active users), then Option C (revenue impact)
```

---

## Cohort Analysis for Feature Priority

Use cohort data to justify feature investments.

### Example: Prioritize Retention Features

```
Cohort Analysis Results:

Apr 2026 cohort retention: 45% at week 12
Mar 2026 cohort retention: 25% at week 12
Difference: April improved retention by 20 points

What changed between Mar and Apr?
- Product: Onboarding redesign, notification feature
- Marketing: New messaging about use case
- Pricing: Added free tier option

A/B test to isolate:
- 50% of Mar users see new onboarding (segment A)
- 50% of Mar users see old onboarding (segment B)
- Result: New onboarding group has 40% week-12 retention, old group 20%

Conclusion:
- Onboarding redesign alone drove 20-point retention improvement
- Prioritize: More onboarding improvements, engagement features
- Expected ROI: Retention improvement = revenue growth without acquisition

Backlog priority: Rank retention features highest
```

---

## A/B Testing for High-Risk Features

When unsure about design/messaging, test before full rollout.

### A/B Test Process

1. **Setup:** Randomly assign 5-10% of traffic to Test, 5-10% to Control
2. **Run:** Let it run for 1-2 weeks (enough data for statistical significance)
3. **Measure:** Compare conversion rate (Test vs. Control)
4. **Decide:** If Test wins and p<0.05 (statistically significant), ship; else revert

### Example: A/B Test Checkout Flow

```
Test: Simplified checkout (3 steps vs. 5 steps)
Control: Current checkout (5 steps)

Metrics:
- Control completion rate: 85%
- Test completion rate: 92%
- Difference: 7 percentage points
- Statistical significance: p=0.02 (95% confidence)

Decision: Test is 7% better, statistically significant. Ship new flow.
```

### Example: A/B Test Pricing Page

```
Test: New pricing page with video explanation
Control: Current pricing page (text only)

Metrics:
- Control conversion: 12%
- Test conversion: 13%
- Difference: 1 percentage point
- Statistical significance: p=0.4 (not significant)

Decision: Not enough difference to warrant change. Keep current design.
```

### When to A/B Test

- **Do A/B test:** High-stakes design decisions, price changes, onboarding flow
- **Don't A/B test:** Minor UI tweaks, low-traffic features (not enough data)
- **Rule of thumb:** If you're unsure between 2 options, test it

---

## Post-Launch Adoption Tracking

After launch, track adoption curve week-by-week.

### Week-by-Week Tracking

```
Mobile Checkout Launch Timeline

Week 1: 1% rollout
- 15% of 1% of users = 0.15% of total DAU adopted
- Status: No errors, performance good
- Decision: GO to 10%

Week 2: 10% rollout
- 25% of 10% = 2.5% of total DAU
- Status: 2% adoption (on track to 10% target)
- Decision: GO to 100%

Week 3: 100% rollout
- 10% of total DAU = 10% adoption ✅ (hit target!)
- Week 4: 12% adoption (still growing)

Month 2: 15% adoption, stabilizing
Month 3: 18% adoption, slight decline from peak (churn of early adopters)
- Status: Feature is sticky for core users

Conclusion: Feature successful. Move to Phase 2 (improvements).
```

### Adoption Curve Interpretation

```
High adoption (healthy):
  ╱╲
 ╱  ╲  (ramps up, plateaus, slight decline as early adopters switch to power users)
     ╲
      ——— (stable at high level)

Flat adoption (red flag):
  ___  (nobody using it)
 ╱
╱ (or grows then drops to nothing)

Declining adoption (feature failed):
    ╱
   ╱ (sharp rise then drop, suggests users tried and abandoned)
```

### Decision Points at Each Stage

- Week 1-2: "Is adoption ramping up?" (if <1%, investigate)
- Week 3-4: "Have we hit our target?" (if below, investigate or kill)
- Month 2: "Is adoption sticky?" (if declining fast, churn too high)
- Month 3+: "Ready for Phase 2?" (if stable, invest in improvements)

---

## Weekly Metrics Review Cadence

Every Monday, review feature metrics.

### Monday Standup Agenda (15 min)

```
Mobile Checkout Launch Week 3 Review:

Metric 1 - Adoption: 10% (on track) ✅
Metric 2 - Completion rate: 84% (close to 85% target) ✅
Metric 3 - Churn regression: Comparable to non-mobile users ✅

Support issues:
- 3 reports of slow checkout (performance issue)
- 2 reports of payment failure (investigate)
- 1 report of confusing UX

NPS (early mobile users): 7.5/10 (good)

Decision:
- Fix performance issue this sprint (impacts 3 users)
- Monitor payment failure (might be fluke)
- Monitor UX confusion (not urgent)

Status: Green, ready to continue rollout
```

---

## Implementation Checklist

- [ ] Define AARRR metrics for your product
- [ ] Identify which metric is broken first
- [ ] Document pre-launch success metrics (primary + secondary)
- [ ] Analyze current funnel (identify biggest drop-offs)
- [ ] Analyze cohorts (are recent users better/worse?)
- [ ] Set up dashboards for all metrics
- [ ] Create alerts for regression
- [ ] Plan weekly metrics review cadence (Monday standup)
- [ ] For high-risk features, plan A/B test
- [ ] Document adoption tracking plan
- [ ] Set kill-decision criteria (if adoption <X% after Y days)
- [ ] Post-launch: Review metrics weekly
- [ ] Month 3: Cohort review (Phase 2 or de-prioritize?)
