# Spline Race Camera Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep Cinemachine directly behind the preferred horse through Sprint turns with stable framing, while making each camera rig independent for future side-by-side competition.

**Architecture:** Add a pure yaw-lag model plus one serialized `RaceCameraTarget` per racer. The target copies the horse anchor position, follows horse yaw with an eight-degree maximum lag, and updates between `RaceSplineTapDriver` and `CinemachineBrain`; Cinemachine owns only framing, damping, and collision.

**Tech Stack:** Unity 6000.5.9f1, C#/.NET Standard 2.1, Cinemachine 3.1.6, Malbers Animal Controller, Unity Test Framework 1.7.0, Unity MCP.

---

## File map

- Create `Assets/Scripts/Race/RaceCameraHeadingModel.cs`: deterministic yaw smoothing, lag cap, and discontinuity snap.
- Create `Assets/Scripts/Race/RaceCameraTarget.cs`: per-racer pivot lifecycle and Cinemachine target ownership.
- Modify `Assets/Scripts/Race/HorseRacing.Race.asmdef`: reference `Unity.Cinemachine`.
- Create `Assets/Tests/EditMode/RaceCameraHeadingModelTests.cs`: pure heading behavior tests.
- Modify `Assets/Tests/PlayMode/EventCameraPlayModeTests.cs`: scene wiring, framing, and stability assertions.
- Modify `Assets/Scenes/Main.unity` through Unity MCP: create player-one target, wire both event cameras, disable Malbers camera ownership, and set presentation values.

Existing unstaged horse/stamina changes in `RaceSplineTapDriver.cs`, `Main.unity`, and `EditorSettings.asset` are user-owned. Preserve them. Stage only camera-specific scene hunks when committing.

### Task 1: Heading model with bounded lag

**Files:**
- Create: `Assets/Scripts/Race/RaceCameraHeadingModel.cs`
- Create: `Assets/Tests/EditMode/RaceCameraHeadingModelTests.cs`

- [ ] **Step 1: Write failing EditMode tests**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace HorseRacing.Race.Tests
{
    public sealed class RaceCameraHeadingModelTests
    {
        [Test]
        public void StepYaw_NeverExceedsConfiguredLag()
        {
            var velocity = 0f;
            var result = RaceCameraHeadingModel.StepYaw(
                0f, 30f, ref velocity, 1f / 60f, 0.08f, 8f, 45f);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(result, 30f)),
                Is.LessThanOrEqualTo(8.001f));
        }

        [Test]
        public void StepYaw_SnapsAcrossLargeDiscontinuity()
        {
            var velocity = 90f;
            var result = RaceCameraHeadingModel.StepYaw(
                0f, 100f, ref velocity, 1f / 60f, 0.08f, 8f, 45f);

            Assert.That(Mathf.DeltaAngle(result, 100f), Is.Zero.Within(0.001f));
            Assert.That(velocity, Is.Zero);
        }

        [Test]
        public void StepYaw_ForceSnapPlacesCameraBehindImmediately()
        {
            var velocity = 20f;
            var result = RaceCameraHeadingModel.StepYaw(
                270f, 15f, ref velocity, 0f, 0.08f, 8f, 45f, true);

            Assert.That(Mathf.DeltaAngle(result, 15f), Is.Zero.Within(0.001f));
            Assert.That(velocity, Is.Zero);
        }
    }
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Use Unity MCP `run_tests` with mode `EditMode` and test name `HorseRacing.Race.Tests.RaceCameraHeadingModelTests`.

Expected: compilation/test failure because `RaceCameraHeadingModel` does not exist.

- [ ] **Step 3: Add minimal heading model**

