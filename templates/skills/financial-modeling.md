# Financial Modeling for Product Managers

Use financial models to quantify the business impact of product decisions. This guide covers 3-year revenue forecasting, unit economics, and financial impact assessment.

---

## 3-Year Revenue Projection Model

Build this annually. Update quarterly. This is your business forecast tied to product strategy.

### Inputs & Data Gathering

**Current State (baseline):**
- Monthly Active Users (MAU)
- Daily Active Users (DAU)
- Monthly churn rate (%)
- Average Revenue Per User (ARPU) monthly and annual
- Customer Acquisition Cost (CAC)

**Historical cohort data:**
- What % of users from Month 1 are still active in Month 3, 6, 12, 24?
- Build a retention curve for each cohort

**Growth assumptions:**
- How many new users per month?
- Will ARPU increase? (pricing changes, more features)
- Will churn improve? (product improvements)

### Example: 3-Year Model

**Current State (Jan 2026):**
```
- MAU: 50,000
- Monthly churn: 5%
- ARPU: $10/user/month ($120/year)
- CAC: $20 per user
- New users/month: 5,000

Cohort Retention (Jan 2026 cohort):
- Month 1: 100% (50,000 users) = $500K revenue
- Month 3: 92% (46,000 users)
- Month 6: 82% (41,000 users)
- Month 12: 60% (30,000 users)
- Month 24: 35% (17,500 users)
```

**3-Year Revenue Model:**

```
YEAR 1 (2026):
Jan cohort (50K users × 12 months): 
  - Jan: 50K × $10 = $500K
  - Feb: 46K × $10 = $460K (5% churn)
  - Mar: 42.7K × $10 = $427K
  - (Months continue declining)
  Subtotal: ~$4.2M for Jan cohort

Feb cohort (5K new users × 11 months):
  - Feb: 5K × $10 = $50K
  - Mar-Dec: Similar decline
  Subtotal: ~$450K

Mar-Dec cohorts: Similar (~$450K each)

Year 1 Total ARR: ~$6.5M

YEAR 2 (2027):
Jan 2026 cohort: Only 30% retained (15K users) = 15K × $120 = $1.8M
Feb 2026 cohort: 30% retained (1.5K users) = $180K
Mar-Dec 2026 cohorts: Similar
Full year of 2027 new cohorts (60K new users): ~$4M

Year 2 Total ARR: ~$8.5M

YEAR 3 (2028):
All 2026 cohorts: Mostly churned (10% retained)
All 2027 cohorts: 30% retained
Full year of 2028 new cohorts: ~$5.5M

Year 3 Total ARR: ~$10.2M
```

### Unit Economics Check

For each customer cohort, calculate:

**CAC Payback Period:**
- Formula: CAC / (Monthly ARPU)
- Example: $20 CAC / $10 ARPU = 2 months to payback
- Target: <12 months (ideally <6 months)
- What it means: How long until customer pays for acquisition cost?

**LTV:CAC Ratio:**
- Formula: Lifetime Value / CAC
- Example: ($10 × 36 months avg lifetime) / $20 = 18:1
- Target: >3:1 (ideally >5:1)
- What it means: For every dollar spent acquiring, how much revenue over lifetime?

**Example calculation:**

```
Customer cohort Jan 2026:
- ARPU: $10/month
- Churn: 5%/month
- Avg lifetime: 20 months (1 / 5% churn)
- LTV: $10 × 20 = $200
- CAC: $20
- LTV:CAC = $200/$20 = 10:1 (excellent)
- CAC payback: $20 / $10 = 2 months (excellent)
```

---

## Financial Impact of Individual Initiatives

For each major initiative, estimate financial impact. Use scenarios (optimistic, base, conservative).

### Example: Mobile App Decision

**Initiative:** Ship native mobile app

**Assumption:** Mobile users will have 25% higher retention, attract new mobile-first users.

**Scenario 1 (Optimistic):**
- Increases retention from 60% to 75% (15 point improvement)
- Attracts 1,000 new mobile-first users/month (vs. 500 today)
- Revenue impact Year 1: +$300K ARR; Year 2: +$2M ARR

**Scenario 2 (Base Case):**
- Increases retention from 60% to 70% (10 point improvement)
- Attracts 500 new mobile users/month
- Revenue impact Year 1: +$150K ARR; Year 2: +$1M ARR

**Scenario 3 (Conservative):**
- Increases retention from 60% to 65% (5 point improvement)
- Attracts 200 new mobile users/month
- Revenue impact Year 1: +$30K ARR; Year 2: +$300K ARR

**Build cost:**
- Engineering: 10 weeks
- Additional operating costs: +$1.5M/year (infrastructure, support, maintenance)

### ROI Analysis

