# Horse Spline Race Movement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Make RaceSetup/Horse Realistic respond only to configurable keyboard taps, travel along RaceTrackSpline at native animation root-motion speed, stop reliably, and use a stable race camera.

**Architecture:** RaceSplineTapDriver remains the Unity/Malbers adapter and sole world-pose owner. Three small tested classes own tap effort, configurable key filtering, and one-shot root-motion distance consumption. Malbers selects and evaluates gait animations but cannot move or rotate the GameObject.

**Tech Stack:** Unity 6000.5.9f1, C#/.NET Standard 2.1, Input System 1.20.0, Splines 2.9.0, Cinemachine 3.1.6, Malbers Animal Controller, Unity Test Framework 1.7.0, Unity MCP.

---

## File map

- Create Assets/Scripts/Race/HorseRacing.Race.asmdef: testable runtime assembly.
- Create Assets/Scripts/Race/TapEffortModel.cs: bounded tap rate, smoothing, idle snap, gait hysteresis.
- Create Assets/Scripts/Race/RootMotionDistanceAccumulator.cs: consume Animator distance once.
- Create Assets/Scripts/Race/ConfigurableKeyboardTapInput.cs: deduplicated keyboard bindings.
- Modify Assets/Scripts/Race/RaceSplineTapDriver.cs: Unity/Malbers/spline integration.
- Create Assets/Tests/EditMode/HorseRacing.Race.EditModeTests.asmdef and three model test files.
- Create Assets/Tests/PlayMode/HorseRacing.Race.PlayModeTests.asmdef and two input/integration test files.
- Modify Assets/Scenes/Main.unity through Unity MCP: effort timing and camera noise.

RaceSplineTapDriver.cs and Main.unity were dirty before this plan. Never reset either file. Main.unity stays unstaged because it contains unrelated user scene work. Separate worktree use is intentionally skipped because required baseline changes exist only in this worktree.

### Task 1: Establish testable assemblies

**Files:**
- Create: Assets/Scripts/Race/HorseRacing.Race.asmdef
- Create: Assets/Tests/EditMode/HorseRacing.Race.EditModeTests.asmdef
- Create: Assets/Tests/PlayMode/HorseRacing.Race.PlayModeTests.asmdef

- [ ] **Step 1: Create runtime asmdef**