```csharp
using UnityEngine;

namespace HorseRacing.Race
{
    public static class RaceCameraHeadingModel
    {
        public static float StepYaw(float currentYaw, float targetYaw,
            ref float yawVelocity, float deltaTime, float smoothTime,
            float maxLagDegrees, float snapAngleDegrees, bool forceSnap = false)
        {
            smoothTime = Mathf.Max(0.0001f, smoothTime);
            maxLagDegrees = Mathf.Max(0f, maxLagDegrees);
            snapAngleDegrees = Mathf.Max(maxLagDegrees, snapAngleDegrees);
            var error = Mathf.DeltaAngle(currentYaw, targetYaw);

            if (forceSnap || deltaTime <= 0f || Mathf.Abs(error) >= snapAngleDegrees)
            {
                yawVelocity = 0f;
                return Normalize(targetYaw);
            }

            var smoothed = Mathf.SmoothDampAngle(currentYaw, targetYaw,
                ref yawVelocity, smoothTime, Mathf.Infinity, deltaTime);
            var lag = Mathf.DeltaAngle(targetYaw, smoothed);
            smoothed = targetYaw + Mathf.Clamp(lag, -maxLagDegrees, maxLagDegrees);
            return Normalize(smoothed);
        }

        static float Normalize(float angle) => Mathf.Repeat(angle, 360f);
    }
}
```

- [ ] **Step 4: Run focused tests and verify GREEN**

Expected: 3 passed, 0 failed.

- [ ] **Step 5: Commit model and tests**

```powershell
git add -- Assets/Scripts/Race/RaceCameraHeadingModel.cs Assets/Scripts/Race/RaceCameraHeadingModel.cs.meta Assets/Tests/EditMode/RaceCameraHeadingModelTests.cs Assets/Tests/EditMode/RaceCameraHeadingModelTests.cs.meta
git commit -m "test: define stable race camera heading"
```

### Task 2: Per-racer Cinemachine target

**Files:**
- Create: `Assets/Scripts/Race/RaceCameraTarget.cs`
- Modify: `Assets/Scripts/Race/HorseRacing.Race.asmdef`
- Modify: `Assets/Tests/PlayMode/EventCameraPlayModeTests.cs`

- [ ] **Step 1: Add failing PlayMode component assertions**

Add imports and assertions inside `MainScene_UsesCenteredLockedBehindMountCamera`:

```csharp
var raceTarget = Object.FindFirstObjectByType<RaceCameraTarget>();
Assert.That(raceTarget, Is.Not.Null);
Assert.That(raceTarget.PositionAnchor, Is.Not.Null);
Assert.That(raceTarget.HeadingSource, Is.Not.Null);
Assert.That(raceTarget.MaxYawLagDegrees, Is.EqualTo(8f).Within(0.001f));

var cameras = Object.FindObjectsByType<CinemachineCamera>(
    FindObjectsInactive.Include, FindObjectsSortMode.None)
    .Where(value => value.name == "CM Third Person Mount" ||
                    value.name == "CM Third Person Main")
    .ToArray();
Assert.That(cameras, Has.Length.EqualTo(2));
Assert.That(cameras.All(value =>
    value.Target.TrackingTarget == raceTarget.transform), Is.True);
```

- [ ] **Step 2: Run focused PlayMode test and verify RED**

Expected: compilation failure because `RaceCameraTarget` does not exist.

- [ ] **Step 3: Add Cinemachine assembly reference**

Add `"Unity.Cinemachine"` to `Assets/Scripts/Race/HorseRacing.Race.asmdef` references.

- [ ] **Step 4: Implement `RaceCameraTarget`**

