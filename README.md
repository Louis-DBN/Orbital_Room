# Orbital Room

Orbital Room is a VR training experience for future space-station technicians. Trainees diagnose and repair critical failures—such as electrical faults, leaks, and antenna misalignment—before an alarm escalates.

The project targets Meta Quest 3 and emphasizes robust spatial interaction and human-computer interaction over visual asset quality. The current initial physical-area target is 2 m × 1 m, but dimensions may change. The intended experience includes movement between workstations, voice-summoned tools or schematics, a deliberate calibration gesture, spatial audio alerts, and a score that explains precision, time, and protocol adherence.

## Project status

This repository is currently a Unity starter project, not a completed or configured Quest experience. It uses Unity `6000.3.23f1`, Universal Render Pipeline, and the Input System. The current build scene is `Assets/Scenes/SampleScene.unity`. The package manifest does not include OpenXR or XR Interaction Toolkit, so the existing project settings do not by themselves confirm Quest build or headset readiness.

## Getting started

1. Open the project in Unity Hub using Unity `6000.3.23f1`.
2. Review the current scene and package setup before implementing headset-dependent behavior.
3. Use the architecture and feature workflow below to agree on a small, testable feature slice with the group.
4. Validate changes in the Editor and on Meta Quest 3 when the behavior depends on headset hardware. Report any validation that could not be performed.

## Working with AI agents

- [AGENTS.md](AGENTS.md) — shared agent instructions and project conventions.
- [Architecture](docs/architecture.md) — runtime boundaries, dependency direction, and testing principles.
- [Feature workflow](docs/feature-workflow.md) — small vertical slices, acceptance criteria, and pull-request review.

Keep implementation and documentation focused on one user-visible outcome at a time. The team should review each slice before layering on the next capability.
