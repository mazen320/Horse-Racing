# Event Coast, Sprint, and Chase Camera Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `RaceSetup/Horse Realistic` a predictable coast-to-stop, a sustained-tap Sprint tier, and a centered locked-behind event camera.

**Architecture:** The pure `TapEffortModel` selects requested gaits 0-5 with hysteresis, while `GaitTravelSpeedModel` independently rate-limits physical travel and derives the locomotion gait needed to avoid sliding during deceleration. `RaceSplineTapDriver` remains the sole spline pose owner and maps those values to Malbers. Cinemachine and manual-look settings remain scene configuration, verified in PlayMode.

**Tech Stack:** Unity 6000.5.9f1, C#, Input System 1.20.0, Splines 2.9.0, Cinemachine 3.1.6, Malbers Animal Controller, Unity Test Framework 1.7.0, Unity MCP.

---

## File map

- Modify `Assets/Scripts/Race/TapEffortModel.cs`: add Sprint threshold and 0-5 gait hysteresis.
- Modify `Assets/Tests/EditMode/TapEffortModelTests.cs`: prove Sprint entry and exit behavior.
- Modify `Assets/Scripts/Race/GaitTravelSpeedModel.cs`: add Sprint speed, separate deceleration, exact stop, and animation-gait selection.
- Modify `Assets/Tests/EditMode/GaitTravelSpeedModelTests.cs`: prove capped Sprint and frame-rate-independent coasting.
- Modify `Assets/Scripts/Race/RaceSplineTapDriver.cs`: integrate gait 5, retain locomotion while coasting, and restore Sprint ownership.
- Modify `Assets/Tests/PlayMode/RaceSplineTapDriverPlayModeTests.cs`: simulate W spam, Sprint, coast, animated stop, and hands-off stability.
- Modify `Assets/Tests/PlayMode/HorseRacing.Race.PlayModeTests.asmdef`: reference Cinemachine camera assemblies for direct assertions.
- Create `Assets/Tests/PlayMode/EventCameraPlayModeTests.cs`: verify centered follow camera, locked look, and zero noise.
- Modify `Assets/Scenes/Main.unity` through Unity MCP: event values and chase-camera overrides.

The Unity Editor is attached to the current checkout, and the user explicitly requested commits and push from `main`; do not move the scene into a detached worktree. The worktree is clean at plan creation.

### Task 1: Add Sprint gait hysteresis

**Files:**
- Modify: `Assets/Tests/EditMode/TapEffortModelTests.cs`
- Modify: `Assets/Scripts/Race/TapEffortModel.cs`

- [ ] **Step 1: Write the failing Sprint threshold test**

Add this test and update existing `SelectGait` calls to pass `0.92f` before hysteresis:

```csharp
[Test]
public void SelectGait_SprintUsesSeparateExitThreshold()
{
    Assert.That(TapEffortModel.SelectGait(
        0.92f, 4, 0.08f, 0.28f, 0.52f, 0.78f, 0.92f, 0.06f), Is.EqualTo(5));
    Assert.That(TapEffortModel.SelectGait(
        0.87f, 5, 0.08f, 0.28f, 0.52f, 0.78f, 0.92f, 0.06f), Is.EqualTo(5));
    Assert.That(TapEffortModel.SelectGait(
        0.85f, 5, 0.08f, 0.28f, 0.52f, 0.78f, 0.92f, 0.06f), Is.EqualTo(4));
}
```

- [ ] **Step 2: Run the test and establish RED**

Run `HorseRacing.Race.Tests.TapEffortModelTests` in EditMode through Unity MCP.

If the new call first produces an overload compiler error, add only this overload stub and rerun:

```csharp
public static int SelectGait(float effort, int gait, float walkAt,
    float trotAt, float canterAt, float gallopAt, float sprintAt, float hysteresis)
{
    return -1;
}
```

Expected: the new test fails because `-1` is not gait 5.

- [ ] **Step 3: Implement the minimal 0-5 selector**

Replace the selector with:

