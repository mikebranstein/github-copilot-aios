# Cross-Functional Workflows for Product Owners

Clear workflows with each function (Design, BA, Eng, QA, Marketing) prevent handoff problems.

---

## Workflow 1: PO ↔ Product Manager (Strategic → Tactical)

### The PM Creates Strategic-Opportunity Issue

**What PM Provides:**
- Market research findings (customer interviews, support tickets, competitive analysis)
- Customer validation strength (1-2 customers vs. 10+ customers)
- Strategic alignment (which OKR does this support?)
- Recommended decision (CHAMPION / DEFER / BLOCK)
- Effort estimate (rough sizing)

**Example Issue:**
```
Title: [strategic-opportunity]: Mobile App for Field Teams

Research Summary:
- 12 support tickets from field teams ("can't check out from phone")
- 4 customer interviews (all field-heavy segments)
- Competitive analysis: 3 competitors have mobile (we don't)
- Market signal: 40% of leads are field-based teams

Validation:
- 4 customers who would adopt immediately (willing to beta)
- 8 additional customers potentially interested
- Competitive differentiation: 3-month lead if we ship first

PM Decision: CHAMPION (strong market signal, strategic alignment with Q3 mobile pillar)

Rough Effort: 10 weeks (backend API + iOS + Android)
```

### PO Reviews and Asks Clarifying Questions

**PO's Perspective:** \"Does this deserve backlog space?\"

**Questions to Ask:**
- \"How strong is the validation? 4 customers willing to beta, but what about the other 8? Do you have a commitment level?\"
- \"What's the competitive advantage timeline? If we don't ship in 10 weeks, can competitors copy in that time?\"
- \"Does this fit with our Q3 goals? Which OKR?\"
- \"What's the revenue impact? Per-customer value? Potential TAM?\"
- \"Is 10 weeks realistic? Any dependencies or technical risks?\"

**PO Decides:**
- Accept PM recommendation (move to backlog)
- Ask for more validation before deciding
- Override if backlog doesn't have capacity
- Suggest alternative scope (MVP instead of full app)

### PO Creates Feature-Request Issue

Once accepted, PO converts strategic-opportunity into tactical feature-request:

```
Title: [feature-request]: Mobile App - MVP: iOS Checkout for Field Teams

Linked to: strategic-opportunity #[X]

User Story:
As a field manager, I want to check out equipment from my iPhone,
so that I don't need to return to office.

Problem:
Field teams do 40% of checkouts. Currently they:
- Return to office (30 min roundtrip) to use web checkout
- Use colleague's phone (inefficient)
- Checkpoint location doesn't have WiFi (offline needed)

Value Scores:
- User value: 5 (solves critical pain)
- Business value: 4 (12 support tickets, revenue enabler)
- Complexity: 4 (new platform, backend changes)
- Priority score: (5+4)/(4*1.5) = 1.2 (Strategic bet)

MVP Scope:
- iOS only (Android in Phase 2)
- Checkout flow only (no browse/search)
- Online + offline support (basic caching)
- Estimated effort: 6 weeks (vs. 10 weeks for full app)

Success Metrics:
- 50% adoption among field teams in first month
- Completion rate 85% (vs. web 90%)
- Churn reduction for field teams: 5% improvement
```

---

## Workflow 2: PO ↔ Business Analyst (Feature → Acceptance Criteria)

### PO Provides (High-Level)

```
[feature-request]: Mobile App - iOS Checkout

User Story: As a field manager, I want to checkout equipment from my iPhone so I can avoid office trip.

Problem: Field teams currently return to office or ask colleagues to checkout.

Key Requirements:
1. Support offline (no WiFi at sites)
2. Auto-sync when reconnected
3. Reduce checkout time from 5 min (web) to 2 min (mobile)
4. Work on iPhone 11+ (not iPad)
```

### BA Asks Clarifying Questions

**In comments on feature-request issue:**

