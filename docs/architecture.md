# Architecture

## Goals

The architecture should make interaction behavior testable, keep hardware details replaceable, and let a small group review one training capability at a time. It should remain proportionate to the project: add boundaries when a feature needs them, not empty layers or frameworks in advance.

## Runtime boundaries

Use these responsibilities as dependency guidelines, not as a requirement to create a separate assembly for every layer:

1. **Domain — training rules.** Own incident/task state, protocol steps, completion rules, and score calculation. Prefer plain C# types with no dependency on `UnityEngine`, scene objects, or headset SDKs.
2. **Application — use cases.** Coordinate a training session: start an incident, accept a diagnosis or repair attempt, advance protocol, and publish outcomes. Depend on domain rules and small interfaces for external capabilities; do not read input devices directly.
3. **Adapters — Unity and device APIs.** Translate hand/controller/voice input into application requests, and connect application outcomes to spatial UI, audio, haptics, or other Unity services. Keep Meta/XR SDK calls here.
4. **Presentation and authoring — scenes and assets.** Use scenes, prefabs, and ScriptableObjects to compose and tune the experience. Treat ScriptableObjects as shared configuration; keep mutable per-run state in runtime objects.

The intended direction is **adapters and presentation → application → domain**. Domain rules must not depend on Unity, XR, audio, UI, or vendor SDKs. Keep dependencies explicit instead of finding systems through global singletons or string-based scene lookups.

## Feature ownership

Organize work around capabilities a user can experience and test. Likely capabilities include incident diagnosis, workstation interaction, gesture calibration, voice summons, spatial alerts, and training results. A feature may touch its input adapter, application behavior, presentation, and tests; keep those changes together when they form one user outcome.

Do not implement all these capabilities as part of the documentation setup. For each capability, first agree on its behavior and acceptance criteria, then introduce only the code and asset boundaries needed for that slice.

When code is added, a practical starting point is:

```text
Assets/
  Scenes/
  Features/
    <FeatureName>/
      Runtime/
      Tests/
  Shared/
```

Keep a feature's code and related assets together where Unity workflows allow it. Split `Runtime` further into domain, application, and adapters only when the feature's complexity benefits from that distinction. Create assembly definitions only when there is a concrete compile-time or test-isolation benefit. Do not add empty folders or assemblies just to mirror this example.

## Interaction and safety principles

- Treat each input method as an adapter to the same well-defined action when appropriate. Keep gesture recognition and voice recognition replaceable, and provide clear feedback for accepted, rejected, or cancelled actions.
- Make critical actions deliberate and recoverable. Define what happens when input is ambiguous, interrupted, unavailable, or attempted out of sequence.
- Confirm the current physical-area target for each feature. The initial target is 2 m × 1 m, but dimensions may change; keep bounds configurable rather than embedding them in interaction logic. Keep controls reachable and do not require unsafe leaning, blind steps, or movement beyond the configured boundary.
- Use spatial audio as a directional cue and pair critical alerts with clear in-world visual feedback so the user can locate and understand them.
- Make scoring traceable to observable actions such as precision, elapsed time, and protocol adherence. Present what affected the result and how to improve, rather than only showing a number.

## Verification strategy

- Give each feature a reproducible user-run test plan: setup, actions, expected results, and failure or recovery cases.
- Verify scene behavior in the Unity Editor and headset-dependent behavior on Quest. Record which environment and hardware were actually used; Editor checks alone do not establish that an interaction works in VR.
- Automated tests are optional and should be added when they make important behavior easier to verify repeatedly. They are not required for every feature and do not replace user-run validation.
- Report the outcomes observed and any validation that remains outstanding.