```csharp
public static int SelectGait(float effort, int gait, float walkAt,
    float trotAt, float canterAt, float gallopAt, float sprintAt, float hysteresis)
{
    effort = Mathf.Clamp01(effort);
    gait = Mathf.Clamp(gait, 0, 5);
    hysteresis = Mathf.Max(0f, hysteresis);

    while (gait < 5 && effort >= Threshold(
               gait + 1, walkAt, trotAt, canterAt, gallopAt, sprintAt))
        gait++;
    while (gait > 0 && effort < Mathf.Max(0f,
               Threshold(gait, walkAt, trotAt, canterAt, gallopAt, sprintAt) - hysteresis))
        gait--;
    return gait;
}

static float Threshold(int gait, float walkAt, float trotAt,
    float canterAt, float gallopAt, float sprintAt)
{
    return gait switch
    {
        1 => walkAt,
        2 => trotAt,
        3 => canterAt,
        4 => gallopAt,
        _ => sprintAt
    };
}
```

- [ ] **Step 4: Run GREEN and commit**

Run all EditMode tests. Expected: all existing tests plus Sprint hysteresis pass.

```powershell
git add -- Assets/Scripts/Race/TapEffortModel.cs Assets/Tests/EditMode/TapEffortModelTests.cs
git commit -m "feat: add sustained-tap sprint gait"
```

### Task 2: Add deterministic coast-to-stop travel

**Files:**
- Modify: `Assets/Tests/EditMode/GaitTravelSpeedModelTests.cs`
- Modify: `Assets/Scripts/Race/GaitTravelSpeedModel.cs`

- [ ] **Step 1: Replace the snap-stop test with failing coast tests**

Update test calls to the new signature and add:

```csharp
[Test]
public void Sprint_UsesCappedSprintSpeed()
{
    var model = new GaitTravelSpeedModel();
    model.Step(5, 1f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f, 20f, 3f);
    Assert.That(model.Speed, Is.EqualTo(8.5f).Within(0.0001f));
}

[Test]
public void ReleasedInput_CoastsThenStopsExactly()
{
    var model = new GaitTravelSpeedModel();
    model.Step(5, 1f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f, 20f, 3f);

    var firstCoastDistance = model.Step(
        0, 0.5f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f, 20f, 3f);

    Assert.That(firstCoastDistance, Is.GreaterThan(0f));
    Assert.That(model.Speed, Is.EqualTo(7f).Within(0.0001f));

    for (var i = 0; i < 10; i++)
        model.Step(0, 0.5f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f, 20f, 3f);

    Assert.That(model.Speed, Is.Zero);
}

[Test]
public void CoastingSpeed_KeepsALocomotionGaitUntilExactStop()
{
    Assert.That(GaitTravelSpeedModel.SelectAnimationGait(
        0, 1.2f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f), Is.EqualTo(1));
    Assert.That(GaitTravelSpeedModel.SelectAnimationGait(
        0, 0f, 1.6f, 3.2f, 5.2f, 7.2f, 8.5f), Is.Zero);
}
```

- [ ] **Step 2: Run the tests and establish RED**

Run `HorseRacing.Race.Tests.GaitTravelSpeedModelTests` in EditMode. If necessary, add signature-only overloads returning `-1f` and `-1` so compilation succeeds.

Expected: failures show idle still snaps to zero, Sprint is not distinct, and animation gait selection is absent.

- [ ] **Step 3: Implement separate acceleration and deceleration**

Replace `Step` and add the selector:

```csharp
public float Step(int gait, float deltaTime, float walkSpeed, float trotSpeed,
    float canterSpeed, float gallopSpeed, float sprintSpeed,
    float acceleration, float deceleration)
{
    if (deltaTime <= 0f) return 0f;

    var target = gait switch
    {
        <= 0 => 0f,
        1 => walkSpeed,
        2 => trotSpeed,
        3 => canterSpeed,
        4 => gallopSpeed,
        _ => sprintSpeed
    };

    target = Mathf.Max(0f, target);
    var previous = Speed;
    var rate = target > previous ? acceleration : deceleration;
    Speed = Mathf.MoveTowards(previous, target, Mathf.Max(0.01f, rate) * deltaTime);
    if (Speed < 0.001f) Speed = 0f;
    return (previous + Speed) * 0.5f * deltaTime;
}

public static int SelectAnimationGait(int requestedGait, float speed,
    float walkSpeed, float trotSpeed, float canterSpeed,
    float gallopSpeed, float sprintSpeed)
{
    if (speed <= 0.001f) return Mathf.Clamp(requestedGait, 0, 5);

    var speedGait = speed < (walkSpeed + trotSpeed) * 0.5f ? 1
        : speed < (trotSpeed + canterSpeed) * 0.5f ? 2
        : speed < (canterSpeed + gallopSpeed) * 0.5f ? 3
        : speed < (gallopSpeed + sprintSpeed) * 0.5f ? 4
        : 5;
    return Mathf.Max(Mathf.Clamp(requestedGait, 0, 5), speedGait);
}
```