```csharp
using Unity.Cinemachine;
using UnityEngine;

namespace HorseRacing.Race
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class RaceCameraTarget : MonoBehaviour
    {
        [Header("Per-player references")]
        [SerializeField] Transform positionAnchor;
        [SerializeField] Transform headingSource;
        [SerializeField] CinemachineCamera[] controlledCameras;

        [Header("Turn presentation")]
        [SerializeField, Min(0.001f)] float yawSmoothTime = 0.08f;
        [SerializeField, Range(0f, 20f)] float maxYawLagDegrees = 8f;
        [SerializeField, Range(10f, 180f)] float snapAngleDegrees = 45f;

        Transform[] _originalTargets;
        float _yaw;
        float _yawVelocity;
        bool _initialized;
        bool _ownsTargets;

        public Transform PositionAnchor => positionAnchor;
        public Transform HeadingSource => headingSource;
        public float MaxYawLagDegrees => maxYawLagDegrees;
        public float YawErrorDegrees => headingSource
            ? Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y,
                headingSource.eulerAngles.y))
            : 0f;

        void OnValidate()
        {
            yawSmoothTime = Mathf.Max(0.001f, yawSmoothTime);
            maxYawLagDegrees = Mathf.Clamp(maxYawLagDegrees, 0f, 20f);
            snapAngleDegrees = Mathf.Clamp(snapAngleDegrees,
                Mathf.Max(10f, maxYawLagDegrees), 180f);
        }

        void OnEnable()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _originalTargets = new Transform[controlledCameras.Length];
            SnapBehindSubject();
            for (var i = 0; i < controlledCameras.Length; i++)
            {
                _originalTargets[i] = controlledCameras[i].Target.TrackingTarget;
                controlledCameras[i].Target.TrackingTarget = transform;
            }
            _ownsTargets = true;
        }

        void LateUpdate() => ApplyPose(Time.unscaledDeltaTime, false);

        public void SnapBehindSubject() => ApplyPose(0f, true);

        void ApplyPose(float deltaTime, bool forceSnap)
        {
            if (!positionAnchor || !headingSource) return;
            var targetYaw = headingSource.eulerAngles.y;
            _yaw = RaceCameraHeadingModel.StepYaw(_yaw, targetYaw,
                ref _yawVelocity, deltaTime, yawSmoothTime,
                maxYawLagDegrees, snapAngleDegrees,
                forceSnap || !_initialized);
            transform.SetPositionAndRotation(positionAnchor.position,
                Quaternion.Euler(0f, _yaw, 0f));
            _initialized = true;
        }

        bool ValidateConfiguration()
        {
            if (!positionAnchor || !headingSource || controlledCameras == null ||
                controlledCameras.Length == 0)
            {
                Debug.LogError($"{nameof(RaceCameraTarget)} on {name} is missing per-player references.", this);
                return false;
            }

            for (var i = 0; i < controlledCameras.Length; i++)
            {
                if (controlledCameras[i]) continue;
                Debug.LogError($"{nameof(RaceCameraTarget)} on {name} has an empty camera reference.", this);
                return false;
            }
            return true;
        }

        void OnDisable()
        {
            if (!_ownsTargets || _originalTargets == null) return;
            for (var i = 0; i < controlledCameras.Length; i++)
            {
                if (controlledCameras[i] &&
                    controlledCameras[i].Target.TrackingTarget == transform)
                    controlledCameras[i].Target.TrackingTarget = _originalTargets[i];
            }
            _ownsTargets = false;
            _initialized = false;
            _yawVelocity = 0f;
        }
    }
}
```

- [ ] **Step 5: Validate script and compilation**

Use Unity MCP `validate_script` on both new scripts. Read `mcpforunity://editor/state` until compilation finishes, then read Console errors.

Expected: 0 validation errors and 0 C# compiler errors.

### Task 3: Wire polished player-one camera

**Files:**
- Modify: `Assets/Scenes/Main.unity`
- Modify: `Assets/Tests/PlayMode/EventCameraPlayModeTests.cs`

- [ ] **Step 1: Extend failing presentation assertions**

```csharp
Assert.That(mount.Damping.x, Is.EqualTo(0.08f).Within(0.001f));
Assert.That(mount.Damping.y, Is.EqualTo(0.14f).Within(0.001f));
Assert.That(mount.Damping.z, Is.EqualTo(0.08f).Within(0.001f));
Assert.That(mount.ShoulderOffset.x, Is.Zero.Within(0.001f));
Assert.That(mount.ShoulderOffset.y, Is.EqualTo(0.25f).Within(0.001f));
Assert.That(mount.VerticalArmLength, Is.EqualTo(1f).Within(0.001f));
Assert.That(mount.CameraDistance, Is.EqualTo(7.25f).Within(0.001f));

var mountCamera = cameras.Single(value => value.name == "CM Third Person Mount");
Assert.That(mountCamera.Lens.FieldOfView, Is.EqualTo(45f).Within(0.001f));

Assert.That(targets.All(value => !value.enabled), Is.True);
Assert.That(lookLinks.All(value => !value.enabled), Is.True);

var noises = Object.FindObjectsByType<CinemachineBasicMultiChannelPerlin>(
    FindObjectsInactive.Include, FindObjectsSortMode.None);
Assert.That(noises.All(value => value.AmplitudeGain == 0f), Is.True);

var impulses = Object.FindObjectsByType<CinemachineExternalImpulseListener>(
    FindObjectsInactive.Include, FindObjectsSortMode.None);
Assert.That(impulses.All(value => !value.enabled || value.Gain == 0f), Is.True);
```

