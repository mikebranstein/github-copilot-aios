# User Research & Personas: Documentation and Persistence

This skill defines how to systematically document, store, and access user research artifacts—personas, journey maps, and research findings—so they persist across quarters and inform ongoing strategic decisions.

## Problem Solved

PMs conduct valuable customer research every quarter, but if it's not stored systematically:
- Personas get lost or become stale
- Journey maps aren't updated when context changes
- Research findings can't be easily referenced during opportunity validation
- New team members can't access research history
- Strategic decisions lack clear research provenance

**Goal:** Central, versioned repository for all research artifacts, indexed for discovery and decision-making.

---

## Research Artifact Storage Structure

### GitHub Wiki as Your Research Hub (Recommended)

Create a dedicated **Research Wiki directly in your GitHub repository** using GitHub's built-in Wiki feature. This integrates research persistence with your code repository, making it a single source of truth for strategic context.

Use this folder structure:

```
Research Hub
├── Personas (Updated Quarterly)
│   ├── [Persona Name] — Facility Manager Frank
│   │   ├── Demographics & Firmographics
│   │   ├── Goals & Frustrations
│   │   ├── Success Metrics
│   │   ├── Interview Sources (N=8 in Q2, 5 in Q3, etc.)
│   │   ├── Key Quotes
│   │   └── Journey Map (links to dedicated journey map page)
│   ├── [Persona Name] — CFO Caroline
│   └── [Persona Name] — Technician Tyler
│
├── Journey Maps (Segment-Based)
│   ├── Facility Manager Journey
│   │   ├── Discovery Stage (friction points, emotional journey)
│   │   ├── Adoption Stage
│   │   ├── Regular Usage Stage
│   │   ├── Problem Resolution Stage
│   │   └── Churn Indicators
│   ├── CFO Journey
│   └── Technician Journey
│
├── Interview Transcripts & Findings
│   ├── Q2 2026 Research Cycle
│   │   ├── Interview 1 — Frank Johnson, ABC Manufacturing
│   │   ├── Interview 2 — Sarah Chen, XYZ Logistics
│   │   └── [Synthesis] Top 3 Pain Points Across 12 Interviews
│   ├── Q3 2026 Research Cycle
│   └── Q1 2025 Research Cycle (archived, reference-only)
│
├── Research-to-Decision Index
│   ├── "Equipment Loss Tracking" → Persona: Facility Manager Frank
│   │   → Journey Stage: Problem Resolution
│   │   → Research Quote: [link to interview]
│   │   → Strategic Opportunity: [link to GitHub issue]
│   ├── "Real-Time GPS Visibility" → Same journey map, same persona
│   └── [Pattern Matching: Which customer problems fuel which opportunities?]
│
└── Quarterly Research Summary
    ├── Q2 2026 Summary
    │   ├── Total Interviews: 12
    │   ├── Customer Segments: 3 (Facility Mgmt, Finance, Operations)
    │   ├── Major Themes: Equipment tracking, cost visibility, team coordination
    │   ├── Churn Signals: Low mobile adoption, hard-to-find features
    │   └── Strategic Implications: [links to updated strategic decisions]
    └── Q3 2026 Summary
```

---

## Persona Template

Use this template for every persona. Update quarterly as interview data changes.

```markdown
# Persona: [Name]
**Archetype:** [One-line description]
**Last Updated:** Q3 2026 (12 new interviews, total N=27 across all quarters)
**Review Frequency:** Quarterly (or when 5+ new interviews suggest persona shift)

## Demographics & Firmographics
- **Age Range:** 
- **Experience:** 
- **Company Size:** 
- **Industry/Role:** 
- **Geography:** 

## Primary Job to Be Done
"[Complete this sentence from customer interviews]
I want to [action] so that [outcome], because [constraint/fear]."

Example: "I want to see where equipment is in real-time so that I can recover it quickly, 
because equipment loss costs us $50K/year and creates emergency work."

## Goals & Success Metrics
- **Goal 1:** [What are they trying to achieve?]
  - Success metric: [How would they measure success?]
- **Goal 2:** 
  - Success metric: 

## Frustrations & Pain Points
- **Friction 1:** [What makes their job harder?] (Source: 8/12 interviews mentioned this)
- **Friction 2:** 
- **Churn Signal:** [What made past users leave?]

## Current Workflow
[How do they currently solve this problem? What tools do they use?]

## Decision-Making Process
- Who is involved in purchases? (Individual, procurement, budget holder?)
- What's the buying cycle?
- What blocks adoption?

## Key Quotes from Interviews
- "Quote 1" — [Customer Name, Interview Date]
- "Quote 2" — [Customer Name, Interview Date]
- "Quote 3" — [Customer Name, Interview Date]

## Interview Sources
- **Q2 2026:** 8 interviews (Frank Johnson, Sarah Chen, etc.)
- **Q3 2026:** 5 interviews (Carol Davis, John Martinez, etc.)
- **Cumulative:** 27 interviews across 4 quarters

## Connected Journey Map
See [Facility Manager Journey Map](link-to-journey-map) for end-to-end experience.

## Connected Strategic Opportunities
- [Equipment Loss Prevention](#) (GitHub issue #12)
- [Real-Time GPS Integration](#) (GitHub issue #34)
- [Cost Per Use Dashboard](#) (GitHub issue #56)

## Evolution Notes
- **Q2→Q3 shift:** Persona remains stable; increased focus on mobile access
- **Emerging need:** Reporting/analytics capability (new in 5/5 Q3 interviews)
```

