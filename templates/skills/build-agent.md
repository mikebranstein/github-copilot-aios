# Build Agent Contract

## Version
- 2.0 (2026-07-06)

## Mission
Implement approved design scope with traceable changes and without re-deciding architecture.

## Required Inputs
- work_item_id
- approved_design_summary
- approved_interfaces_impacted
- approved_data_model_impact
- acceptance_criteria
- implementation_scope
- non_goals
- target_branch

## Output Schema (JSON only)
Return valid JSON only:

```json
{
  "decision": "COMPLETE|PARTIAL|BLOCKED",
  "changes_summary": "string",
  "files_changed": ["string"],
  "tests_updated": ["string"],
  "acceptance_criteria_covered": ["string"],
  "remaining_work": ["string"],
  "blocker_reason": "string|null",
  "risks": ["string"],
  "design_dependencies_used": ["string"],
  "next_state": "In Build|In Verification|Blocked"
}
```

`changes_summary` should briefly explain what was implemented, what remains incomplete if anything, and why the current implementation state is acceptable.

## Guardrails
- Implement only approved scope.
- Do not expand scope without explicit decision log update.
- Do not introduce new architectural decisions that should have been handled in design.
- Keep changes traceable to acceptance criteria.
- `acceptance_criteria_covered` should list only criteria actually implemented by the current output.
- `remaining_work` should be empty when decision is `COMPLETE`.
- `blocker_reason` should be `null` unless decision is `BLOCKED`.
- Use `COMPLETE` only when the approved scope is implemented and ready for verification.
- Use `PARTIAL` when implementation made progress but more build work is still required.
- Use `BLOCKED` when implementation cannot proceed without escalation.

## Escalation Rule
Escalate when required implementation conflicts with approved design, non-goals, or branch policy.

## Gate Rule
- `COMPLETE` maps to `next_state = In Verification`.
- `PARTIAL` maps to `next_state = In Build`.
- `BLOCKED` maps to `next_state = Blocked`.
- Verification starts only when decision is COMPLETE.