```
@[PO], I have clarifying questions before writing acceptance criteria:

1. Offline handling:
   - User can checkout without network connection?
   - What happens if network drops mid-checkout?
   - Can they complete later when reconnected?

2. Data sync:
   - If two phones both checkout same item offline, what happens?
   - Who wins? (First one syncs? Latest overwrites?)

3. Performance:
   - \"Reduce to 2 min\" - is this end-to-end checkout time?
   - Or just the mobile app interaction (not including backend)?

4. Device support:
   - iPhone 11+ means iOS 13+?
   - What about Android later - should we design for that now?

5. Features:
   - Can users browse equipment on mobile or just checkout known item?
   - Do they need receipt/confirmation on phone?

Let's sync to clarify before I write AC.
```

### PO Responds

```
Great questions! Here are my answers:

1. Offline handling:
   - Yes, complete checkout without network (core requirement)
   - If network drops mid-checkout: Auto-save and resume when reconnected
   - Yes, they can complete later

2. Data sync:
   - Shouldn't happen in practice (each person checks out different items)
   - If it does: Second checkout fails with \"item already checked out\" message (team resolves in system)

3. Performance:
   - End-to-end time from \"open app\" to \"confirmation screen\"
   - Backend should be fast; UX should be minimal clicks

4. Device support:
   - iOS 13+ correct
   - For Android: We'll decide in Phase 2 (don't design for now, but don't block it)

5. Features:
   - MVP: They know what they're checking out (ID only)
   - Search/browse added in Phase 2
   - Receipt: Yes, show on screen + email confirmation
```

### BA Writes Acceptance Criteria

```
## Acceptance Criteria (Given/When/Then)

### Scenario 1: Normal Checkout (Online)
Given: User has open network connection
When: User enters equipment ID and taps \"Checkout\"
Then: Equipment checked out + receipt shown within 2 seconds

### Scenario 2: Offline Checkout
Given: User has no network connection
When: User enters equipment ID and taps \"Checkout\"
Then: 
  - Checkout queued locally (not confirmed yet)
  - User sees \"Offline - will sync when connected\" message
  - Receipt shows \"Pending sync\"

### Scenario 3: Auto-Sync When Reconnected
Given: User checked out equipment while offline
And: Network is now available
When: User opens app or waits 10 seconds
Then:
  - Checkout syncs to backend (no user action needed)
  - Receipt shows \"Synced\"
  - Offline queue clears

### Scenario 4: Device Support
Given: User on iPhone 11 running iOS 13
When: User opens app
Then: App works correctly (all scenarios pass)

Given: User on iPhone 5 running iOS 10
When: User opens app
Then: \"This app requires iOS 13+\" error shown

### Scenario 5: Error Handling
Given: User enters invalid equipment ID
When: User taps \"Checkout\"
Then: Error message \"Equipment not found\" shown + form not cleared
```

### PO Approves

```
Approved! These AC are clear and testable. Ready for dev.

One clarification: In Scenario 3, can we make sync even faster?
(Thinking: Sync immediately upon reconnect, not wait 10 seconds)

Otherwise looks good.
```

---

## Workflow 3: PO ↔ Design (Feature Direction → Interaction Design)

### PO Articulates the Problem

```
[feature-request]: Mobile Checkout

Problem: Field teams need to check out equipment from their phone (no office visit).
Pain point: Currently takes 30 min roundtrip to office + 5 min checkout = 35 min lost per day.

Success Metrics:
- Adoption: 50% of field teams using mobile within 1 month
- Time: Checkout takes <2 minutes (vs. 5 min on web)
- Satisfaction: NPS 7+/10 from mobile users

User Context:
- Using iPhone 11+ (company-provided)
- In field (poor/no WiFi)
- Trying to quickly check out item and move on
- May be in loud environment (site conditions)

Design Constraints:
- Minimal taps (reduce error risk)
- Large touch targets (wearing gloves possible)
- Offline support (no network assumption)
```

### Design Explores Options

```
Design: \"Here are two approaches:

Option A: Simple / Minimal
- One screen: Enter ID + Tap Checkout
- Pros: Fastest, minimal error risk
- Cons: No browse, no undo, no confirmation

Option B: Full / Feature-rich
- Screen 1: Browse equipment list
- Screen 2: Item details + Confirm checkout
- Pros: Browse capability, confirmation reduces errors
- Cons: More taps, slower for power users

Which aligns with our goal?\"
```