---

## Journey Map Template

Create one journey map per customer segment. Update quarterly based on new research.

```markdown
# Journey Map: [Segment Name]

**Persona(s):** [Links to relevant personas]
**Last Updated:** Q3 2026
**Research Basis:** 12 interviews Q2 + Q3

## Stage 1: Discovery
**Goal:** User becomes aware product exists and understands core value.

**User Actions:**
- Searches for solution (Google: "equipment tracking software")
- Lands on marketing site
- Reads pricing/features page
- Watches 2-min demo video

**Emotional Journey:**
- Curiosity → Confusion → Cautious Interest

**Friction Points:**
- "What does this actually do?" — Homepage is vague (N=7 customers noted this)
- Hard to understand ROI before signup
- Competitive comparison unclear

**Opportunities:**
- Add case study with ROI calculation visible before login
- Create side-by-side competitor comparison
- Add interactive ROI calculator to homepage

**Metrics:**
- Traffic to website: [baseline]
- CTR on "Learn More" button: [baseline]
- Bounce rate: [baseline]

---

## Stage 2: Signup & Onboarding
**Goal:** User creates account and completes first setup.

**User Actions:**
- Clicks "Start Free Trial"
- Fills 7-field signup form
- Receives activation email
- Logs in to app for first time

**Emotional Journey:**
- Excitement → Form Fatigue → Blank Slate Anxiety

**Friction Points:**
- Form feels heavy (7 required fields; one customer said "I almost didn't proceed")
- No guidance on what to enter for "Team Size"
- First login shows empty dashboard — unclear where to start

**Opportunities:**
- Pre-fill from company integration (LinkedIn, Clearbit)
- Replace 7 fields with 3 fields + optional setup wizard
- Add interactive tutorial: "Add your first piece of equipment"

**Metrics:**
- Signup-to-activation rate: [baseline]
- Abandonment rate during form: [baseline]
- Time to first action: [baseline]

---

## Stage 3: Regular Usage (Daily)
**Goal:** User integrates product into daily workflow.

**User Actions:**
- Logs in 1-2x per day
- Checks equipment status
- Updates location or notes
- Exports reports for manager

**Emotional Journey:**
- Satisfaction → Occasional Frustration → Habit Formation

**Friction Points:**
- Mobile experience is clunky (desktop-first design; N=8 prefer mobile at work)
- Searching for equipment takes 20+ seconds (no full-text search)
- Reports lack filters (have to manually delete irrelevant rows)

**Opportunities:**
- Responsive mobile redesign (Phase 1 of mobile strategy)
- Full-text search on equipment page
- Add report filtering UI

**Metrics:**
- DAU / MAU: [baseline]
- Session length: [baseline]
- Feature usage: [which features get the most use?]

---

## Stage 4: Problem Resolution
**Goal:** User needs to recover missing or misplaced equipment.

**User Actions:**
- Searches equipment database by location or person
- Reviews location history
- Escalates to team for manual search
- Reports equipment lost (accounting writeoff)

**Emotional Journey:**
- Frustration → Desperation → Resignation

**Friction Points:**
- Search is slow; results not ordered by likelihood
- No alert system when equipment leaves designated zone
- GPS tracking requires manual setup per item (not batch-enabled)

**Opportunities:**
- Real-time GPS tracking (ties to Q2026 OKR: "Reduce equipment loss by 30%")
- Geofence alerts when equipment leaves facility
- Batch tagging for fleet tracking

**Metrics:**
- Time to locate equipment: [baseline 20+ min]
- Success rate (equipment recovered): [~70%]
- Equipment loss rate: [baseline $50K/year]

---

## Churn Signals
**When do users leave?**
- No mobile app (after 1 month of trial, 15% of field teams churn)
- Can't integrate with existing ERP (5 companies asked for Salesforce sync, didn't proceed)
- Reporting too rigid (CFO couldn't get the dashboard he needed)

**Retention Levers:**
- Mobile app launch
- Salesforce/ServiceNow integration
- Customizable dashboards
```