Update the existing frame-rate test to call gait 4 with Sprint `8.5f` and deceleration `3f`.

- [ ] **Step 4: Run GREEN and commit**

Run all EditMode tests. Expected: all pass, including equal 30/120 FPS travel and exact stop.

```powershell
git add -- Assets/Scripts/Race/GaitTravelSpeedModel.cs Assets/Tests/EditMode/GaitTravelSpeedModelTests.cs
git commit -m "fix: coast horse travel to an exact stop"
```

### Task 3: Integrate Sprint and animated coasting in the driver

**Files:**
- Modify: `Assets/Tests/PlayMode/RaceSplineTapDriverPlayModeTests.cs`
- Modify: `Assets/Scripts/Race/RaceSplineTapDriver.cs`
- Modify: `Assets/Scenes/Main.unity`

- [ ] **Step 1: Write failing PlayMode behavior assertions**

Expose runtime telemetry in the intended API and extend the W-spam test to assert:

```csharp
Assert.That(driver.RequestedGait, Is.EqualTo(5));
Assert.That(driver.AnimationGait, Is.EqualTo(5));
Assert.That(driver.TravelSpeed, Is.GreaterThan(driver.gallopMetersPerSecond));
Assert.That(driver.animal.CanSprint, Is.True);
Assert.That(driver.animal.Sprint, Is.True);
Assert.That(driver.animal.CurrentSpeedIndex, Is.EqualTo(5));
```

Immediately after releasing all keys, wait one frame and assert the coast is real and animated:

```csharp
var releasePosition = driver.transform.position;
var releaseSpeed = driver.TravelSpeed;
yield return null;
yield return new WaitForEndOfFrame();

Assert.That(driver.TravelSpeed, Is.GreaterThan(0f));
Assert.That(driver.TravelSpeed, Is.LessThanOrEqualTo(releaseSpeed));
Assert.That(driver.AnimationGait, Is.GreaterThan(0));
Assert.That(Vector3.Distance(driver.transform.position, releasePosition), Is.GreaterThan(0f));
```

At the end of the existing release timeout, assert:

```csharp
Assert.That(driver.TravelSpeed, Is.Zero.Within(0.0001f));
Assert.That(driver.AnimationGait, Is.Zero);
Assert.That(driver.animal.ActiveState.ID.ID, Is.EqualTo(0));
```

- [ ] **Step 2: Run PlayMode tests and establish RED**

Run `HorseRacing.Race.Tests.RaceSplineTapDriverPlayModeTests` through Unity MCP.

Expected: compile failure until read-only telemetry properties are added; after adding only those properties, assertions fail because Sprint is disabled and idle still snaps travel.

- [ ] **Step 3: Add serialized Sprint/coast fields and validation**

Add fields:

```csharp
public float sprintAt = 0.92f;
public float sprintMetersPerSecond = 8.5f;
[Tooltip("How quickly travel slows after effort drops.")]
public float travelDeceleration = 3f;

int _animationGait;
bool _originalAnimalUseSprint;
bool _originalAnimalSprint;

public float TravelSpeed => _travelSpeed.Speed;
public int RequestedGait => _gait;
public int AnimationGait => _animationGait;
```

In `OnValidate`, clamp `sprintAt` above `gallopAt`, clamp Sprint speed above Gallop, and clamp deceleration to at least `0.01f`.

- [ ] **Step 4: Capture and own Malbers Sprint safely**