```
Scenario: Base Case
- Year 1 new revenue: +$150K
- Year 1 cost: -$1.5M (build + ops)
- Year 1 net: -$1.35M (loss)

- Year 2 new revenue: +$1M
- Year 2 cost: -$1.5M (ops only)
- Year 2 net: -$500K (loss)

- Year 3 new revenue: +$1.8M
- Year 3 cost: -$1.5M (ops)
- Year 3 net: +$300K (profit)

ROI Analysis:
- Payback period: 2.5-3 years
- 3-year cumulative: -$1.55M (net loss)

BUT: Strategic benefit: Mobile is competitive moat; stickiness improves company health
Decision: CHAMPION as strategic investment (not pure ROI play)
```

---

## Quarterly Financial Review

Track actual vs. forecast monthly. Update quarterly.

**Template:**

```
Q2 2026 Financial Review

Forecast (from Q1):
- Year 1 ARR: $6.5M
- Churn: 5%/month
- CAC payback: 2 months

Actual (Q2 2026 results):
- Current ARR: $6.2M (-$300K vs. forecast)
- Churn: 6%/month (+1% worse)
- CAC payback: 2.5 months (+0.5 month worse)

Analysis:
- Churn increased (why? Did we break something? New competitor?)
- CAC payback worsened (are we spending more on acquisition?)
- ARR tracking below forecast (combination of lower MAU + higher churn)

Action:
- Investigate churn spike: customer interviews, support analysis
- Review CAC spending: is sales team spending more per customer?
- Update 3-year forecast (lower growth, higher churn = $5.8M Year 1)
- Adjust strategy: focus on retention, not just acquisition

Next quarter: Monitor churn closely; may need to ship retention features
```

---

## Break-Even & Profitability Analysis

When will your product be profitable?

**Formula:**
- Revenue - Costs = Profit
- When Revenue > Costs: Break-even

**Example:**

```
2026:
- Revenue: $6.5M
- COGS (server, hosting): $1.3M (20%)
- Sales & marketing: $2M
- Operations (support, ops): $1M
- R&D (engineering, product): $1.5M
- Total costs: $5.8M

Gross profit: $6.5M - $1.3M = $5.2M (80% gross margin)
Net profit: $6.5M - $5.8M = $0.7M (11% net margin)

Status: Profitable (barely)
```

**Long-term path:**
- Year 1: 11% net margin (profitable)
- Year 2: 18% net margin (ops leverage)
- Year 3: 25% net margin (scale)

---

## Sensitivity Analysis

What if assumptions are wrong?

**Question:** "How sensitive is our forecast to changes in churn?"

**Example:**

```
Base case:
- 5% monthly churn
- Year 1 ARR: $6.5M

If churn increases to 6%:
- Year 1 ARR: $5.8M (-$700K)

If churn decreases to 4%:
- Year 1 ARR: $7.5M (+$1M)

Insight: We're highly sensitive to churn. Every 1% change = ~$700K impact.
Priority: Focus on retention features, not just acquisition.
```

---

## Financial Model Template

Use this for quarterly planning.

**Inputs:**
- [ ] Current MAU, DAU, churn %
- [ ] ARPU (monthly and annual)
- [ ] CAC
- [ ] Cohort retention curve
- [ ] Projected new users/month
- [ ] Operating costs breakdown

**Calculations:**
- [ ] 3-year ARR forecast
- [ ] CAC payback period
- [ ] LTV:CAC ratio
- [ ] Gross margin %
- [ ] Net margin %
- [ ] Break-even timeline

**Scenarios (for major initiatives):**
- [ ] Optimistic case
- [ ] Base case
- [ ] Conservative case
- [ ] ROI analysis for each

**Review Cycle:**
- [ ] Monthly: Track actual vs. forecast
- [ ] Quarterly: Update assumptions + rebuild forecast
- [ ] Annually: Long-term strategic implications

---

## Key Insights

1. **Unit economics matter more than total size.** A 10:1 LTV:CAC ratio at $1M ARR beats 3:1 at $10M ARR.

2. **Churn is your biggest lever.** 1% change in churn = massive revenue swing. Retention > acquisition.

3. **CAC payback drives runway.** If payback is 12+ months, you're burning cash fast. <6 months is ideal.

4. **Forecast is always wrong.** Use it to understand sensitivities, not predict perfectly. Update quarterly.

5. **Margin improves with scale.** Same $2M fixed costs spread over $10M revenue (20% cost) vs. $5M revenue (40% cost).

---

## Implementation Checklist

- [ ] Gather cohort retention data (download from analytics)
- [ ] Calculate current unit economics (LTV, CAC, payback)
- [ ] Build 3-year forecast model
- [ ] Run financial review quarterly
- [ ] For each major initiative: model 3 scenarios
- [ ] Identify 1-2 sensitivities that matter most
- [ ] Share quarterly forecast with CFO + board
- [ ] Update annually
