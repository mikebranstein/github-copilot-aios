# Continuous Learning Cycles for Product Decisions

Don't wait 3 months for a retrospective. Use 2-sprint feedback loops to make fast decisions about what to build, what to iterate, and when to kill.

---

## The Problem with Long Feedback Loops

**Old way (quarterly review):**

```
Week 1: Ship feature
Weeks 2-12: Radio silence (nobody uses it)
Week 12: Retrospective: "Why didn't this work?"
Week 13-14: Analyze and decide to kill (1-2 weeks too late, team demoralized)
```

**Cost:** 3 months of wasted engineering, team momentum lost, wrong conclusions ("customers don't want this" vs. "we shipped it wrong").

---

## The New Way: 2-Sprint Feedback Loops

Every feature/experiment gets 2 sprints (4 weeks) of intensive feedback, then a clear decision.

### Timeline

```
SPRINT 1 (Weeks 1-2): BUILD
- Ship feature to 100% of users (or controlled rollout)
- Feature is "rough but usable"
- Goal: Get real users on it

SPRINT 2 (Week 3): COLLECT FEEDBACK
- Monitor adoption (% of users activating feature)
- Interview 10-15 users (qualitative)
- Check metrics (is it reducing churn? increasing engagement?)
- Review support feedback
- Competitor monitoring

SPRINT 2 (Week 4): DECIDE
- Adopt the decision tree (below)
- Commit to Iterate, Pivot, or Kill
- Communicate decision to team
```

### Decision Tree

```
IS ADOPTION >20% IN FIRST 2 WEEKS?

└─ YES (20%+)
   └─ PHASE 2: Iterate & Improve
      - Customers validate value
      - Invest in polish, performance, edge cases
      - Next milestone: 50% adoption (3-6 months)
      - Revenue opportunity confirmed

└─ NO (<20%)
   ├─ Diagnosis: Why low adoption?
   │  ├─ Discovery broken? (Users don't know feature exists)
   │  │  └─ Action: Better onboarding, education, tutorials
   │  │  └─ Re-test in 2 weeks (revised rollout)
   │  │
   │  ├─ UX broken? (Users find it confusing)
   │  │  └─ Action: UX redesign, simplification
   │  │  └─ Re-test in 2 weeks (redesigned feature)
   │  │
   │  ├─ Audience wrong? (Built for SMB, only enterprise using)
   │  │  └─ Action: Re-target marketing, messaging
   │  │  └─ Re-test in 2 weeks (new audience)
   │  │
   │  ├─ Value not there (Users try it, don't see ROI)
   │  │  └─ Action: Kill it (probably unfixable in short term)
   │  │  └─ Decision: KILL
   │  │
   │  └─ Market moved? (Competitors shipped faster)
   │     └─ Action: Kill it (no longer differentiator)
   │     └─ Decision: KILL
   │
   └─ IF FIXABLE IN 1-2 SPRINTS
      - Re-test after fix (2 more weeks)
      - If >20% adoption after fix → PHASE 2
      - If still <20% → KILL

IF KILL: Ship removal (even if small % use it)
- Users with data: Export before removal
- Docs: Explain why removed, alternatives
- Team: Learn & share (quarterly postmortem)
```

---

## Example: Feature Decision

### Scenario: "AI Email Drafting" Feature

**Week 1-2: Ship**
- Ship to 100% of users
- 3-click setup: "Enable AI drafting"
- Feature: LLM generates email draft, user reviews

**Week 3: Collect Feedback**

Metrics dashboard:
```
Adoption: 12% (1,200 of 10,000 activated)
Daily Active: 3% (300 of 10,000 use >1x daily)
Support: 4 complaints ("drafts are too generic", "takes too long")
NPS: 6.2/10 (vs. product average 8.5/10)

User interviews (10 users):
- Users who adopted: "Nice for quick emails, but not for important ones"
- Users who didn't: "I tried it once, didn't see the point"
- Power users: "Just use Gmail's built-in drafting"
```

**Week 4: Decision**

```
Decision Tree:
- Adoption 12% < 20% threshold?
- YES → Diagnose

Diagnosis:
1. Discovery broken? (3% DAU suggests not a discovery issue; 12% adoption is OK initial)
2. UX broken? (4 support complaints; users say "too generic")
3. Value not there? (User quote: "not for important emails") ← This one
4. Market moved? (Gmail has similar feature)

Conclusion:
- Value proposition is weak. LLM drafting is "nice-to-have" not "must-have"
- Fixable via more training data? Possible, but requires 3+ sprints
- Cost-benefit: 3 weeks of eng work for 12% adoption
- Better bet: Mobile app (has 40% adoption demand from win/loss)

DECISION: KILL

Communication:
- To users: "AI drafting wasn't used by most of you. We're removing it to simplify."
- To team: "We learned LLM for email is premature. Market isn't ready. Let's revisit in 12 months."
- Postmortem: Document why (data, user feedback, competitive analysis)
```

---

## 3-Month Checkpoint: Cohort Health Review

After 2-sprint feedback loop, don't immediately ship Phase 2. Instead, do a 3-month cohort review.

### Why 3-Month Review?

Features need time to:
1. Mature (smooth out bugs, edge cases)
2. Attract the "early majority" (after early adopters)
3. Show sustained usage (not just novelty)

### Process