### PO Decides

```
Option A for MVP (simple/minimal).

Reasoning:
- Field teams KNOW what they're checking out (they just used the item)
- Time is critical (2 min target)
- Browse can be Phase 2 feature

Design can explore Option B for Phase 2.
```

### Design Refines

```
Design: \"Got it. One more question:

What about edge cases?
- User taps Checkout twice by accident
- Invalid ID
- Network drops mid-submit

Sketches for each scenario: [mockup links]

Which approach feels right?\"
```

### PO Reviews & Approves

```
Mockups look great. I have feedback:

- Scenario 1 (double tap): Disable button after tap (prevent double-submit)
- Scenario 2 (invalid ID): Red error text + keep form filled (easy retry)
- Scenario 3 (network drop): Show \"Offline\" status + queued (they'll see sync later)

Otherwise approved. Ready for handoff to dev.
```

---

## Workflow 4: PO ↔ Engineering (Clarity → Implementation)

### PO Ensures Clarity

Before dev starts:
```
Dev Lead Checklist:
☑ Acceptance criteria are clear? (no ambiguities)
☑ Edge cases covered? (error scenarios documented)
☑ Dependencies understood? (does this need backend first?)
☑ Scope bounded? (not opening doors to scope creep)
☑ Questions answered? (no blockers on team)
```

### Dev Asks Clarifying Questions

```
@[PO], during standup we had questions:

1. Offline sync - does order matter?
   - If user checks out Item A then Item B offline
   - Should they sync in order? (Item A, then Item B)

2. Retry logic:
   - If checkout fails (network error), auto-retry?
   - Or wait for user to manually tap retry?

3. Scope:
   - Should we support camera scanning of ID (QR code)?
   - Or keyboard entry only for MVP?
```

### PO Answers

```
1. Order matters (field team track chronological sequence)
   - Sync in order (FIFO queue)

2. Auto-retry:
   - Yes, retry every 5 seconds for 2 minutes
   - Then show \"Offline\" status (user taps retry manually if still needed)

3. Scope:
   - Keyboard only for MVP
   - Camera/QR code in Phase 2 (more testing needed)

Let me know if you need clarity on anything else.
```

### Daily Standups

**Questions for PO (if any come up mid-sprint):**
```
Daily Standup:
- PO: \"Any blockers?\"
- Dev: \"We're unsure about the offline retry logic. Does this cover it?\" [shows code]
- PO: \"That looks right. But one edge case: if user taps retry 10 times, do we keep retrying? Or give up?\"
- Dev: \"Good point. Let's say: retry 2x then give up, show manual retry button.\"
- Decision made, dev unblocked.
```

---

## Workflow 5: PO ↔ QA (Acceptance Criteria → Test Cases)

### PO/BA Provides AC

```
Acceptance Criteria (Given/When/Then scenarios)

### Scenario 1: Offline Checkout & Auto-Sync
Given: iPhone has no WiFi, user opens checkout
When: User enters ID + taps \"Checkout\"
Then: Checkout queued locally + receipt shows \"Pending sync\"

When: WiFi reconnects
Then: Auto-sync triggers (no user action) + receipt shows \"Synced\"
```

### QA Designs Test Cases

```
Test Case 1.1: Offline Queue Persistence
- Setup: Clear app cache
- Steps:
  1. Turn off WiFi
  2. Check out Item A
  3. Check out Item B
  4. Force-quit app
  5. Reopen app
- Expected: Both items still queued (survives app restart)

Test Case 1.2: Auto-Sync on Reconnect
- Setup: WiFi off, queue has 2 items
- Steps:
  1. Turn WiFi on
  2. Wait 10 seconds
  3. Check app
- Expected: Both items synced (server shows checkouts)

Test Case 1.3: Edge Case - Duplicate ID
- Setup: WiFi off
- Steps:
  1. Check out Item A
  2. Check out Item A again (duplicate)
  3. Turn WiFi on
- Expected: Second checkout fails (\"Already checked out\") after sync
```

