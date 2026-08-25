# Tripo Jockey Malbers Rider Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the cowboy visual on the Main scene's Malbers Rider with the Tripo humanoid jockey while preserving mounted animation and IK.

**Architecture:** Keep the existing Malbers Rider root Animator as the gameplay authority. Add the Tripo FBX beneath it with its native humanoid Animator, mirror parameters/layers/states through `RiderAnimatorSynchronizer`, disable the old visible hierarchy, and bridge Malbers rein/foot IK to the Tripo humanoid bones.

**Tech Stack:** Unity 6000.5, C#, NUnit EditMode tests, Malbers Horse AnimSet Pro, Unity Humanoid animation, Unity MCP.

---

### Task 1: Add rider scene contract test

**Files:**
- Create: `Assets/Tests/EditMode/TripoJockeyRiderSceneTests.cs`
- Modify: `Assets/Tests/EditMode/HorseRacing.Race.EditModeTests.asmdef`

- [x] Write an EditMode test that opens `Assets/Scenes/Main.unity`, finds `RaceSetup/Rider`, and checks the root and visual Animator contract.
- [x] Assert `JockeyVisual` exists with an enabled `SkinnedMeshRenderer`, Tripo Avatar, and synchronizer target.
- [x] Assert the cowboy renderer is disabled.
- [x] Assert `MRider.LeftHand` and `MRider.RightHand` equal the visual Animator's humanoid hand transforms.
- [x] Run the focused EditMode test and confirm the missing-jockey contract fails before implementation.

### Task 2: Perform the scene-local visual swap

**Files:**
- Modify: `Assets/Scenes/Main.unity`
- Create: `Assets/Scripts/Race/RiderAnimatorSynchronizer.cs`

- [x] Instantiate `Assets/TripoModels/jockey_3d_model/jockey_3d_model.fbx` beneath `RaceSetup/Rider` as `JockeyVisual`.
- [x] Keep the native Tripo Animator and configure it with the Malbers controller without root motion.
- [x] Add a synchronizer that mirrors non-curve parameters, layer weights, active states, transitions, and mounted IK.
- [x] Disable the old cowboy visual/skeleton at the scene-instance level.
- [x] Assign `MRider.LeftHand` and `MRider.RightHand` from the Tripo visual Animator's humanoid mapping.
- [x] Save `Assets/Scenes/Main.unity` and wait for Unity serialization/import to finish.

### Task 3: Fit and verify the mounted rider

**Files:**
- Modify: `Assets/Scenes/Main.unity`

- [x] Enter Play Mode and ensure the configured start-mounted flow completes.
- [x] Set/observe locomotion through mounted idle and max-effort sprint/canter states.
- [x] Adjust only `JockeyVisual` uniform scale, local root position, and 180-degree facing correction until seated and aligned.
- [x] Confirm both hands follow the Malbers rein IK targets and the old cowboy is not rendered.
- [x] Save the final fit back to the scene.

### Task 4: Verification

**Files:**
- Test: `Assets/Tests/EditMode/TripoJockeyRiderSceneTests.cs`
- Test: `Assets/Tests/PlayMode/RaceSplineTapDriverPlayModeTests.cs`

- [x] Run the focused EditMode test and confirm it passes.
- [x] Run the complete PlayMode suite and confirm no regression in mounted race behavior.
- [x] Check the Unity Console for new Errors and Exceptions.
- [x] Inspect the rider scene diff and remove unintended Avatar, material, and hand-transform overrides.