In `CaptureOwnership`:

```csharp
_originalAnimalUseSprint = animal.UseSprint;
_originalAnimalSprint = animal.Sprint;
```

In `ConfigureMovementOwnership`, replace Sprint disabling with:

```csharp
animal.UseSprint = true;
animal.CanSprint = true;
animal.Sprint_Set(false);
```

In `OnDisable`, before restoring other flags:

```csharp
animal.Sprint_Set(false);
animal.UseSprint = _originalAnimalUseSprint;
if (_originalAnimalSprint) animal.Sprint_Set(true);
```

- [ ] **Step 5: Separate requested gait from animation gait**

Update `SelectGait` to pass `sprintAt`. After selection in `Update`, derive and drive the gait:

```csharp
_animationGait = GaitTravelSpeedModel.SelectAnimationGait(
    _gait, _travelSpeed.Speed, walkMetersPerSecond, trotMetersPerSecond,
    canterMetersPerSecond, gallopMetersPerSecond, sprintMetersPerSecond);
DriveGait(_animationGait);
```

Change `OnAnimatorMove` to require `_animationGait > 0`. In `LateUpdate`, call the new travel signature, then recalculate `_animationGait` and call `DriveGait` again so exact zero becomes Idle in the same rendered frame.

- [ ] **Step 6: Map gait 5 to Malbers Sprint**

In `DriveGait`, keep Sprint enabled while owned. For locomotion:

```csharp
var sprinting = gait >= 5;
animal.AlwaysForward = true;
animal.SetInputAxis(Vector3.forward);
if (animal.ActiveState == null || animal.ActiveState.ID.ID != 1)
    animal.State_Force(1);

if (sprinting)
{
    if (animal.CurrentSpeedIndex != 4 && animal.CurrentSpeedIndex != 5)
        animal.Speed_CurrentIndex_Set(4);
    animal.Sprint_Set(true);
}
else
{
    animal.Sprint_Set(false);
    if (animal.CurrentSpeedIndex != gait)
        animal.Speed_CurrentIndex_Set(gait);
}
```

Idle must call `Sprint_Set(false)`, stop movement, and force state 0 only after `AnimationGait` reaches zero.

- [ ] **Step 7: Apply approved event values through Unity MCP**

On `RaceSetup/Horse Realistic`, set:

```text
tapWindow=0.85
tapsPerSecondForMax=6.0
accelSmoothTime=0.22
coastSmoothTime=0.55
sprintAt=0.92
gaitHysteresis=0.06
sprintMetersPerSecond=8.5
travelAcceleration=4.5
travelDeceleration=3.0
```

Save `Assets/Scenes/Main.unity` and read the component back.

- [ ] **Step 8: Compile, run GREEN, and commit**

Validate the driver, wait for compilation, and read Console errors. Run all EditMode tests and all existing PlayMode tests.

Expected: sustained W spam reaches gait/index 5; release retains locomotion while speed falls; speed reaches exact zero and Idle; hands-off remains stationary.

```powershell
git add -- Assets/Scripts/Race/RaceSplineTapDriver.cs Assets/Tests/PlayMode/RaceSplineTapDriverPlayModeTests.cs Assets/Scenes/Main.unity
git commit -m "feat: add animated sprint and smooth event stopping"
```

### Task 4: Center and lock the chase camera

**Files:**
- Modify: `Assets/Tests/PlayMode/HorseRacing.Race.PlayModeTests.asmdef`
- Create: `Assets/Tests/PlayMode/EventCameraPlayModeTests.cs`
- Modify: `Assets/Scenes/Main.unity`

- [ ] **Step 1: Add camera test references**

Add `Unity.Cinemachine`, `MalbersAnimations.Cinemachine`, and `MalbersAnimations.InputSystem` to the PlayMode test assembly references.

- [ ] **Step 2: Write the failing camera test**