### PO Clarifies Edge Cases

```
@[QA], great test cases. One clarification:

Test Case 1.3: If Item A is already checked out, what should the user see?
- Option A: \"Error - Item already checked out\" (technical)
- Option B: \"Item A is checked out. Contact field supervisor if this is wrong.\" (user-friendly)

I prefer Option B. Please verify both via UX testing if possible.

Otherwise, test plan looks comprehensive.
```

---

## Meeting Cadences for Coordination

### Weekly Refinement Session (1 hour)

**Attendees:** PO, BA, Dev lead, Design  
**When:** Tuesday morning  
**Purpose:** Refine backlog for next sprint

**Agenda:**
1. **Review top 3-5 backlog items** (PO presents)
   - User story + problem statement
   - Value assessment
   - Any questions?

2. **BA clarifies requirements** (BA leads)
   - What questions do we have?
   - PO responds to clarify intent
   - Document any assumptions

3. **Design shows mockups** (Design presents)
   - Here's the interaction design
   - Edge cases covered?
   - PO approves or suggests changes

4. **Dev estimates effort** (Dev lead estimates)
   - How much work? 
   - Any risks?
   - Dependencies identified?

5. **PO makes scope trade-offs**
   - If effort too high: reduce scope or defer
   - If blockers: resolve or remove from sprint

**Outcome:** Sprint backlog is clear; dev ready to start Monday

### Daily Standups (15 min)

**Attendees:** Whole team  
**When:** 10 AM daily  
**Format:** Status + Blockers

```
- PO: \"What's the status on [Feature X]?\"
- Dev: \"We completed [part 1], working on [part 2]. One blocker: can you clarify [question]?\"
- PO: \"[Answers on the spot or says 'let me look into this']\"
- QA: \"No blockers from our side, ready to test whenever [Feature Y] is ready for QA\"
```

### Release Planning (1 hour, weekly during release window)

**Attendees:** PM, PO, Eng lead, QA lead, Infra lead  
**When:** Friday before release week  
**Purpose:** Coordinate release across teams

```
Agenda:
1. What shipped last week?
2. What ships this week?
3. Blockers? (dependency issues?)
4. Risks? (high-risk items need buffer time)
5. Go/no-go decision (safe to deploy?)
6. Rollback plan (tested and documented?)
```

### Post-Launch Review (1 hour, 2 weeks after launch)

**Attendees:** PO, PM, Eng, QA, Marketing  
**When:** After 2-week period  
**Purpose:** Evaluate feature health + learnings

```
Agenda:
1. Metrics review: Did we hit targets?
2. Support issues: Any blockers/complaints?
3. User feedback: NPS, CSAT?
4. Learnings: What would we do differently?
5. Decision: Iterate, scale, or kill?
```

---

## Cross-Functional Checklist

Before any feature ships:

- [ ] PO + PM aligned on strategic rationale
- [ ] BA reviewed acceptance criteria (clear + testable)
- [ ] Design approved interaction patterns (mockups)
- [ ] Dev estimated effort + identified risks
- [ ] QA designed test strategy (edge cases covered)
- [ ] Marketing prepared messaging (if external-facing)
- [ ] Support team trained (if new feature)
- [ ] Monitoring set up (success metrics instrumented)
- [ ] Rollback plan documented (tested)
- [ ] Weekly refinement complete (no surprises mid-sprint)

---

## Implementation Checklist

- [ ] Schedule weekly refinement session
- [ ] Establish daily standup routine
- [ ] Create feature-request issue template (PO → BA → Dev)
- [ ] Define acceptance criteria format (Given/When/Then)
- [ ] Design mockup review process
- [ ] Establish QA test case design process
- [ ] Weekly release planning during release window
- [ ] Post-launch review cadence (2 weeks after launch)
- [ ] Create communication channels (Slack, wiki for decisions)
- [ ] Document blockers log (track what's stuck)