---

## Research-to-Decision Index

Maintain a living index that links research findings directly to strategic opportunities.

**Purpose:** When validating a new opportunity, quickly find:
- Which customers mentioned this pain?
- What stage of their journey does it affect?
- How many customers have this problem?
- What was the exact quote?

**Format:**

| Problem | Persona | Journey Stage | Interview Count | Research Quote | Strategic Opportunity | Decision |
|---------|---------|---------------|-----------------|----------------|-----------------------|----------|
| Equipment loss tracking | Facility Manager Frank | Problem Resolution | 8/12 interviews | "We lose $50K/year..." | [#12 Equipment Loss Prevention](link) | CHAMPION |
| Real-time visibility | Facility Manager Frank | Regular Usage | 7/12 interviews | "Takes 20+ minutes to search..." | [#34 Real-Time GPS](link) | CHAMPION |
| Mobile experience | Facility Manager Frank, Technician Tyler | Regular Usage | 8/12 interviews | "Desktop-first design doesn't work in field..." | [#45 Mobile Redesign](link) | CHAMPION |
| Cost per use reporting | CFO Caroline | Problem Resolution | 3/12 interviews | "I need to show CFO cost per use..." | [#56 Cost Dashboard](link) | STRATEGIC_BET |
| ERP integration | Facility Manager Frank, CFO Caroline | Adoption | 5/12 interviews | "We need Salesforce sync..." | [#78 Salesforce Integration](link) | DEFER (Q3 priority) |

---

## Quarterly Update Process

**Every quarter, follow this 5-step cycle:**

### 1. **Conduct 15-20 New Interviews** (ongoing)
   - Target: 1 per week (3-5 hours/week)
   - Segments: power users (sticky), at-risk (declining), churned, prospects
   - Transcribe every interview

### 2. **Synthesize Findings** (3-4 hours)
   - Create **Quarterly Research Summary** page
   - Identify themes: What problems recurred? (5+ mentions = strong signal)
   - List churn signals: What drove users away?
   - Highlight strategic implications: Does this shift our OKRs?

### 3. **Update Personas** (2-3 hours)
   - For each persona:
     - Review new interview data
     - Update interview count (cumulative)
     - Revise goals/frustrations if theme shifts
     - Add new quotes
     - Note evolution ("Q2→Q3 shift...")
   - Archive old versions (keep history)

### 4. **Revise Journey Maps** (3-4 hours)
   - For each stage:
     - Confirm friction points still valid (or emerged newly)
     - Update metrics (new baseline data)
     - Add new opportunities discovered
     - Flag resolved friction (if initiative shipped)

### 5. **Update Research-to-Decision Index** (1-2 hours)
   - New opportunities discovered → add to index
   - Opportunities validated by research → update decision column
   - Deferred opportunities → note why

**Recommended Cadence:**
- Week 1-4 of quarter: Conduct interviews
- Week 5: Synthesis sprint (summarize + update index)
- Week 6: Update personas & journey maps
- Week 7-8: Present findings to leadership; incorporate into strategic planning

---

## Continuous Wiki Maintenance

Research Wiki maintenance is not a quarterly task—it's an **ongoing responsibility**. The PM agent must keep the wiki current in real-time as it discovers customer insights. This ensures:

- Research findings don't get lost between quarterly reviews
- PO, BA, and dev teams have access to current research
- Strategic decisions are always traceable to customer evidence
- New team members can learn customer context from the wiki
- Themes and patterns emerge while interviews are still fresh

### Maintenance Schedule

**After Every Interview (Same Day)**

1. Add interview entry to `Interview-Transcripts-[Current-Quarter]` page:

```bash
# Update interview transcripts with new entry
gh wiki edit "Interview-Transcripts-Q3-2026" --body "
| Date | Customer | Role | Key Findings | Recording Link |
|------|----------|------|--------------|----------------|
| [Today] | [Name] | [Title] | [Summary of 3-5 key insights] | [Recording URL] |
```

2. Extract 1-2 direct customer quotes and add to the relevant persona page:

```bash
# Add quote to existing persona (append to "Key Quotes" section)
gh wiki edit "Personas-Facility-Manager-Frank" --body "
## Key Quotes from Interviews
- \"[Direct quote]\" — [Customer Name, Today's Date]
- [Other quotes...]
```

3. If interview reveals new friction point: Update relevant journey map stage immediately

---

**Weekly (After 3-4 Interviews)**

1. Scan interview notes for **emerging patterns**: Are 3+ customers mentioning the same problem?
2. If yes, add to `Research-to-Decision-Index` page with pattern count:

```bash
# Add new pattern to index
gh wiki edit "Research-to-Decision-Index" --body "
| Pattern | Persona | Stage | Count | Status |
| [New Pattern] | [Affected Personas] | [Journey Stage] | 3/4 mentions | EMERGING |
```

3. Increment persona interview count:

```bash
# Update persona interview metadata
gh wiki edit "Personas-Facility-Manager-Frank" --body "
**Interview Sources**
- Q2 2026: 8 interviews
- Q3 2026: [Updated count - e.g., 5 so far]
- Cumulative: [Running total - e.g., 13]
```

4. If journey map friction points confirmed: Update journey map stage notes

---

**Monthly (Week 4 of Month)**

1. Synthesize weekly updates into themes: Which patterns are "signal" vs. "noise"?
   - 5+ mentions of same problem = strong signal → Move to "Confirmed" status
   - 2-3 mentions = emerging → Keep as "Emerging"
   - 1 mention = outlier → Note but don't prioritize

2. Update personas with any theme shifts:
   - Add "Evolution Notes" if customer segment behavior changed
   - Move friction points up/down in priority based on frequency
   - Update "Churn Signals" section if you're seeing new patterns

3. Update Research-to-Decision Index: Promote patterns with 5+ mentions to "STRONG_SIGNAL"

---

**Quarterly (Full Synthesis Sprint, 8-10 Hours)**

Execute the full 5-step quarterly update process (documented above):

1. Synthesize all interviews from the quarter
2. Create/update `Quarterly-Summary-[Quarter]` page with themes, signals, implications
3. Update all personas with cumulative interview data
4. Revise all journey maps with new friction points
5. Re-assess Research-to-Decision Index: confirm patterns, note resolved items

---

### Wiki Update Checklist

Before moving a `strategic-opportunity` issue to "Ready for PO":

- [ ] All interview transcripts added to wiki (same day)
- [ ] Customer quotes extracted and linked to personas
- [ ] Pattern added to Research-to-Decision Index (if 3+ mentions)
- [ ] Strategic-opportunity GitHub issue links to wiki pages (persona, journey map, interview transcript)
- [ ] Example: "Research basis: [Facility Manager Frank](wiki-link) | [Problem Resolution](journey-map-wiki-link) | 8 interviews mention this"

---

### Sample Maintenance Commands (GitHub CLI)

```bash
# Add new interview to transcripts page
gh wiki edit "Interview-Transcripts-Q3-2026" --body "$(gh wiki view "Interview-Transcripts-Q3-2026" --format markdown)

**New Entry:**
| 2026-07-07 | Frank Johnson | Facility Manager | \"We lose \$50K/year in equipment. Mobile app would save us thousands\" | [recording-link] |"

# Update persona with new quote
gh wiki edit "Personas-Facility-Manager-Frank" --body "$(gh wiki view "Personas-Facility-Manager-Frank" --format markdown)

- \"We need real-time GPS visibility\" — Frank Johnson, 2026-07-07"

# Update Research-to-Decision Index with new pattern
gh wiki edit "Research-to-Decision-Index" --body "$(gh wiki view "Research-to-Decision-Index" --format markdown)

| Equipment Loss | Frank + Tyler | Problem Res | 5/8 interviews | \"We lose \$50K...\" | #12 Equipment Loss Prevention | STRONG_SIGNAL |"
```

---

## Tool Recommendations

### Primary: GitHub Wiki ✅ (Recommended for This Workshop)

**Why GitHub Wiki?**
- Integrated directly with your GitHub repository (single source of truth)
- Version-controlled (every change tracked, rollback available)
- Free for all team members
- Markdown-based (same format as agents and skills)
- Teams already familiar with GitHub for collaboration
- Links directly from GitHub issues to Research Wiki pages

**GitHub Wiki Setup:**

1. Go to your GitHub repository
2. Click **Settings** (top right)
3. Scroll down to **Features** section
4. Check the box next to **Wiki**
5. Click **Save**
6. Click the **Wiki** tab (now visible in repo navigation)
7. Click **Create the first page** to start

OR use GitHub CLI:
```bash
# Create a wiki page using GitHub CLI
gh wiki create "Home" --body "# Product Research Wiki

Central repository for customer research, personas, and journey maps."
```

**Page Structure in GitHub Wiki:**

Create pages with this naming convention (GitHub Wiki sidebar auto-links them):
```
Home (start here — links to all sections)
Personas-Facility-Manager-Frank
Personas-CFO-Caroline
Journey-Maps-Facility-Manager
Journey-Maps-CFO
Interview-Transcripts-Q2-2026
Interview-Transcripts-Q3-2026
Research-to-Decision-Index
Quarterly-Summary-Q2-2026
Quarterly-Summary-Q3-2026
```

**Linking from GitHub Issues to Research Wiki:**

In your strategic-opportunity or feature-request issues, add links like:
```markdown
## Research Basis
- **Persona:** [Facility Manager Frank](https://github.com/your-org/your-repo/wiki/Personas-Facility-Manager-Frank)
- **Journey Map:** [FM Journey - Problem Resolution](https://github.com/your-org/your-repo/wiki/Journey-Maps-Facility-Manager)
- **Key Quote:** From interview with Frank Johnson, Q2 2026
```

---

### Secondary: Notion (If You Prefer Richer Media)

If your team prefers visual research tools with embedded media:

**Pros:**
- Rich media support (images, videos, embeds)
- Advanced search + database mode for research indexing
- Better visual design for complex research
- Good for sharing with non-technical stakeholders

**Cons:**
- Separate from code repository
- Not version-controlled
- Paid for larger teams ($8-10/user/month)

**Setup:** Create Notion workspace, invite PM team, use templates provided in user-research-and-personas.md

**Note:** If using Notion, still link from GitHub issues to Notion pages for traceability.

---

### Not Recommended for This Workshop: Confluence

Confluence is enterprise-grade but adds complexity and cost for workshop purposes. Revisit if your organization already uses Confluence for documentation.

---

## Linking Research to Strategic Decisions

Every strategic opportunity created in GitHub should reference its research basis:

**In your `strategic-opportunity` issue, include:**

```markdown
## Research Basis
- **Persona(s):** Facility Manager Frank, CFO Caroline
- **Journey Stage:** Problem Resolution, Regular Usage
- **Interview Count:** 8/12 interviews in Q2 + Q3
- **Key Quote:** "We lose $50K/year in equipment..."
- **Link to Research:** [Wiki: Facility Manager Journey](research-wiki-link)
- **Churn Signal?** Yes — 15% of field teams churned due to lack of mobile
```

This creates a **direct chain** from research → opportunity → decision.

---

## Example: How This All Works Together

**Q2 Research Cycle:**
1. PM conducts 15 interviews with facility managers
2. Identifies theme: "Equipment tracking takes too long, we lose $50K/year"
3. Updates Facility Manager persona with 8 new sources
4. Updates journey map: Problem Resolution stage gets new friction points
5. Adds to Research-to-Decision Index: "Equipment loss" → 8 customers → Problem Resolution → CHAMPION

**Q2 Validation (PM Agent):**
1. PM sees new customer request for equipment tracking
2. Checks Research-to-Decision Index → finds 8 existing customer mentions
3. Gathers research: Reads persona, reviews journey map, pulls quotes
4. Validates opportunity: "Strong customer signal, aligns to strategic OKR"
5. Creates `strategic-opportunity` GitHub issue with research link
6. Issue is labeled `pm-opportunity`, ready for PO prioritization

**Q3 Update:**
1. PM conducts 5 new interviews; confirms theme persists
2. Updates Facility Manager persona: "N=13 across Q2+Q3"
3. Adds new friction point: "Mobile experience is blocker"
4. Updates journey map with mobile adoption as new Adoption Stage friction
5. PO uses this to prioritize related feature-requests ahead of others

**Long-term:**
- Year-over-year, research archive shows evolution of customer needs
- New team members can read research history without asking "What do we know about facility managers?"
- Every strategic decision has clear provenance (traceable to interviews)
- Quarterly re-evaluation cycles through same personas, showing shifts

---

## Validation Checklist

Before moving a research artifact to "Active" status:

- [ ] Persona based on 5+ customer interviews (not guesses)
- [ ] Journey map updated in last quarter
- [ ] 3+ friction points identified per stage
- [ ] Churn signals explicitly called out
- [ ] Strategic opportunities linked from research index
- [ ] Key quotes attributed to customers and dates
- [ ] Wiki/docs accessible to full product team
- [ ] Quarterly update schedule defined and tracked

