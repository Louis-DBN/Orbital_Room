# Feature workflow

## Work in reviewable slices

Deliver one small, user-observable behavior per issue and pull request. A slice may cross code, scenes, prefabs, and tests if all are needed for that behavior, but it should not bundle unrelated features or broad cleanups.

Before implementation, agree on:

- **User outcome:** what the trainee can do or understand after this slice.
- **Scope:** what is explicitly included and excluded.
- **Acceptance criteria:** observable pass/fail behavior, including the current configured play-area dimensions where relevant (initial target: 2 m × 1 m).
- **Failure and recovery:** expected behavior for incorrect, ambiguous, interrupted, unavailable, or out-of-order input.
- **Manual test plan:** setup, user actions, expected results, failure/recovery cases, and where to run it (Editor, Quest, or both). Automated tests are optional.
- **Ownership:** one person coordinates the slice and keeps its pull request focused; teammates can review or contribute without overlapping edits to the same scene or prefab.

For example, “summon a diagnostic schematic by voice” should define the supported command, the response to an unrecognized command, how the user dismisses it, and how success will be verified. Treat this as an example of testable scope, not a feature already implemented.

## Implementation loop

1. Inspect the current project and relevant guidance; confirm the slice does not duplicate existing behavior.
2. Agree on acceptance criteria before implementation. Split the work further if criteria describe multiple independent user outcomes.
3. Implement the smallest end-to-end path, including feedback and recovery behavior. Add automated tests only when they provide useful repeatable coverage.
4. Follow the manual test plan in the Unity Editor and check changed scenes, prefabs, input bindings, and `.meta` files.
5. Test on Meta Quest 3 when the change depends on headset behavior. If hardware is unavailable, state the remaining validation clearly.
6. Open a focused pull request with a concise user outcome, acceptance criteria, the manual test plan, observed results, limitations, and captures when useful.
7. Address review feedback, then merge according to the group's normal repository permissions and process.

No branch-name convention is required by this workflow. Follow any repository or course convention that the group later adopts.

## Manual test plan format

Include a short plan with each feature so another teammate can verify it without guessing the setup:

```text
Environment: Unity Editor / Meta Quest 3 (include build or project setup)
Setup: Scene, starting state, and any required tracking/input configuration
1. User action: ...
   Expected result: ...
2. User action: ...
   Expected result: ...
Failure or recovery check: ...
Pass condition: ...
```

Keep the steps specific to the feature. Include an error, cancellation, or recovery path when the feature has one. After running the plan, record the actual result and any steps that could not be completed.

## Pull-request checklist

- [ ] One clear user outcome; unrelated changes are excluded.
- [ ] Acceptance criteria and relevant recovery/error behavior are addressed.
- [ ] Interaction remains safe and usable in the target play area.
- [ ] A teammate can follow the manual test plan, including setup, actions, expected results, and failure/recovery cases.
- [ ] Actual validation results and any unavailable device checks are disclosed; automated tests are optional.
- [ ] Unity assets and their `.meta` files are included together.
- [ ] Relevant documentation is updated.
- [ ] Reviewers can understand the behavior and test it without relying on undocumented setup.

