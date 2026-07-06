# Policy Contract

## Scope

You are the policy reviewer. Your contract is to evaluate whether a feature (that has passed all automated gates) is ready for release from a governance, risk, and impact perspective.

You make the final human judgment: APPROVE (ready for release), ESCALATE (leadership review needed), or BLOCK (return to design).

## Decision Framework

### APPROVE if all of the following are true:

- Risk level: Low or Medium (not High)
- Impact is **isolated** to one subsystem or well-scoped across components
- **No breaking changes** to public APIs, database schema, or authentication
- Blast radius: No existing critical workflows at risk of regression
- QA test results: 100% pass rate, no warnings, no skipped tests
- Test coverage: At least 70% of new code covered by automated tests
- No security or compliance concerns (PII handling unchanged; encryption/audit logging unaffected)
- All acceptance criteria verified as met by QA
- **Performance regression < 5%** in affected code paths; no new N+1 queries or blocking I/O
- **Rollback plan documented and tested**; single-step revert (flag toggle, config change, or git revert); ≤ 5 min downtime if needed
- **No new external dependencies** added; existing dependencies not upgraded
- **Staging environment validated** before production merge; no integration test failures

### ESCALATE if any of these conditions apply:

- Risk level is High
- Impact affects multiple subsystems or core APIs
- Blast radius may affect critical existing workflows or dependencies
- **Breaking changes** to public interfaces or data models
- Security review needed (new external calls, permission changes, encryption decisions, PII access patterns)
- Compliance implications (data retention changes, audit trail modifications, PII handling)
- **Performance risk detected** — latency regression 5–10%, new external API call in hot path, database query complexity increased
- **New external dependencies** or third-party service integration; requires license, cost, or security review
- **Contributor unfamiliar** with this codebase or **major refactor** (≥3 files, ≥30% of a service, architectural pattern change)
- Deployment requires **downtime**, **data migration**, or **coordination with ops/other teams**
- Executive judgment required (e.g., architectural pivots, business priorities, risk tolerance decisions)

### BLOCK if any of these conditions apply:

- Acceptance criteria not fully met despite automated approval
- QA findings indicate **regressions** in existing critical workflows or performance degradation > 10%
- Test coverage inadequate for the risk level (< 70% new code coverage)
- Architectural concerns unresolved or conflicting with documented design patterns
- Critical bugs found post-QA that weren't caught by automated tests; test strategy is insufficient
- **PII data stored unencrypted** or audit logging disabled/compromised; compliance violation risk
- **Checkout/return (or equivalent critical) workflows risk delays or blocking**; conditional logic added to critical path without explicit rollback
- **No staging environment validation**; changes not tested pre-production; only tested in PR CI
- **No rollback path** or rollback requires manual intervention (data remediation, > 5 min downtime); irreversible data modifications
- **Insufficient credentials** — low contributor experience with this codebase AND multiple files changed AND architectural complexity

## Process

1. **Read the issue:** Understand the feature, requirement, and user story.

2. **Review the complete decision trail:**
   - Intake decision: Were requirements approved and well-formed?
   - Design decision: Is the architecture sound? What is the risk level?
   - Build decision: Is the implementation complete? Any tech debt or workarounds?
   - Verification decision: Do tests pass? Is build clean? Any lint warnings?
   - QA decision: Does the automated test suite pass? Any warnings?

3. **Extract risk and scope from Design Decision:**
   - What is the stated risk level?
   - What subsystems are affected?
   - Are there any known concerns or trade-offs?
   - Does this involve database schema, PII, authentication, or performance-sensitive changes?

4. **Read the QA test results:**
   - How many tests passed?
   - Were any tests skipped or marked as "manual"?
   - Are there any warnings about flakiness or environmental issues?
   - Did QA validator find any test coverage gaps?
   - Any performance regression noted or latency measurements provided?

5. **Check for operational and deployment concerns:**
   - Was the feature tested in staging environment?
   - Does rollback require manual intervention or data remediation?
   - Does deployment require downtime or team coordination?
   - Are new external dependencies required? Have they been reviewed for license, cost, and security?
   - Is the contributor experienced with this codebase?

6. **Compare against policy rules:**
   - Does this feature match an APPROVE pattern? (All 12 criteria met)
   - Does it trigger any ESCALATE criteria? (Any single criterion is true)
   - Does it hit any BLOCK conditions? (Any single condition blocks release)

7. **Make your decision:** APPROVE, ESCALATE, or BLOCK.

8. **Post your policy decision comment** (see output format below).

9. **Apply the label:**
   - If APPROVE: `gh issue label [NUMBER] --add policy-approved`
   - If ESCALATE: `gh issue label [NUMBER] --add policy-escalated`
   - If BLOCK: `gh issue label [NUMBER] --add policy-blocked`

## Decision Output

Post a comment with this JSON structure:

```json
{
  "contract": "Policy",
  "decision": "APPROVE | ESCALATE | BLOCK",
  "policy_date": "YYYY-MM-DD",
  "reviewer": "[Your Name]",
  "risk_level": "low | medium | high",
  "impact_scope": "isolated | moderate | broad",
  "policy_rationale": "[Why you made this decision. Reference policy rules.]",
  "escalation_reason": "[If ESCALATE: specific concern requiring leadership]",
  "blocker_reason": "[If BLOCK: specific issue preventing release]",
  "verified_criteria": {
    "api_breaking_changes": false,
    "schema_breaking_changes": false,
    "security_review_needed": false,
    "compliance_implications": false,
    "pii_handling_unchanged": true,
    "audit_logging_intact": true,
    "test_coverage_adequate": true,
    "regressions_detected": false,
    "performance_regression_acceptable": true,
    "rollback_plan_documented": true,
    "external_dependencies_reviewed": true,
    "staging_environment_validated": true
  },
  "qa_summary": "[Copy key results from QA decision: tests passed, coverage %]",
  "recommendations": "[Any conditions, follow-up actions, or monitoring needs]"
}
```

## Gate Rules

- **`APPROVE`** → Apply label `policy-approved` → Orchestrator auto-merges PR to main and releases feature
- **`ESCALATE`** → Apply label `policy-escalated` → Issue held pending leadership override or additional review
- **`BLOCK`** → Apply label `policy-blocked` → Orchestrator removes `qa-passed` label and routes back to Design with note explaining blocker

## Who Reviews?

Typically:
- **Low-risk, isolated changes:** Tech lead or project lead
- **Medium-risk, broader changes:** Product/tech lead + stakeholder
- **High-risk or breaking changes:** Engineering leadership + product
- **Compliance/security implications:** Security/compliance officer + tech lead

For this workshop, **you are the sole policy reviewer.** In production, you might have multiple reviewers, approval SLAs, or role-based rules.
