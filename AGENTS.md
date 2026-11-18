# Agent guide

## Project

Orbital Room is a Unity training experience for Meta Quest 3 technicians. Its focus is robust, understandable spatial interaction in a physical area currently specified as 2 m × 1 m, not high-fidelity modeling.

Read [README.md](README.md), [docs/architecture.md](docs/architecture.md), and [docs/feature-workflow.md](docs/feature-workflow.md) before making project changes. Treat these documents as shared team conventions.

## Product constraints

- Preserve the training scenario: diagnose and repair critical orbital-station failures before an alarm.
- Use the currently specified 2 m × 1 m area as the initial design target; its dimensions may change. Confirm the current target before starting a feature, and avoid hard-coding play-area bounds. Require intentional movement between workstations while keeping interactions safe, reachable, and understandable.
- Plan for voice commands to summon tools or schematics, a deliberate hand gesture for calibration, spatialized audio to direct attention, and feedback that explains score and protocol outcomes.
- Prioritize interaction robustness and HCI relevance over visual polish.
- Do not claim that a capability exists or that the project is Quest-ready unless it is implemented and validated. The repository currently contains a Unity starter scene and does not include OpenXR or XR Interaction Toolkit packages.

## Implementation conventions

- Deliver one small, user-visible vertical slice at a time. Avoid unrelated refactors and speculative frameworks.
- Follow [docs/architecture.md](docs/architecture.md): keep training rules independent of Unity scene objects and hardware SDKs; adapt hardware and Unity APIs at the edges.
- Prefer explicit, cohesive types and clear ownership. Avoid global mutable state, hidden scene lookups, and abstractions with only one trivial use.
- Keep runtime state separate from authoring configuration. Do not mutate shared ScriptableObject assets to store per-session state.
- Keep user-facing text and code comments in English. Add comments only when they clarify non-obvious intent or constraints; do not narrate straightforward code.
- Follow existing Unity conventions, including matching `.meta` files for added or moved assets. Commit Unity-generated metadata alongside its asset.
- Do not change project settings, package versions, input bindings, or generated assets unless the feature requires it. Explain intentional changes in the pull request.
- Keep scenes and prefabs focused. Prefer reusable prefabs for shared interaction patterns and avoid coupling a feature to unrelated scene objects.

## Validation and review

- For each feature, provide a reproducible manual test plan with setup, user actions, expected results, and failure or recovery cases. State whether it should be run in the Unity Editor, on Quest, or both.
- Automated tests are optional. Add them when they provide useful repeatable coverage; do not make them a prerequisite for a feature or a substitute for user-run VR validation.
- When Unity Editor or Quest hardware validation is required but unavailable, state that explicitly; do not present static inspection as runtime validation.
- For interaction changes, the manual plan should cover the intended input, cancellation/recovery paths, and behavior inside the current target play area. For scoring changes, check both the score and its explanatory feedback.
- Keep a pull request focused on one feature outcome. Include acceptance criteria, the manual test plan and observed results, known limitations, and relevant screenshots or a short capture when useful.
- Update the relevant documentation when behavior, architecture, setup, or team workflow changes.

