# Release Coordination for Product Owners

Coordinate releases across teams to minimize chaos, catch bugs early, and maintain the ability to rollback quickly.

---

## Dependency Mapping

Before shipping any release, identify what blocks what.

### Build Dependency Chart

```
Feature A (Mobile checkout)
  ├─ Depends on: Backend API (Feature B)
  └─ Blocking: Feature C (Mobile payments)

Feature B (Backend API)
  ├─ Depends on: Infrastructure work (Feature D)
  └─ Blocking: Feature A, Feature C

Feature C (Mobile payments)
  ├─ Depends on: Feature A (checkout), Feature B (API)
  └─ Blocking: Nothing

Feature D (Infrastructure)
  ├─ Depends on: Nothing
  └─ Blocking: Feature B, Feature A, Feature C
```

### Sequencing Decision

- Ship Feature D first (no dependencies)
- Then Feature B (depends on D)
- Then Feature A (depends on B)
- Then Feature C (depends on A and B)

### Weekly Blocker Check

Every Monday:
- What's blocking what?
- Has status changed since last week?
- Are we on track to ship in the right order?

---

## Staging Gates (Feature Must Pass Each)

Every feature goes through multiple gates before reaching production.

### Gate 1: Feature Complete (Dev Team)
- Feature code is written
- Internal testing by dev team (obvious bugs caught)
- Checklist:
  - [ ] Feature loads without errors
  - [ ] Basic happy path works
  - [ ] No obvious performance issues

### Gate 2: Design Review (Product + Design)
- Does the feature match the approved design?
- UI consistent with brand/existing patterns?
- Accessibility checklist passed?
- Checklist:
  - [ ] Visual design approved
  - [ ] Interaction patterns match spec
  - [ ] Mobile/responsive experience working
  - [ ] Accessibility standards met (WCAG)

### Gate 3: QA Gate (QA + PO)
- Feature passes all acceptance criteria
- No blockers/critical bugs
- Edge cases tested
- Checklist:
  - [ ] All acceptance criteria verified
  - [ ] No critical or blocker bugs
  - [ ] Edge cases tested (network failure, invalid input, etc.)
  - [ ] Performance acceptable (load time, memory)

### Gate 4: Product Approval (PO)
- Feature is ready to release
- Success metrics set up (monitoring ready)
- Launch checklist complete
- Checklist:
  - [ ] Success metrics defined and instrumented
  - [ ] Monitoring/alerting set up
  - [ ] Support team trained
  - [ ] Documentation ready
  - [ ] Rollback plan documented
  - [ ] Launch communication drafted

### Gate Review Meeting
Each gate has a defined owner who approves gate status.

```
Monday Release Gate Review:
Feature A status: ✅ Design review complete
Feature B status: ⚠️ QA gate blocking (3 high-priority bugs)
  Action: Dev team fixes by Wed; QA re-tests Thu
Feature C status: ❌ Feature not complete yet
  Action: Defer to next week or ship without it
```

---

## Risk Sequencing

For any release with multiple features, sequence by risk.

**High-risk features** (ship early, have more time to test):
- New technology/architecture
- Affects many users
- Complex edge cases
- Integration with external systems

**Low-risk features** (ship later, less testing needed):
- Bug fixes
- Minor UI improvements
- Isolated features
- Well-tested code

### Example Release Sequence

```
Week 1 (Mon-Tue): Ship high-risk Feature A (new payment processor)
  - Full week of testing + monitoring after release
  - Any issues found can be fixed before Feature B ships

Week 2 (Wed-Thu): Ship medium-risk Feature B (mobile redesign)
  - Less risky, but affects UX for all users
  - Two days of testing + monitoring

Week 3 (Fri): Ship low-risk Feature C (bug fix)
  - Low risk, can ship Friday without weekend concern
```

---

## Staged Rollout Strategy (Feature Flags)

Never ship features to 100% of users at once. Use feature flags.