```csharp
using System.Collections;
using System.Linq;
using MalbersAnimations;
using MalbersAnimations.InputSystem;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorseRacing.Race.Tests
{
    public sealed class EventCameraPlayModeTests
    {
        [UnityTest]
        public IEnumerator MainScene_UsesCenteredLockedBehindMountCamera()
        {
            var load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return new WaitForEndOfFrame();

            var follows = Object.FindObjectsByType<CinemachineThirdPersonFollow>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var mount = follows.Single(value => value.gameObject.name == "CM Third Person Mount");

            Assert.That(mount.ShoulderOffset.x, Is.Zero.Within(0.001f));
            Assert.That(mount.ShoulderOffset.y, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(mount.VerticalArmLength, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(mount.CameraDistance, Is.EqualTo(6.75f).Within(0.001f));
            Assert.That(mount.CameraSide, Is.EqualTo(0.5f).Within(0.001f));

            var targets = Object.FindObjectsByType<ThirdPersonFollowTarget>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(targets, Is.Not.Empty);
            Assert.That(targets.All(value => !value.AllowCameraRotation.Value), Is.True);

            var lookLinks = Object.FindObjectsByType<MInputLinkLook>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(lookLinks.All(value => !value.enabled), Is.True);

            var noise = mount.GetComponent<CinemachineBasicMultiChannelPerlin>();
            Assert.That(noise == null || noise.AmplitudeGain == 0f, Is.True);
        }
    }
}
```

- [ ] **Step 3: Run the camera test and establish RED**

Run `HorseRacing.Race.Tests.EventCameraPlayModeTests`.

Expected: failures on shoulder centering/distance and manual camera rotation.

- [ ] **Step 4: Apply camera configuration through Unity MCP**

Read active instances and select the Horse-Racing Editor. Load `Main`, then set both `CM Third Person Mount` and `CM Third Person Main` `CinemachineThirdPersonFollow` values:

```text
Damping=(0.15, 0.25, 0.15)
ShoulderOffset=(0.0, 0.2, 0.0)
VerticalArmLength=0.8
CameraSide=0.5
CameraDistance=6.75
```

Set `CinemachineBasicMultiChannelPerlin.AmplitudeGain=0` on both rigs. Set every event `ThirdPersonFollowTarget.AllowCameraRotation=false`, its `CameraDistance=6.75`, and its camera side to `0.5`. Disable scene `MInputLinkLook` components so mouse/clicks cannot orbit the camera. Save `Assets/Scenes/Main.unity`.

- [ ] **Step 5: Run GREEN and commit**

Run the camera test and all PlayMode tests. Expected: all pass.

```powershell
git add -- Assets/Tests/PlayMode/HorseRacing.Race.PlayModeTests.asmdef Assets/Tests/PlayMode/EventCameraPlayModeTests.cs Assets/Tests/PlayMode/EventCameraPlayModeTests.cs.meta Assets/Scenes/Main.unity
git commit -m "fix: center and lock event chase camera"
```

### Task 5: Fresh end-to-end verification and push

- [ ] **Step 1: Compilation and Console gate**

Wait for Unity `isCompiling=false` and `readyForTools=true`. Clear the Console, refresh assets once, wait again, then read all Errors and Warnings.

Expected: zero C# compiler errors and no race-driver runtime exceptions.

- [ ] **Step 2: Run all EditMode tests**

Run the full EditMode suite through Unity MCP and wait for the test job.

Expected: every test passed, zero failed.

- [ ] **Step 3: Run all PlayMode tests**

Run the full PlayMode suite through Unity MCP and wait for the test job.

Expected: every test passed, zero failed.

- [ ] **Step 4: Perform live event checks**

Enter Play Mode with no input for one second, then simulate rapid W presses for at least two seconds, release, and observe until stop.

Expected evidence:

- Hands-off effort/speed remain exactly zero.
- W spam reaches requested gait 5, animation gait 5, Malbers Sprint true, and speed index 5.
- On release, speed decreases monotonically while a non-idle clip remains active.
- Final speed is zero, state is Idle, and position remains unchanged for a further 0.25 seconds.
- The active view stays centered behind the preferred realistic horse without mouse orbit or camera noise.

- [ ] **Step 5: Inspect repository and push**

```powershell
git diff --check
git status --short
git log -8 --oneline
git push origin main
```

Expected: clean worktree, implementation commits above the approved design/plan commits, and `origin/main` updated.
