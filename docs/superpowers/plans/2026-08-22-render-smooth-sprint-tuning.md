# Render-Smooth Horse and Sprint Tuning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate fixed/render clock jitter on the preferred horse and rider, keep the chase camera synchronized, and expose a safe faster Sprint target.

**Architecture:** `RaceSplineTapDriver` remains the sole spline pose owner but runs before Malbers and Cinemachine, temporarily selects render-synchronized animation, and restores owned settings on disable. `Main` configures the Cinemachine Brain for LateUpdate. Existing Malbers Sprint animation scaling is retained while the travel target moves to `9.25 m/s` under a `10.5 m/s` safety ceiling.

**Tech Stack:** Unity 6000.5.9f1, C#, Cinemachine 3.1.6, Malbers Animal Controller/HAP, Unity Test Framework 1.7.0, Unity MCP.

---

## File map

- Modify `Assets/Tests/PlayMode/RaceSplineTapDriverPlayModeTests.cs`: reproduce and guard the horse/rider/camera clock mismatch and faster Sprint scene value.
- Modify `Assets/Tests/EditMode/GaitTravelSpeedModelTests.cs`: verify the maximum recommended Sprint ceiling.
- Modify `Assets/Scripts/Race/GaitTravelSpeedModel.cs`: own the pure Sprint-speed clamp used by Inspector validation.
- Modify `Assets/Scripts/Race/RaceSplineTapDriver.cs`: run before Malbers/camera, own Animator update mode, and expose the bounded Sprint range.
- Modify `Assets/Scenes/Main.unity` through Unity MCP: set Cinemachine to LateUpdate and Sprint to `9.25 m/s`.

The Unity Editor is attached to the current checkout, and the user explicitly requested direct implementation, commit, and push from the existing `main` workflow. Do not move the live scene into a detached worktree. Preserve unrelated Unity-generated settings/resources from the working tree.

### Task 1: Reproduce the timing and Sprint-safety failures

**Files:**
- Modify: `Assets/Tests/PlayMode/RaceSplineTapDriverPlayModeTests.cs`
- Modify: `Assets/Tests/EditMode/GaitTravelSpeedModelTests.cs`

- [ ] **Step 1: Write the failing PlayMode timing test**

Add imports for `MalbersAnimations.HAP` and `Unity.Cinemachine`, then add a test that loads `Main`, waits one rendered frame, and asserts:

```csharp
Assert.That(driver.animator.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal));
Assert.That(Object.FindFirstObjectByType<MRider>().Anim.updateMode,
    Is.EqualTo(AnimatorUpdateMode.Normal));
Assert.That(Object.FindFirstObjectByType<CinemachineBrain>().UpdateMethod,
    Is.EqualTo(CinemachineBrain.UpdateMethods.LateUpdate));
Assert.That(driver.sprintMetersPerSecond,
    Is.EqualTo(9.25f).Within(0.001f));
```

- [ ] **Step 2: Write the failing EditMode safety test**

Add a test for the intended pure clamp:

```csharp
[Test]
public void SprintSpeed_IsBoundedForEventPresentation()
{
    Assert.That(GaitTravelSpeedModel.ClampSprintSpeed(7.2f, 99f),
        Is.EqualTo(10.5f));
    Assert.That(GaitTravelSpeedModel.ClampSprintSpeed(7.2f, 6f),
        Is.EqualTo(7.2f));
}
```

- [ ] **Step 3: Run both tests and establish RED**

Run the new PlayMode test through Unity MCP. Expected: horse/rider are `Fixed`, Brain is `FixedUpdate`, and scene Sprint is `8.5`.

Run the EditMode test. If compilation initially fails because `ClampSprintSpeed` is missing, add only this compiling stub and rerun:

```csharp
public static float ClampSprintSpeed(float gallopSpeed, float sprintSpeed) => -1f;
```

Expected: the EditMode assertion fails because `-1` is not `10.5`.

### Task 2: Align animation and spline execution clocks

**Files:**
- Modify: `Assets/Scripts/Race/RaceSplineTapDriver.cs`
- Modify: `Assets/Scripts/Race/GaitTravelSpeedModel.cs`