- [ ] **Step 2: Run focused PlayMode test and verify RED**

Expected: failure on missing target or old framing values.

- [ ] **Step 3: Configure scene through Unity MCP**

Stop Play Mode first. Create root GameObject `Race Camera Target P1` with `RaceCameraTarget`.

Assign:

- `positionAnchor`: preferred horse child `CameraTarget Horse`;
- `headingSource`: `RaceSetup/Horse Realistic` root;
- `controlledCameras[0]`: `CM Third Person Mount`;
- `controlledCameras[1]`: `CM Third Person Main`;
- `yawSmoothTime`: `0.08`;
- `maxYawLagDegrees`: `8`;
- `snapAngleDegrees`: `45`.

On both Cinemachine cameras set:

```text
TrackingTarget = Race Camera Target P1
Damping = (0.08, 0.14, 0.08)
ShoulderOffset = (0, 0.25, 0)
VerticalArmLength = 1.0
CameraSide = 0.5
CameraDistance = 7.25
FieldOfView = 45
AvoidObstacles.Enabled = true
AvoidObstacles.DampingIntoCollision = 0.10
AvoidObstacles.DampingFromCollision = 0.40
CinemachineBasicMultiChannelPerlin.AmplitudeGain = 0
```

Disable both Malbers `ThirdPersonFollowTarget` and `MInputLinkLook` components. Set `CM Brain/CinemachineExternalImpulseListener.Gain = 0`. Keep `CinemachineBrain.UpdateMethod = LateUpdate`. Save `Main`.

- [ ] **Step 4: Run focused PlayMode test and verify GREEN**

Expected: 1 passed, 0 failed.

- [ ] **Step 5: Run all camera and race tests**

Run full assemblies:

```text
HorseRacing.Race.EditModeTests: all pass
HorseRacing.Race.PlayModeTests: all pass
```

### Task 4: Live turn verification and delivery

**Files:**
- No new production files expected.
- Update tests only if live evidence reveals a missing invariant.

- [ ] **Step 1: Find highest-curvature spline section**

Enter Play Mode. Use Unity MCP `execute_code` to sample spline tangents across normalized positions and return the largest heading change. Reposition the runtime driver just before that normalized position for a diagnostic run; do not save the runtime position.

- [ ] **Step 2: Drive sustained Sprint through the turn**

Inject repeated `RegisterTap()` bursts until telemetry reports gait 5 and `9.25 m/s`. Sample `RaceCameraTarget.YawErrorDegrees` through the bend.

Expected: yaw error never exceeds `8.001 degrees`; camera remains behind horse.

- [ ] **Step 3: Capture and inspect moving turn frame**

Capture Game View at the highest-curvature bend. Verify horse remains horizontally centered, full jockey/horse readable, horizon level, and no side-on background smear. Remove generated screenshot assets after inspection.

- [ ] **Step 4: Final verification**

Stop Play Mode. Validate scripts, read Console for compiler errors, rerun full EditMode and PlayMode assemblies, run `git diff --check`, and review every camera-specific scene hunk.

- [ ] **Step 5: Commit only camera work**

Stage new scripts, metadata, assembly/test changes, and only camera-specific `Main.unity` hunks. Do not stage pre-existing horse/stamina or EditorSettings changes.

```powershell
git commit -m "fix: keep race camera behind horse"
```

- [ ] **Step 6: Push and verify remote**

```powershell
git push origin main
git status --short --branch
git rev-parse HEAD
git rev-parse origin/main
```

Expected: push succeeds and local `HEAD` equals `origin/main`; user-owned unstaged changes remain preserved.
