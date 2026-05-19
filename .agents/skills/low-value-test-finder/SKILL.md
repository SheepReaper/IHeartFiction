---
name: low-value-test-finder
description: Find and report low-value, noisy, or removal-candidate automated tests in a repository. Use when an agent is asked to audit tests for weak defect-detection value, AI-generated coverage padding, trivial constructor/property/getter/setter tests, tests of language or framework guarantees, implementation-detail tests, assertion-free tests, duplicate tests, or tests that should be removed or rewritten; produce removal-ready findings with file, test name, location, evidence, valuation metadata, and justification.
---

# Low-Value Test Finder

## Overview

Use this skill to audit test suites for tests whose maintenance cost is higher than their defect-detection value. Prefer precise, evidence-backed findings over broad complaints about coverage culture.

The default stance is conservative: mark a test as low value only when the evidence is strong enough that another tool or engineer can remove or rewrite it without rediscovering the rationale.

This skill is agent-neutral. It does not require host-specific tools or APIs. All resource paths below are relative to this skill directory unless explicitly stated otherwise.

## Workflow

1. Inspect the repository's test framework, naming conventions, and recent test style.
2. Optionally run `scripts/scan_low_value_tests.py <repo-root>` to collect heuristic candidates.
3. Read each candidate test and the production code it exercises.
4. Score value with the rubric in `references/valuation-rubric.md`.
5. For tests that invoke a use case, handler, command/query pipeline, or application service, inspect the invoked production behavior before judging value. A use case can provide meaningful validation even when the test assertions appear to target DTOs or plumbing.
6. Emit findings only for tests with `classification: low-value` or `classification: rewrite-candidate`.
7. Include enough identifiers and metadata for another tool to remove, rewrite, unfold, move, or suppress the test.

Do not delete tests unless the user explicitly asks for removal. When asked to remove tests, remove only findings whose evidence is clear and keep behavior/risk tests intact.

## Finding Format

Return findings as JSON, YAML, or a concise table plus machine-readable metadata. Prefer JSON when the user mentions handoff, automation, or another tool.

Each finding must include:

```json
{
  "file": "tests/ExampleTests.cs",
  "line": 42,
  "end_line": 58,
  "test_name": "Constructor_SetsName",
  "framework": "xUnit",
  "classification": "low-value",
  "confidence": "high",
  "removal_action": "delete-test",
  "valuation": {
    "behavior_value": 0,
    "defect_detection": 0,
    "regression_value": 0,
    "maintenance_cost": 2,
    "brittleness": 1,
    "duplication": 0,
    "boundary_span": 0,
    "net_value": -3
  },
  "signals": [
    "constructor/property echo",
    "tests assignment guaranteed by simple object initialization",
    "no branch, boundary, invariant, validation, or observable workflow"
  ],
  "justification": "The test only constructs a DTO and asserts that constructor parameters are exposed unchanged. It verifies language-level assignment plumbing, not domain behavior.",
  "unfolding": {
    "recommended": false,
    "parts": [],
    "duplicate_of": []
  },
  "boundary_assessment": {
    "crosses_runtime_or_hosted_service_boundary": false,
    "suggested_suite": "unit"
  },
  "safe_rewrite": null
}
```

Use these `classification` values:

- `low-value`: delete candidate; the test mainly verifies trivial plumbing, language/framework guarantees, or duplicated coverage.
- `rewrite-candidate`: keep the concern but replace the test with a behavior, invariant, boundary, integration, or regression test.
- `not-low-value`: candidate was reviewed and should be kept; include only when the user asks for reviewed non-findings.

Use these `removal_action` values:

- `delete-test`
- `merge-with-existing-test`
- `rewrite-test`
- `unfold-test`
- `move-to-integration-suite`
- `keep`

## Low-Value Signals

Treat these as strong signals, especially when several appear together:

