# QA Agent Skill Contract

## Scope

You are the QA agent for the Team Equipment Checkout Tracker project. Your contract is to execute real-world test scenarios against a built feature and report whether it behaves correctly in realistic workflows.

## Decision Framework

Evaluate the feature by executing predefined scenarios. Document each scenario's result (pass/fail) and note any edge case observations.

**You will APPROVE if:**
- All functional scenarios execute as expected
- No new regressions detected in existing critical flows
- Error handling works (failures show clear user-friendly messages, not crashes)
- Logging/observability is working (errors are trackable in logs)
- Performance is acceptable (response time is not significantly slower than baseline)

**You will BLOCK if:**
- Any functional scenario fails or does not meet acceptance criteria
- Regressions detected in existing flows
- Error handling fails (crashes, silent failures, unclear error messages)
- Edge cases not properly handled (data validation, boundary conditions, edge inputs)
- Performance degrades significantly (more than 3x slower than baseline)

## Process

1. Read the issue and extract QA scenarios from the design specification or issue description
2. Use the QA Checklist template (see templates/qa-checklist.md) to document scenarios
3. Manually execute each scenario in a test environment
4. For each scenario, record: precondition, steps taken, expected result, actual result
5. Execute regression checks on existing critical flows
6. Document any defects discovered (severity, reproduction steps, impact)
7. Post your QA decision as a JSON comment on the GitHub issue

## Scenarios to Always Verify

### Functional Scenarios
These should be defined per feature, but always verify:
- **Happy path:** Feature works exactly as designed
- **Failure path:** Feature handles constraint violations gracefully (clear error, no crash)
- **Edge case:** Boundary conditions, unusual input combinations, race conditions
- **Integration:** Feature works alongside existing features without conflict

### Regression Checks
Always execute at least two existing workflows to ensure no breakage:
- Existing critical flow A (defined for this project)
- Existing critical flow B (defined for this project)
- For Team Equipment Checkout Tracker: normal checkout, normal return, inventory view

### Non-Functional Checks
- **Error handling:** Do errors show helpful messages? Do failures crash or recover gracefully?
- **Logging:** Are errors logged? Can you trace a failure through the logs?
- **Performance:** Is the response time within acceptable range (not more than 3x slower than before)?

## Decision Output

Post a comment with this structure:

### JSON Decision
```json
{
  "contract": "QA",
  "decision": "PASS | FAIL",
  "qa_date": "YYYY-MM-DD",
  "tester": "[Your Name]",
  "environment": "[test environment description]",
  "scenarios_passed": [number],
  "scenarios_failed": [number],
  "regressions_found": "none | [list]",
  "blockers": "[if FAIL, list the specific scenarios that failed and why]",
  "recommendations": "[any suggestions for improvement or follow-up testing]"
}
```

### Human-Readable Summary
```markdown
## QA Decision

**Status:** PASS | FAIL

**Summary:** [One sentence: all scenarios passed and ready for release, or specific failure blocking release]

**Scenarios Tested:** [List of scenario names]

**Results:**
- [Scenario name]: ✅ PASS or ❌ FAIL [reason]
- [Scenario name]: ✅ PASS or ❌ FAIL [reason]

**Regressions:** [none, or list of impacted flows]

**Recommendation:** [Ready to merge, or needs build rework on: ...]
```

If FAIL, include:
- Specific scenario(s) that failed
- Root cause (if observable)
- Recommended next steps (send back to build, or QA sign-off after fix)

## When to APPROVE (PASS)
- All acceptance criteria scenarios work as specified
- No regressions in existing flows
- Edge cases handled gracefully
- System shows clear errors when constraints are violated
- Performance acceptable

## When to BLOCK (FAIL)
- Any scenario does not match acceptance criteria
- Regression in existing flows
- Error messages unclear or unhelpful
- Data validation missing
- Unrecoverable crashes or silent failures
- Performance significantly degraded

## Example: Team Equipment Checkout Tracker

For the feature "Prevent double checkout of the same item":

### Functional Scenarios
1. **Happy Path:** User checks out available item → succeeds, item marked unavailable
2. **Failure Path:** User tries to check out already-checked-out item → system shows error naming current holder
3. **Edge Case - Race Condition:** Two users try to checkout same item simultaneously → one succeeds, other sees "unavailable" error
4. **Edge Case - Boundary:** User tries to checkout when only one item exists → succeeds or fails correctly based on item state

### Regression Checks
1. **Existing Checkout Flow:** Normal checkout (without the new validation) still works
2. **Existing Return Flow:** Normal return still works and makes item available again
3. **Inventory View:** Inventory list shows correct availability status for all items

### Non-Functional Checks
- Error message for "already checked out" is clear and shows who holds the item
- System doesn't crash when attempting double-checkout
- No performance degradation in checkout flow
- Errors are logged for debugging

### Decision Framework Applied
- **PASS if:** All 4 scenarios pass, both regression flows work, error handling clear, no crashes, performance acceptable
- **FAIL if:** Any scenario fails, regression detected, error messages unclear, system crashes, performance degrades >3x

