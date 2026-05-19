# Low-Value Test Valuation Rubric

## Consensus Baseline

Use this consensus as the starting point:

- Valuable tests verify observable behavior, requirements, contracts, boundaries, and meaningful failure modes.
- Low-value tests often verify implementation details, trivial accessors, simple constructors, language/runtime guarantees, framework behavior, or duplicate coverage.
- Getters, setters, constructors, DTOs, and other trivial code can deserve tests when they enforce invariants, transform values, protect serialization/API compatibility, or document a regression.
- Tests that invoke use cases, handlers, command/query pipelines, or application services require an extra pass: the invoked behavior may contain validation or workflow value that is invisible from a DTO-shaped assertion.
- Coverage percentage is not a value metric by itself; a test that increases coverage while catching no plausible project bug can still be negative value.

Sources used while creating this rubric:

- Unit Test Frameworks, "4.5 (Not) Testing Get/Set Methods": https://flylib.com/books/en/1.104.1.30/1/
- Matthias Noback, "Don't test constructors": https://matthiasnoback.nl/2021/05/dont-test-constructors/
- Mark Seemann, "Test trivial code": https://blog.ploeh.dk/2013/03/08/test-trivial-code/
- Jacop Retorius, "Test Behavior, Not Implementation": https://jacopretorius.net/2012/01/test-behavior-not-implementation.html
- Diffblue documentation, testability codes for trivial getters/setters and constructors: https://docs.diffblue.com/features/output-codes/t-testability-codes
- Caltech CS130 lecture notes, maximize value by focusing unit tests on important and complex features: https://courses.cms.caltech.edu/cs130/lectures/CS130-Wi2026-Lec08.pdf

## Scoring

Score each reviewed test from evidence in the test and nearby production code.

Positive value:

- `behavior_value` 0-3: Observable requirement or public contract coverage.
- `defect_detection` 0-3: Plausible ability to catch a real project bug.
- `regression_value` 0-3: Guards a prior bug, edge case, compatibility rule, or fragile integration.

Cost/risk:

- `maintenance_cost` 0-3: Setup burden, churn sensitivity, fixture complexity, or developer confusion.
- `brittleness` 0-3: Fails when implementation changes but behavior remains correct.
- `duplication` 0-3: Same behavior already covered nearby or at a better level.
- `boundary_span` 0-3: Number of logical or runtime boundaries crossed for the asserted concern. Score 0 for a direct subject test, 1 for one collaborator boundary, 2 for several logical layers in-process, and 3 when runtime/hosted-service infrastructure is part of the behavior.

Compute:

```text
net_value = behavior_value + defect_detection + regression_value - maintenance_cost - brittleness - duplication - boundary_span
```

Classification:

- `low-value`: `net_value <= 0` and the test has strong low-value signals.
- `rewrite-candidate`: the concern is legitimate but current assertions target plumbing or implementation details.
- `rewrite-candidate` with `removal_action: unfold-test`: the current test spans multiple in-process parts and should be decomposed into direct tests of those parts, then each part should be re-scored for value and duplication.
- `rewrite-candidate` with `removal_action: move-to-integration-suite`: the test's value depends on runtime or hosted-service boundaries that a unit test cannot honestly exercise. Use this sparingly.
- `not-low-value`: `net_value > 0` or there is a clear keep signal.

## Use Case Boundary Pass

Run this pass before final classification when a test invokes a use case, handler, command/query pipeline, mediator, dispatcher, application service, or similarly named orchestration object.

1. Identify the asserted concern.
2. Read the invoked use case and the directly relevant collaborators.
3. List the independent parts that would exist if the test were unfolded.
4. Search for existing tests of those parts.
5. Re-score the unfolded parts with the normal rubric.

Outcomes:

- Keep the current test when it verifies observable use case behavior such as validation, authorization, branching, persistence intent, emitted events, or response semantics that is not already covered.
- Mark `unfold-test` when the concern is legitimate but the unit test uses too many in-process layers for what it asserts. Include the proposed parts and the direct subject each part should test.
- Mark `merge-with-existing-test` or `delete-test` when every valuable unfolded part is already covered by an equal or better test. Increase `duplication`, and name the duplicate tests when possible.
- Mark `move-to-integration-suite` only when the asserted value depends on real runtime or hosted-service boundaries, such as DI composition, hosted background services, message bus behavior, database provider behavior, middleware, transport serialization, or container/AppHost orchestration. If the test stays entirely in-process with mocks/fakes, prefer `unfold-test`.

Valuation guidance:

- Add behavior/defect value for real use case validation or branching even when the assertion is on a DTO or response object.
- Add brittleness and boundary span when a test reaches through a use case only to check DTO assignment, mapper output, or a collaborator call that can be tested directly.
- Add duplication when a direct use case test already covers the same input, decision, and observable outcome.

## Common Valuations

Constructor/property echo:

```json
{
  "behavior_value": 0,
  "defect_detection": 0,
  "regression_value": 0,
  "maintenance_cost": 1,
  "brittleness": 1,
  "duplication": 1,
  "boundary_span": 0,
  "net_value": -3
}
```

Framework/language guarantee:

```json
{
  "behavior_value": 0,
  "defect_detection": 0,
  "regression_value": 0,
  "maintenance_cost": 1,
  "brittleness": 0,
  "duplication": 1,
  "boundary_span": 0,
  "net_value": -2
}
```

Implementation-detail mock interaction:

```json
{
  "behavior_value": 1,
  "defect_detection": 1,
  "regression_value": 0,
  "maintenance_cost": 2,
  "brittleness": 3,
  "duplication": 0,
  "boundary_span": 0,
  "net_value": -3
}
```

Trivial-looking invariant test to keep:

```json
{
  "behavior_value": 3,
  "defect_detection": 2,
  "regression_value": 1,
  "maintenance_cost": 1,
  "brittleness": 0,
  "duplication": 0,
  "boundary_span": 0,
  "net_value": 5
}
```

Use case wrapper that should be unfolded:

```json
{
  "behavior_value": 1,
  "defect_detection": 1,
  "regression_value": 0,
  "maintenance_cost": 2,
  "brittleness": 2,
  "duplication": 1,
  "boundary_span": 2,
  "net_value": -5
}
```

Direct use case validation test to keep:

```json
{
  "behavior_value": 3,
  "defect_detection": 3,
  "regression_value": 0,
  "maintenance_cost": 1,
  "brittleness": 0,
  "duplication": 0,
  "boundary_span": 0,
  "net_value": 5
}
```