### Phase 1: 1% of Users (Internal + Early Adopters)
- Goal: Catch critical bugs before wider impact
- Rollback: If any critical issues, flag off immediately
- Duration: 24-48 hours
- Monitor: Error rates, performance, support tickets
- Go/no-go decision: "Is this safe for 10%?"

### Phase 2: 10% of Users (Early Adopters)
- Goal: Validate performance at scale
- Rollback: If performance issues or high error rate
- Duration: 3-5 days
- Monitor: Error rates, performance, adoption metrics
- Decision: "Is this safe for 100%?"

### Phase 3: 100% of Users (Full Release)
- Rollback plan: Still documented (ability to flip flag off if catastrophic)
- Duration: Ongoing
- Monitor: Adoption, user feedback, business metrics

### Feature Flag Configuration (Example)

```
Feature: Mobile Checkout (ID: mobile-checkout-v1)

Flags:
- rollout_percentage: 1 (Phase 1)
- target_user_ids: [internal-employee-list, early-adopter-list]
- regions: [US] (start with one region first)
- on_error: "log_error + show_fallback" (fallback to web checkout)

Monitoring:
- Alert if error_rate > 5%
- Alert if page_load_time > 3s (vs. baseline 1s)
- Alert if checkout_completion_rate < 80% (vs. baseline 90%)

Owner: PO (can toggle flag)
Fallback plan: If any alert fires, PO flips flag off immediately
```

---

## Launch Readiness Checklist

Before ANY production deployment, verify this checklist is complete.

### Support Readiness
- [ ] Support team trained on new feature
- [ ] Support docs written (how to use, common issues)
- [ ] Support team has access to feature logs (for debugging)
- [ ] Support ticket template created (issue categorization)

### Marketing & Communications
- [ ] Customer announcement drafted (email, blog, in-app message)
- [ ] Sales team briefed (talking points for customers)
- [ ] Social media posts scheduled
- [ ] FAQ document written

### Documentation & Help
- [ ] User docs / help center updated
- [ ] Video tutorial recorded (if complex feature)
- [ ] Onboarding flow updated (if new user path)
- [ ] API documentation updated (if relevant)

### Monitoring & Alerting
- [ ] Success metrics instrumented (can we measure adoption?)
- [ ] Error alerts set up (notify oncall if errors spike)
- [ ] Performance alerts set up (notify oncall if slowdown)
- [ ] Dashboards created (real-time visibility to feature health)
- [ ] On-call team assigned (24-48 hours post-launch)