~~~json
{
  "name": "HorseRacing.Race",
  "rootNamespace": "HorseRacing.Race",
  "references": [
    "MalbersAnimations",
    "MalbersAnimations.InputSystem",
    "Unity.InputSystem",
    "Unity.Mathematics",
    "Unity.Splines"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
~~~

- [ ] **Step 2: Create EditMode test asmdef**

~~~json
{
  "name": "HorseRacing.Race.EditModeTests",
  "rootNamespace": "HorseRacing.Race.Tests",
  "references": ["HorseRacing.Race", "Unity.InputSystem"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false,
  "optionalUnityReferences": ["TestAssemblies"]
}
~~~

- [ ] **Step 3: Create PlayMode test asmdef**

~~~json
{
  "name": "HorseRacing.Race.PlayModeTests",
  "rootNamespace": "HorseRacing.Race.Tests",
  "references": [
    "HorseRacing.Race",
    "Unity.InputSystem",
    "Unity.InputSystem.TestFramework"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false,
  "optionalUnityReferences": ["TestAssemblies"]
}
~~~

- [ ] **Step 4: Refresh Unity and verify compilation**

Wait until mcpforunity://editor/state reports is_compiling=false. Read 50 Console errors with stack traces.

Expected: zero C# compilation errors. Confirm RaceSplineTapDriver remains attached to RaceSetup/Horse Realistic after assembly migration.

- [ ] **Step 5: Commit scaffolding**

~~~powershell
git add -- Assets/Scripts/Race/HorseRacing.Race.asmdef Assets/Tests/EditMode/HorseRacing.Race.EditModeTests.asmdef Assets/Tests/PlayMode/HorseRacing.Race.PlayModeTests.asmdef
git commit -m "test: add race movement test assemblies"
~~~

### Task 2: Implement bounded tap effort test-first

**Files:**
- Create: Assets/Tests/EditMode/TapEffortModelTests.cs
- Create: Assets/Scripts/Race/TapEffortModel.cs

- [ ] **Step 1: Write failing tests**

~~~csharp
using NUnit.Framework;

namespace HorseRacing.Race.Tests
{
    public sealed class TapEffortModelTests
    {
        [Test]
        public void ExtremeSpam_IsBoundedAndClamped()
        {
            var model = new TapEffortModel();
            for (var i = 0; i < 100; i++) model.RegisterTap(i * 0.001f);

            var effort = model.Tick(0.1f, 0.02f, 1f, 4f, 0f, 0.5f);

            Assert.That(model.TapCount, Is.EqualTo(TapEffortModel.MaxTapHistory));
            Assert.That(effort, Is.EqualTo(1f));
        }

        [Test]
        public void ReleasedInput_ReachesExactIdle()
        {
            var model = new TapEffortModel();
            model.RegisterTap(0f);
            model.Tick(0f, 0.02f, 0.5f, 1f, 0f, 0.2f);

            Assert.That(model.Tick(2f, 1f, 0.5f, 1f, 0f, 0.2f), Is.Zero);
            Assert.That(model.TapCount, Is.Zero);
        }

        [Test]
        public void SelectGait_UsesLowerThresholdWhenDropping()
        {
            Assert.That(TapEffortModel.SelectGait(0.28f, 1, 0.08f, 0.28f, 0.52f, 0.78f, 0.05f), Is.EqualTo(2));
            Assert.That(TapEffortModel.SelectGait(0.25f, 2, 0.08f, 0.28f, 0.52f, 0.78f, 0.05f), Is.EqualTo(2));
            Assert.That(TapEffortModel.SelectGait(0.22f, 2, 0.08f, 0.28f, 0.52f, 0.78f, 0.05f), Is.EqualTo(1));
        }
    }
}
~~~

- [ ] **Step 2: Run tests and verify RED**

Run only HorseRacing.Race.Tests.TapEffortModelTests in EditMode.

First run may stop at a missing-type compiler error. Add this API-only stub, then rerun so RED is an assertion failure rather than a compilation error:

~~~csharp
namespace HorseRacing.Race
{
    public sealed class TapEffortModel
    {
        public const int MaxTapHistory = 64;
        public int TapCount => 0;
        public void RegisterTap(float timestamp) { }
        public float Tick(float now, float deltaTime, float tapWindow,
            float tapsPerSecondForMax, float accelTime, float coastTime) => -1f;
        public static int SelectGait(float effort, int gait, float walkAt,
            float trotAt, float canterAt, float gallopAt, float hysteresis) => -1;
        public void Reset() { }
    }
}
~~~

Expected after stub: FAIL on incorrect capacity, effort, and gait assertions. This proves tests exercise behavior rather than compilation.

- [ ] **Step 3: Add minimal complete implementation**

~~~csharp
using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing.Race
{
    public sealed class TapEffortModel
    {
        public const int MaxTapHistory = 64;
        readonly Queue<float> _tapTimes = new Queue<float>(MaxTapHistory);
        float _effort;

        public float Effort => _effort;
        public int TapCount => _tapTimes.Count;

        public void RegisterTap(float timestamp)
        {
            if (float.IsNaN(timestamp) || float.IsInfinity(timestamp)) return;
            while (_tapTimes.Count >= MaxTapHistory) _tapTimes.Dequeue();
            _tapTimes.Enqueue(timestamp);
        }

        public float Tick(float now, float deltaTime, float tapWindow,
            float tapsPerSecondForMax, float accelTime, float coastTime)
        {
            tapWindow = Mathf.Max(0.05f, tapWindow);
            tapsPerSecondForMax = Mathf.Max(0.1f, tapsPerSecondForMax);
            var cutoff = now - tapWindow;
            while (_tapTimes.Count > 0 && _tapTimes.Peek() < cutoff) _tapTimes.Dequeue();

            var target = Mathf.Clamp01((_tapTimes.Count / tapWindow) / tapsPerSecondForMax);
            var timeConstant = target > _effort ? accelTime : coastTime;
            if (timeConstant <= 0.0001f)
                _effort = target;
            else
                _effort = Mathf.Clamp01(Mathf.Lerp(
                    _effort, target,
                    1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / timeConstant)));

            if (_tapTimes.Count == 0 && _effort < 0.01f) _effort = 0f;
            return _effort;
        }

        public static int SelectGait(float effort, int gait, float walkAt,
            float trotAt, float canterAt, float gallopAt, float hysteresis)
        {
            effort = Mathf.Clamp01(effort);
            gait = Mathf.Clamp(gait, 0, 4);
            hysteresis = Mathf.Max(0f, hysteresis);

            while (gait < 4 && effort >= Threshold(gait + 1, walkAt, trotAt, canterAt, gallopAt))
                gait++;
            while (gait > 0 && effort < Mathf.Max(0f,
                       Threshold(gait, walkAt, trotAt, canterAt, gallopAt) - hysteresis))
                gait--;
            return gait;
        }

        static float Threshold(int gait, float walkAt, float trotAt, float canterAt, float gallopAt)
        {
            switch (gait)
            {
                case 1: return walkAt;
                case 2: return trotAt;
                case 3: return canterAt;
                default: return gallopAt;
            }
        }

        public void Reset()
        {
            _tapTimes.Clear();
            _effort = 0f;
        }
    }
}
~~~

- [ ] **Step 4: Run GREEN and commit**

Expected: 3 passed, 0 failed.

~~~powershell
git add -- Assets/Scripts/Race/TapEffortModel.cs Assets/Tests/EditMode/TapEffortModelTests.cs
git commit -m "feat: add bounded tap effort and gait hysteresis"
~~~

### Task 3: Consume root motion once

**Files:**
- Create: Assets/Tests/EditMode/RootMotionDistanceAccumulatorTests.cs
- Create: Assets/Scripts/Race/RootMotionDistanceAccumulator.cs

- [ ] **Step 1: Write failing test**

~~~csharp
using NUnit.Framework;
using UnityEngine;

namespace HorseRacing.Race.Tests
{
    public sealed class RootMotionDistanceAccumulatorTests
    {
        [Test]
        public void Consume_ReturnsHorizontalDistanceOnlyOnce()
        {
            var value = new RootMotionDistanceAccumulator();
            value.Add(new Vector3(0f, 5f, 3f));
            value.Add(new Vector3(4f, -2f, 0f));

            Assert.That(value.Consume(), Is.EqualTo(7f).Within(0.0001f));
            Assert.That(value.Consume(), Is.Zero);
        }
    }
}
~~~

- [ ] **Step 2: Run RED**

First run may stop at a missing-type compiler error. Add this API-only stub, then rerun:

~~~csharp
using UnityEngine;

namespace HorseRacing.Race
{
    public sealed class RootMotionDistanceAccumulator
    {
        public void Add(Vector3 delta) { }
        public float Consume() => -1f;
        public void Reset() { }
    }
}
~~~

Expected after stub: FAIL because the first consumption returns -1 instead of 7.

- [ ] **Step 3: Implement**

~~~csharp
using UnityEngine;

namespace HorseRacing.Race
{
    public sealed class RootMotionDistanceAccumulator
    {
        float _pending;

        public void Add(Vector3 delta)
        {
            delta.y = 0f;
            if (float.IsNaN(delta.x) || float.IsNaN(delta.z)) return;
            _pending += delta.magnitude;
        }

        public float Consume()
        {
            var value = _pending;
            _pending = 0f;
            return value;
        }

        public void Reset() => _pending = 0f;
    }
}
~~~

- [ ] **Step 4: Run GREEN and commit**

Expected: 4 total EditMode tests passed.

~~~powershell
git add -- Assets/Scripts/Race/RootMotionDistanceAccumulator.cs Assets/Tests/EditMode/RootMotionDistanceAccumulatorTests.cs
git commit -m "feat: consume root motion once per animation update"
~~~

### Task 4: Add configurable keyboard-only input

**Files:**
- Create: Assets/Tests/EditMode/ConfigurableKeyboardTapInputTests.cs
- Create: Assets/Tests/PlayMode/ConfigurableKeyboardTapInputPlayModeTests.cs
- Create: Assets/Scripts/Race/ConfigurableKeyboardTapInput.cs

- [ ] **Step 1: Write failing binding test**

~~~csharp
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace HorseRacing.Race.Tests
{
    public sealed class ConfigurableKeyboardTapInputTests
    {
        [Test]
        public void SetBindings_RemovesNoneAndDuplicates()
        {
            var input = new ConfigurableKeyboardTapInput();
            input.SetBindings(new[] { Key.Space, Key.None, Key.Space, Key.A });
            Assert.That(input.BindingCount, Is.EqualTo(2));
        }
    }
}
~~~

- [ ] **Step 2: Run RED**

First run may stop at a missing-type compiler error. Add this API-only stub, then rerun:

~~~csharp
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace HorseRacing.Race
{
    public sealed class ConfigurableKeyboardTapInput
    {
        public int BindingCount => 0;
        public void SetBindings(IEnumerable<Key> keys) { }
        public bool WasPressedThisFrame(Keyboard keyboard) => false;
    }
}
~~~

Expected after stub: FAIL because BindingCount is 0 instead of 2.

- [ ] **Step 3: Add binding implementation with a false polling stub**

~~~csharp
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace HorseRacing.Race
{
    public sealed class ConfigurableKeyboardTapInput
    {
        readonly HashSet<Key> _keys = new HashSet<Key>();
        public int BindingCount => _keys.Count;

        public void SetBindings(IEnumerable<Key> keys)
        {
            _keys.Clear();
            if (keys == null) return;
            foreach (var key in keys)
                if (key != Key.None) _keys.Add(key);
        }

        public bool WasPressedThisFrame(Keyboard keyboard) => false;
    }
}
~~~

Run EditMode test. Expected: PASS.

- [ ] **Step 4: Write failing real-keyboard PlayMode test**

~~~csharp
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace HorseRacing.Race.Tests
{
    public sealed class ConfigurableKeyboardTapInputPlayModeTests : InputTestFixture
    {
        [Test]
        public void Polling_AcceptsOnlyConfiguredKey()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var input = new ConfigurableKeyboardTapInput();
            input.SetBindings(new[] { Key.A });

            Press(keyboard.bKey);
            Assert.That(input.WasPressedThisFrame(keyboard), Is.False);
            Release(keyboard.bKey);
            Press(keyboard.aKey);
            Assert.That(input.WasPressedThisFrame(keyboard), Is.True);
        }
    }
}
~~~

Run PlayMode test. Expected: FAIL because polling stub returns false.

- [ ] **Step 5: Replace polling stub**

~~~csharp
public bool WasPressedThisFrame(Keyboard keyboard)
{
    if (keyboard == null) return false;
    foreach (var key in _keys)
    {
        var control = keyboard[key];
        if (control != null && control.wasPressedThisFrame) return true;
    }
    return false;
}
~~~

- [ ] **Step 6: Run GREEN and commit**

Expected: all EditMode tests and keyboard PlayMode test passed.

~~~powershell
git add -- Assets/Scripts/Race/ConfigurableKeyboardTapInput.cs Assets/Tests/EditMode/ConfigurableKeyboardTapInputTests.cs Assets/Tests/PlayMode/ConfigurableKeyboardTapInputPlayModeTests.cs
git commit -m "feat: add configurable keyboard-only race taps"
~~~

### Task 5: Make spline driver sole movement owner

**Files:**
- Modify: Assets/Scripts/Race/RaceSplineTapDriver.cs
- Create: Assets/Tests/PlayMode/RaceSplineTapDriverPlayModeTests.cs

- [ ] **Step 1: Write mouse regression against current driver**

~~~csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorseRacing.Race.Tests
{
    public sealed class RaceSplineTapDriverPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator MouseClick_DoesNotCreateRaceEffort()
        {
            var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            while (!load.isDone) yield return null;

            var driver = Object.FindFirstObjectByType<RaceSplineTapDriver>();
            Assert.That(driver, Is.Not.Null);
            var mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.leftButton);
            yield return null;

            Assert.That(driver.Effort, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
~~~

- [ ] **Step 2: Run RED**

Expected: FAIL because current WasTap counts Mouse.current.leftButton.

- [ ] **Step 3: Refactor RaceSplineTapDriver**

Keep existing serialized field names so Main.unity values survive. Add namespace HorseRacing.Race, DisallowMultipleComponent, and these fields:

~~~csharp
[SerializeField] Key[] tapKeys = { Key.Space, Key.W, Key.UpArrow };
[SerializeField, Range(0f, 0.2f)] float gaitHysteresis = 0.04f;

readonly TapEffortModel _effortModel = new TapEffortModel();
readonly RootMotionDistanceAccumulator _rootMotion = new RootMotionDistanceAccumulator();
readonly ConfigurableKeyboardTapInput _keyboardInput = new ConfigurableKeyboardTapInput();

Rigidbody _rigidbody;
Spline _spline;
float _splineLength = 1f;
float _normalizedT;
float _yawVelocity;
int _gait;
bool _ready;

public float Effort => _effortModel.Effort;
public void RegisterTap() => _effortModel.RegisterTap(Time.time);
~~~

Awake must cache MAnimal, Animator, Rigidbody, and RaceTrackSpline; validate spline count and log one error before disabling itself; configure bindings; disable SplineAnimate, MAnimalAIControl, MInputLink, PlayerInput, Aim, and LockOnTarget once; then establish one movement owner:

~~~csharp
animal.RootMotion = true;
animal.DisablePosition = true;
animal.DisableRotation = true;
animal.UseCameraInput = false;
animal.Strafe = false;
animal.UseSprint = false;
animal.CanSprint = false;
animal.Sprint_Set(false);
animal.SetAnimatorSpeed(1f);
animator.applyRootMotion = true;
animator.speed = 1f;

if (_rigidbody)
{
    _rigidbody.linearVelocity = Vector3.zero;
    _rigidbody.angularVelocity = Vector3.zero;
    _rigidbody.isKinematic = true;
    _rigidbody.useGravity = false;
    _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
}
~~~

Update must contain only keyboard polling, effort update, hysteretic gait selection, and Malbers gait control:

~~~csharp
if (_keyboardInput.WasPressedThisFrame(Keyboard.current)) RegisterTap();

_effortModel.Tick(Time.time, Time.deltaTime, tapWindow, tapsPerSecondForMax,
    accelSmoothTime, coastSmoothTime);
_gait = TapEffortModel.SelectGait(_effortModel.Effort, _gait,
    walkAt, trotAt, canterAt, gallopAt, gaitHysteresis);
DriveGait(_gait);
~~~

Remove Mouse and Touchscreen references. Remove per-frame GetComponentsInChildren Animator scans and all direct SetFloat calls. Idle gait must call SetInputAxis(Vector3.zero), StopMoving, and State_Activate(0). Gaits 1 through 4 must call SetInputAxis(Vector3.forward), State_Activate(1), and Speed_CurrentIndex_Set(gait). Sprint remains false.

Capture Animator delta only at animation evaluation:

~~~csharp
void OnAnimatorMove()
{
    if (_ready && _gait > 0)
        _rootMotion.Add(animator.deltaPosition);
}
~~~

Consume once in LateUpdate:

~~~csharp
void LateUpdate()
{
    if (!_ready) return;
    var distance = _rootMotion.Consume();
    if (distance > 0.00001f)
        _normalizedT = Mathf.Repeat(_normalizedT + distance / _splineLength, 1f);
    ApplyPose(false);
}
~~~

Retain NearestT, tangent fallback, look-ahead yaw, and spline position code. Rename old _t and _length fields to _normalizedT and _splineLength. OnDisable resets model/accumulator, stops MAnimal, and restores the Rigidbody and MAnimal ownership flags captured in Awake.

- [ ] **Step 4: Validate and compile**

Run validate_script at standard level with diagnostics. Wait for compilation, then read Console errors.

Expected: zero script diagnostics; no Animator “Parameter ... does not exist” errors.

- [ ] **Step 5: Run GREEN tests**

Expected: mouse regression passes; all EditMode and PlayMode tests pass.

- [ ] **Step 6: Commit code without Main.unity**

~~~powershell
git add -- Assets/Scripts/Race/RaceSplineTapDriver.cs Assets/Tests/PlayMode/RaceSplineTapDriverPlayModeTests.cs
git commit -m "fix: synchronize horse spline travel with root motion"
~~~

### Task 6: Tune existing horse and camera through Unity MCP

**Files:**
- Modify: Assets/Scenes/Main.unity

- [ ] **Step 1: Re-read editor state and targets**

Read mcpforunity://editor/state and mcpforunity://scene/cameras. Find RaceSetup/Horse Realistic by path and CM Third Person Main by name.

Expected: exactly one race horse; CM Third Person Main active.

- [ ] **Step 2: Set natural component values**

~~~text
manage_components(
  action="set_property",
  target="RaceSetup/Horse Realistic",
  search_method="by_path",
  component_type="RaceSplineTapDriver",
  properties={
    "tapWindow":0.85,
    "tapsPerSecondForMax":6.0,
    "accelSmoothTime":0.22,
    "coastSmoothTime":0.55,
    "walkAt":0.08,
    "trotAt":0.28,
    "canterAt":0.52,
    "gallopAt":0.78,
    "gaitHysteresis":0.04,
    "lookAheadMeters":12.0,
    "turnSmoothTime":0.2
  })
~~~

Read the component back. Expected: exact values above.

- [ ] **Step 3: Remove continuous camera shake**

~~~text
manage_camera(
  action="set_noise",
  target="CM Third Person Main",
  search_method="by_name",
  properties={"amplitudeGain":0.0,"frequencyGain":0.3})
~~~

Expected: active camera unchanged; noise amplitude zero. Do not add Motion Blur.

- [ ] **Step 4: Save and inspect scene**

Save Assets/Scenes/Main.unity through Unity MCP. Run git diff on only Main.unity. Expected: approved values plus pre-existing user work. Leave file unstaged and uncommitted.

### Task 7: Fresh verification

- [ ] **Step 1: Compile gate**

Wait for ready_for_tools=true, is_compiling=false, and no pending domain reload. Clear Console and refresh once.

Expected: no RaceSplineTapDriver errors. Report unrelated pre-existing missing-script and negative-scale collider errors separately.

- [ ] **Step 2: Run all EditMode tests**

Run tests, then wait up to 60 seconds with get_test_job.

Expected: all passed, zero failed.

- [ ] **Step 3: Run all PlayMode tests**

Run tests, then wait up to 60 seconds with get_test_job.

Expected: all passed, zero failed.

- [ ] **Step 4: Hands-off Play Mode check**

Play Main for at least five seconds without input.

Expected: Horse Realistic position remains fixed, effort exactly zero, idle gait, no autonomous motion.

- [ ] **Step 5: Input and release check**

Tap each configured key separately, spam keys to Gallop, then release.

Expected: smooth Walk → Trot → Canter → Gallop; native stride owns travel; quick smooth decay to true idle.

- [ ] **Step 6: Camera and turn check**

Capture idle, Gallop, and curve screenshots at 640px through Unity MCP.

Expected: no continuous shake, no post-processing blur, no spline snapping, horse aligned with track.

- [ ] **Step 7: Final evidence**

~~~powershell
git diff --check
git status --short
git log -6 --oneline
~~~

Expected: implementation/test commits present; Main.unity remains intentional user-owned unstaged work with race tuning.