```
Date: 3 months after Phase 1 ship

Cohort Review:
- Adoption: Now 20%, 50%, 80%? (tracking toward goals?)
- Retention: Do adopters stay? (stickiness good or bad?)
- Revenue: Did it unlock upsell / reduce churn? (business impact)
- Competitive: Did market move? (are we differentiated?)

Scenarios:

A) ADOPTION GROWING, RETENTION GOOD, REVENUE POSITIVE
   → Phase 2: Deep investment (polish, features, integrations)

B) ADOPTION FLAT, RETENTION DECLINING, REVENUE ZERO
   → Phase 2 delayed: Fix retention first (UX? Value prop?)

C) ADOPTION DECLINING, RETENTION POOR
   → Kill: Market rejected it

D) ADOPTION GOOD, BUT COMPETITORS LAUNCHED FASTER
   → Reevaluate: Still worth investing if differentiated? Or reallocate?
```

### Template: 3-Month Cohort Review

```markdown
# 3-Month Cohort Review: [Feature Name]

Date: [Date]
Feature shipped: [Date, 3 months ago]

## Adoption & Usage

- Peak adoption: 12% (Week 1)
- Current adoption: 18% (Week 12)
- Trend: Stable, slowly growing
- DAU: 4% (up from 3% Week 1)
- Weekly active: 12% (steady)

Interpretation: Early adopter phase; early majority starting to arrive

## Retention & Stickiness

- Week 4 retention: 60% of Week 1 activations still use weekly
- Week 12 retention: 45% of Week 1 activations still use weekly
- Churn: 3% of adopters churn/month (vs. 5% overall platform)

Interpretation: Adopters are stickier than average; good sign for Phase 2

## Revenue Impact

- Premium upgrade: 5% of adopters upgrade to premium (vs. 2% platform average)
- Churn reduction: $50K annual ARR impact from improved retention
- Upsell: 8 enterprise deals cite feature as differentiator

Interpretation: Clear revenue signal; ROI is positive

## Competitive Status

- Competitors launched? No (3-month lead time)
- Differentiation: Yes (we're 2-3 months ahead)
- Market maturity: Increasing (3 blog posts about this trend)

Interpretation: Still differentiated; window is open

## Decision

✅ **PROCEED to Phase 2**

Rationale:
- Adoption growing (12% → 18%)
- Retention strong (45% at 12 weeks)
- Revenue positive ($50K ARR)
- Still differentiated (no competitor equivalent)

Phase 2 Plan:
- Polish: Reduce bugs, improve UX (2 sprints)
- Features: Add [feature A], [feature B] (4 sprints)
- Marketing: Case studies, tutorials (ongoing)
- Goal: 40% adoption in 6 months

Risks:
- Competitors could ship equivalent in 2-3 months
- Our investment could be wasted if market moves
- Mitigation: Monitor competitive weekly; stay agile

Revisit: 6-month checkpoint (Phase 2 results)
```

---

## Kill Decision Framework

When should you kill a feature (even if it has some users)?

### Reasons to Kill

```
1. Adoption <20% after 2 weeks + can't fix in 1-2 more sprints
2. Retention declining (Week 4: 60%, Week 8: 30%)
3. Support burden > value (8 tickets/1000 users vs. 2 for platform average)
4. Competitor obsoleted it (we're no longer differentiated)
5. Strategic pivot (we don't believe in this market anymore)
6. Resource constraint (better bets exist)
```

### Reasons NOT to Kill (Keep Investing)

```
1. Adoption growing (12% → 18% → 25%) over time
2. Revenue positive (customers paying premium for it)
3. Retention strong (45%+ at 12 weeks)
4. Competitive advantage (we're 3-6 months ahead)
5. Flagship customers depend on it (revenue at risk if removed)
```

### Example: Kill vs. Keep Decision

```
Feature: "Advanced Reporting Dashboard"

KILL Decision:
- Adoption: 8% (below 20% threshold)
- Support: 12 tickets/1000 users (6x platform average)
- Retention: Declining (60% → 40% over 8 weeks)
- Reason: Users find it overwhelming, support burden too high
- Action: Remove feature, refund enterprise customers, reallocate team

KEEP Decision:
- Adoption: 30% (above 20% threshold)
- Support: 1.5 tickets/1000 users (below platform average)
- Retention: Growing (40% → 55% over 8 weeks)
- Revenue: $200K ARR from premium upgrade attributed to reporting
- Action: Invest in Phase 2 improvements, market aggressively
```

---

## Continuous Learning Backlog

Keep a list of assumptions that need testing.

```markdown
# Learning Backlog

## High Priority (Test Next)
- [ ] Mobile users have 25% higher retention (CRITICAL if true)
- [ ] Enterprise customers need SSO (blocking $5M TAM)
- [ ] Reporting drives 2x premium conversion (seems high, validate)

## Medium Priority (Test This Quarter)
- [ ] AI drafting would reduce email time by 20% (qualitative feedback positive)
- [ ] Dark mode increases evening usage by 15% (nice-to-have)

## Low Priority (Test if Time Permits)
- [ ] Community features reduce churn by 5% (unproven market)
- [ ] Localization increases APAC adoption (early market)

## Completed Learnings
- ✅ Mobile users ARE stickier (confirmed 25% higher retention)
- ✅ Enterprise does need SSO (14/15 prospects required it)
- ✅ Reporting drives premium but not 2x (confirmed 1.5x)
- ❌ AI drafting is premature (killed after 2-sprint test)
```

---

## Implementation Checklist

- [ ] For next feature ship: Plan a 2-sprint feedback loop
- [ ] Create decision tree before shipping (know your kill criteria upfront)
- [ ] Week 3 of release: Collect quantitative + qualitative feedback
- [ ] Week 4: Make explicit decision (Iterate, Pivot, or Kill)
- [ ] Document decision + evidence
- [ ] 3 months post-ship: Cohort review (proceed to Phase 2 or re-evaluate)
- [ ] Kill with clarity: Communicate why, thank early adopters, move on
- [ ] Monthly learning backlog review: Prioritize next assumptions to test
- [ ] Quarterly: Celebrate kills (they're learning opportunities, not failures)