### Rollback Planning
- [ ] Rollback steps documented (how to turn off feature in <30 min)
- [ ] Rollback tested (practice it, don't just theorize)
- [ ] Communication template drafted (what we'll say if we rollback)
- [ ] Ownership clear (who makes rollback decision?)

### Team Coordination
- [ ] Release owner assigned (single person coordinating)
- [ ] Release window confirmed (date/time)
- [ ] Team on standby (no one on vacation during launch window)
- [ ] Communication channel open (Slack for live updates)

---

## Cross-Team Release Sync

**Weekly During Release Window (1 hour)**

**Attendees:** PM, PO, Design lead, Backend lead, Mobile lead, QA lead, Infra lead, On-call lead

**Agenda:**
1. **Status Update** (15 min)
   - What shipped last week?
   - What's in progress this week?
   - What will ship next week?

2. **Blockers** (15 min)
   - What's blocked? Who's blocked on what?
   - How do we unblock?
   - Timeline impact?

3. **Risks** (10 min)
   - New risks since last week?
   - Risk mitigation strategies?
   - Impact on release date?

4. **Go/No-Go Decision** (10 min)
   - Is deployment happening on schedule?
   - Any reason to defer?
   - Decision recorded in meeting notes

5. **Rollback Readiness** (5 min)
   - Rollback plan documented?
   - On-call team briefed?
   - Any concerns?

### Meeting Notes Template

```
Release Sync - Week of July 7, 2026

Status:
- Feature A: QA complete ✅
- Feature B: Design review in progress (complete by Wed)
- Feature C: Blocked on API (Feature A) (unblocks Thu)

Blockers:
- Feature B design review delayed 2 days (design bandwidth)
  → Mitigation: Design lead prioritizing tomorrow
  → Impact: Shifts B launch from Fri to Mon

Go/No-Go:
- Feature A: GO (Thu release)
- Feature B: CONDITIONAL GO (if design complete by Wed)
- Feature C: GO (Mon release, depends on A)

Risk:
- Feature A involves new payment processor (high risk)
  → Mitigation: 1% rollout, monitoring for 48hr
  → Rollback plan: Documented and tested
```

---

## Rollback Planning & Execution

Every release should have a tested rollback plan.

### Pre-Release Rollback Plan Document

```markdown
# Rollback Plan: Mobile Checkout (v1.2)

## Trigger Conditions (When We Rollback)
- Error rate > 10% (vs. baseline 0.5%)
- Checkout completion rate < 75% (vs. baseline 92%)
- 10+ support tickets about checkout in first hour
- Manual decision by PO (unexpected issue)

## Rollback Steps
1. PO notifies team: "Initiating rollback"
2. Platform lead: Flip feature flag to 0% (disable for all users)
3. Wait 5 minutes, monitor error rates
4. If error rate doesn't drop → Rollback code (git revert, deploy)
5. Notify team: Rollback complete

## Timing
- Steps 1-2: <5 min
- Error verification: 5 min
- Total: <10 min target

## Communication
- Slack #releases: "Rollback initiated at [time]"
- Customer email: "We encountered an issue and rolled back. Working on fix."
- Post-mortem: Scheduled for [date]

## Who Makes Decision
- PO (primary)
- Eng lead (if metrics unclear)

## Owner
- Release owner coordinates
- Platform lead executes flag change
- Eng lead executes code rollback if needed
```

### Rollback Drill (Pre-Release)

Before launching:
1. Simulate rollback (with feature flag off)
2. Verify error rates drop
3. Verify users see fallback experience
4. Time the process (goal: <10 min total)
5. Outcome: Team confident in rollback

---

## Common Release Patterns

### Pattern 1: Single Feature Release
- One feature, one team, simple deployment
- Rollback: Flip feature flag off
- Testing: 2 hours pre-release

### Pattern 2: Multi-Team Release
- Multiple features from multiple teams
- Dependency mapping critical
- Rollback: Flip all feature flags off
- Testing: Daily staging gates, full release sync

### Pattern 3: Infrastructure Release (High Risk)
- Database migration, code deployment, or infrastructure change
- Sequencing: Infrastructure → Feature A → Feature B
- Rollback: May take hours (data reversion complex)
- Testing: Extensive pre-release validation

### Pattern 4: Emergency Hotfix
- Critical bug, need to rollback and fix
- Triggers: Data corruption, security issue, widespread outage
- Process: Rollback immediately, assess damage, fix and re-deploy
- Decision: Release owner decides if hotfix or revert

---

## Monitoring Post-Launch

For 24-48 hours after launch:

**Every 30 Minutes:**
- Error rate (spike detection)
- Performance (load time, response time)
- Adoption (% of users hitting feature)
- Support tickets (issue clustering)

**Decision Points:**
- Error rate spike → Investigate or rollback
- Performance degradation → Investigate or rollback
- Low adoption → Investigate (is discovery broken?)
- High support volume → Investigate or rollback

**On-Call Team:**
- Available for decisions (don't wait for morning standup)
- Has runbook for common issues
- Can escalate to PO for rollback decision

---

## Implementation Checklist

- [ ] Document dependencies before release
- [ ] Set up staging gates for all features
- [ ] Create feature flag configuration
- [ ] Complete launch readiness checklist
- [ ] Document rollback plan + test it
- [ ] Schedule weekly release sync
- [ ] Brief on-call team on feature
- [ ] Set up monitoring + alerts
- [ ] Create support documentation
- [ ] Draft customer communication
- [ ] Post-release: Monitor every 30 min for 48 hours
- [ ] Post-release: Schedule retrospective (what went well/poorly?)