- [ ] **Step 1: Implement the Sprint clamp**

Add:

```csharp
public const float MaximumRecommendedSprintSpeed = 10.5f;

public static float ClampSprintSpeed(float gallopSpeed, float sprintSpeed)
{
    var minimum = Mathf.Clamp(gallopSpeed, 0f, MaximumRecommendedSprintSpeed);
    return Mathf.Clamp(sprintSpeed, minimum, MaximumRecommendedSprintSpeed);
}
```

- [ ] **Step 2: Make the race owner render-synchronized**

Change the driver attribute to `[DefaultExecutionOrder(-100)]`. Capture `_originalAnimatorUpdateMode`, set `animator.updateMode = AnimatorUpdateMode.Normal` in `ConfigureMovementOwnership`, and restore it in `OnDisable`.

- [ ] **Step 3: Bound and describe Sprint tuning**

Add `[Range(8.5f, GaitTravelSpeedModel.MaximumRecommendedSprintSpeed)]` and a tooltip to `sprintMetersPerSecond`. In `OnValidate`, replace the unbounded minimum with:

```csharp
sprintMetersPerSecond = GaitTravelSpeedModel.ClampSprintSpeed(
    gallopMetersPerSecond, sprintMetersPerSecond);
```

- [ ] **Step 4: Run EditMode GREEN**

Run all EditMode tests. Expected: all tests pass, including the new safety ceiling.

### Task 3: Configure Cinemachine and the event Sprint value

**Files:**
- Modify: `Assets/Scenes/Main.unity`

- [ ] **Step 1: Apply scene values through Unity MCP**

Set `RaceSetup/CM Brain` `CinemachineBrain.UpdateMethod` to `LateUpdate` and `RaceSetup/Horse Realistic` `RaceSplineTapDriver.sprintMetersPerSecond` to `9.25`.

- [ ] **Step 2: Save and read back**

Save `Assets/Scenes/Main.unity`, then read both components back and verify `UpdateMethod=1` and Sprint `9.25`.

- [ ] **Step 3: Run PlayMode GREEN**

Run the timing regression and all PlayMode tests. Expected: horse and rider are `Normal`, Brain is `LateUpdate`, W spam still reaches animated Sprint, and release still coasts to exact Idle.

### Task 4: Fresh verification, commit, and push

- [ ] **Step 1: Check compilation and Console**

Wait for Unity compilation/domain reload to finish, then read Errors and Warnings. Expected: zero compiler errors and no race runtime exceptions.

- [ ] **Step 2: Run the full suites**

Run all EditMode tests and all PlayMode tests through Unity MCP. Expected: zero failed tests.

- [ ] **Step 3: Perform a visual runtime check**

Enter Play Mode, capture the active game view behind the preferred horse, and inspect the active components. Expected: render-synchronized clocks, centered/no-noise camera, and the preferred realistic horse remains the race target.

- [ ] **Step 4: Inspect only intended changes, commit, and push**

```powershell
git diff --check
git diff -- Assets/Scripts/Race/GaitTravelSpeedModel.cs Assets/Scripts/Race/RaceSplineTapDriver.cs Assets/Tests/EditMode/GaitTravelSpeedModelTests.cs Assets/Tests/PlayMode/RaceSplineTapDriverPlayModeTests.cs Assets/Scenes/Main.unity docs/superpowers/specs/2026-08-22-render-smooth-sprint-tuning-design.md docs/superpowers/plans/2026-08-22-render-smooth-sprint-tuning.md
git add -- Assets/Scripts/Race/GaitTravelSpeedModel.cs Assets/Scripts/Race/RaceSplineTapDriver.cs Assets/Tests/EditMode/GaitTravelSpeedModelTests.cs Assets/Tests/PlayMode/RaceSplineTapDriverPlayModeTests.cs Assets/Scenes/Main.unity docs/superpowers/specs/2026-08-22-render-smooth-sprint-tuning-design.md docs/superpowers/plans/2026-08-22-render-smooth-sprint-tuning.md
git commit -m "fix: smooth horse render timing"
git push origin main
```

Leave unrelated `ProjectSettings/EditorSettings.asset` and generated GDK resources untouched and uncommitted.