- Constructor, record, dataclass, DTO, POCO, POJO, or simple object initialization tests that only assert assigned values are returned.
- Getter/setter/property echo tests with no validation, transformation, invariant, side effect, serialization contract, binding contract, or compatibility concern.
- Tests that prove language, compiler, runtime, or framework guarantees such as list count after add, default constructor availability, dependency injection container basics, enum parsing without project-specific policy, or mock library behavior.
- Assertion-free tests, smoke tests that only verify "does not throw" without a documented compatibility or regression risk, or tests whose only assertion is non-null construction.
- Tests named after implementation details instead of externally observable behavior or requirements.
- Tests that over-mock internals and assert private collaboration rather than user-visible result, persisted state, emitted event, or public contract.
- Tests that exercise a DTO, mapper, request, response, or simple wrapper only by invoking a use case, handler, command/query pipeline, or application service, when the real subject should be the invoked use case and its collaborators.
- Duplicates that exercise the same input, assertion, and behavior as a nearby or higher-level test.
- Coverage-padding tests that do not contain a meaningful failure mode a maintainer would care about.

## Keep Signals

Do not mark a test low-value just because it is small. Keep it when it protects:

- A domain invariant, validation rule, permission rule, security boundary, money/date/time calculation, serialization format, public API contract, database mapping, query semantics, compatibility behavior, or previously reported regression.
- A simple-looking property with nontrivial logic such as normalization, lazy computation, side effects, notifications, validation, or persistence mapping.
- A constructor that enforces invariants, rejects invalid input, wires required dependencies for public behavior, or represents an externally consumed contract.
- A use case invocation that exercises meaningful validation, authorization, branching, persistence intent, emitted events, or observable workflow behavior not already covered by a direct use case test.
- A smoke test that guards a fragile integration point and has a clear historical or operational reason.

## Use Case Unfolding Heuristic

When a unit test invokes a use case, handler, command/query pipeline, or application service, do not classify it from the test body alone. Read the invoked code and decide whether the current test is valuable as written, should be unfolded into smaller tests, is a duplicate after unfolding, or belongs in an integration suite.

Use `unfold-test` when the test crosses too many logical boundaries but does not require runtime or hosted-service orchestration. The remediation is to deconstruct the scenario into tests for the individual parts, then re-score those proposed parts with the normal rubric:

- Direct use case validation or branching tests.
- DTO/request/response contract tests only when the contract itself has nontrivial behavior or compatibility risk.
- Mapper, policy, domain service, repository abstraction, or collaborator tests when those parts contain independently meaningful logic.

Use `merge-with-existing-test` or `delete-test` when the unfolded parts would duplicate existing tests at the same or better level. Name the duplicate test when possible in `unfolding.duplicate_of`.

Use `move-to-integration-suite` only when the test's value depends on crossing runtime or hosted-service boundaries, such as real DI composition, hosted services, message bus behavior, database provider behavior, authentication middleware, serialization over transport, or AppHost/container orchestration. This should be rare. If the scenario does not cross those boundaries, prefer unfolding within the unit test suite instead of moving it.

## Scanner

Run the scanner as a first pass:

```bash
python3 scripts/scan_low_value_tests.py <repo-root> --format json
```

On Windows, `py -3 scripts/scan_low_value_tests.py <repo-root> --format json` is also acceptable. Agents that cannot execute scripts should skip this step and apply the rubric manually.

The scanner emits heuristic candidates only. Always review candidate tests manually before calling them findings.

Useful options:

- `--include-reviewed`: include lower-confidence candidates classified as `review`; `low-value` and `rewrite-candidate` findings are included by default.
- `--format markdown`: emit a readable table for quick triage.
- `--max-files 2000`: cap scanned test files in very large repos.
- `--scan-all`: scan the whole repository instead of conventional test roots.

## Output Guidance

Lead with removal-ready findings ordered by confidence and net value. Be explicit about uncertainty:

- `high`: test body and production code clearly show trivial plumbing or duplicated coverage.
- `medium`: test is suspicious but needs maintainer judgment.
- `low`: mention only if the user asked for broad triage.

When handing off to another tool, include exact file path, line range, test name, classification, removal action, and justification. Avoid asking the next tool to infer why the test is low value.
