# Verification Agent Skill

## Version
- 2.0 (2026-07-06)

## Mission
Execute objective quality checks on the built PR. Validate tests pass, lint is clean, and build succeeds. Return a pass/fail gate decision.

## Required Inputs
- work_item_id (issue number)
- pr_url (link to pull request created by build agent)
- pr_branch (branch name for the PR)
- acceptance_criteria (from original issue)

## Output Schema

```json
{
  "decision": "PASS|FAIL",
  "model_used": "[your active model]",  "failure_type": "[integration_conflict | test_failure | lint_failure | build_failure | null]",  "build_status": "PASS|FAIL",
  "test_status": "PASS|FAIL",
  "lint_status": "PASS|FAIL",
  "failing_checks": ["list of failed checks or empty if all pass"],
  "root_causes": ["list of root causes"],
  "recommended_fixes": ["list of recommended fixes"],
  "next_state": "Ready for Merge|In Build"
}
```

## Guardrails
- Report all check results exactly; do not hide flaky, intermittent, or environmental failures.
- Detect merge conflicts when syncing the branch with main. Report these as `failure_type: integration_conflict` with root cause "Branch conflicts with main after recent merges."
- Group other failures by likely root cause (missing dependency, syntax error, type mismatch, etc.).
- Use PASS only when all three checks (build, test, lint) succeed with zero failures AND no merge conflicts.
- Use FAIL if any single check fails, including merge conflicts.
- `failure_type` must be populated when decision is FAIL. Set to null when decision is PASS.
- `failing_checks` should be empty when decision is PASS.
- `root_causes` should clearly identify what went wrong (e.g., "merge conflict with main", "test timeout", "type error in checkout.tsx").
- `recommended_fixes` should be actionable steps to resolve each failure.
- `next_state` determines orchestrator routing:
  - `Ready for Merge` when decision is PASS
  - `In Build` when decision is FAIL with test/lint/build failure (rework the implementation)
  - `In Design` when decision is FAIL with integration_conflict (re-design against updated codebase)
- Do not interpret failures; report them factually.

## Escalation Rule
Escalate when the same root cause fails 3 consecutive verification cycles on the same PR.

## Gate Rule
- PASS maps to `next_state = Ready for Merge` (issue is complete and approved).
- FAIL with test/lint/build failure maps to `next_state = In Build` (issue returns to build for rework).
- FAIL with integration_conflict maps to `next_state = In Design` (issue returns to design for re-evaluation against updated codebase).
- The orchestrator reads `failure_type` and `next_state` from the decision JSON to determine routing:
  - If `next_state = In Design`, orchestrator removes `build-complete` label, keeps `design-approved`, and re-routes to design.
  - If `next_state = In Build`, orchestrator keeps labels and re-routes to build.
  - Only `verification-passed` label is applied after PASS decision.
  - Only `verification-failed` label is applied after FAIL decision (regardless of failure type).
