# Horse Spline Race Movement Design

Date: 2026-08-22

## Goal

Make the existing `RaceSetup/Horse Realistic` horse feel natural and reliable for an event installation. Players repeatedly press configurable keyboard keys to build speed. Horse travel must stay synchronized with its native Malbers animation, remain constrained to `RaceTrackSpline`, stop when input ends, and use a stable camera.

The Arabian jockey preview is outside scope and remains unchanged.

## Current Root Causes

1. `RaceSplineTapDriver.WasTap` treats left mouse clicks and touchscreen presses as taps. Clicking the Game view therefore moves the horse even though the intended event input is keyboard keys.
2. The horse Animator uses `AnimatorUpdateMode.Fixed`, but `animator.deltaPosition` is consumed in `LateUpdate`. A render frame can observe the same fixed-animation delta more than once, making spline travel depend on render frame rate and outrun the visible stride.
3. Malbers root motion, a non-kinematic Rigidbody, and `RaceSplineTapDriver.ApplyPose` all write movement. Multiple transform owners create jitter and unpredictable motion.
4. `DriveGait` searches all child Animators every frame and writes parameters that some child controllers do not contain. This allocates/work-scans every frame and produces repeated `Parameter ... does not exist` errors.
5. The scene has no Motion Blur volume override. The active Cinemachine camera does have `CinemachineBasicMultiChannelPerlin` noise with amplitude `0.5` and frequency `0.3`, which creates continuous shake and a blurred/jittery appearance.

## Chosen Architecture

Use the existing Malbers controller for animation and gait selection, but make `RaceSplineTapDriver` the sole owner of world position and yaw.

### Input

- Expose an Inspector-editable list of Unity Input System `Key` values.
- Default bindings: `Space`, `W`, and `UpArrow`.
- Count only rising keyboard edges (`wasPressedThisFrame`). Holding a key does not generate repeated taps.
- Ignore mouse and touchscreen devices completely.
- Reject duplicate configured keys so one physical press always counts once.
- Preserve a public `RegisterTap` entry point for a future physical event-panel adapter without coupling movement logic to hardware.

### Tap Effort

- Store recent tap timestamps in a bounded rolling window.
- Convert taps per second into normalized effort `[0, 1]`.
- Smooth acceleration and deceleration separately.
- Use a shorter coast than the current `1.25` seconds so released input reaches idle promptly without an abrupt stop.
- Clamp effort and tap storage so extreme key spam cannot increase travel beyond native gallop speed or grow memory.
- Add gait hysteresis: crossing upward selects the next gait; dropping must cross a slightly lower threshold. This prevents animation flicker near boundaries.

### Animation and Spline Travel

- Capture horizontal `Animator.deltaPosition` exactly once inside `OnAnimatorMove`, after the Animator evaluates.
- Accumulate any fixed-animation deltas until the next pose application.
- Disable Malbers world position and rotation application using its runtime `DisablePosition` and `DisableRotation` controls.
- Configure the horse Rigidbody as kinematic with gravity disabled. Physics no longer competes with spline placement.
- Keep Malbers root motion enabled because its animation controller still uses root-motion data and gait state.
- Convert accumulated root-motion distance to normalized spline distance using the cached spline length. Unity Splines normalized interpolation is distance-based through its curve-length lookup tables.
- Apply position and a smoothed look-ahead yaw once in `LateUpdate`, then clear consumed distance.
- Never multiply travel by tap rate, render delta time, or custom meters-per-second. Tap effort chooses gait; that gait's native root motion determines travel.
- Limit gait selection to Walk, Trot, Canter, and Gallop. Sprint remains disabled.

### Animator Safety and Performance

- Cache references during `Awake`; no `GetComponentsInChildren` calls inside per-frame code.
- Write locomotion parameters only through `MAnimal` and the primary horse Animator.
- Before writing any optional Animator parameter, cache its existence/hash once.
- Do not modify rider/child Animator parameters blindly.
- Restore owned runtime flags in `OnDisable` where needed so exiting Play Mode or disabling the driver does not leave conflicting state.

### Camera

- Disable the Basic Multi Channel Perlin component, or set its amplitude to zero, on the active race camera.
- Keep modest Cinemachine positional damping for smooth pursuit.
- Do not add post-processing Motion Blur.
- Verify the camera follows the spline horse rather than the Arabian preview object.

## Data Flow

1. Configured keyboard key edge calls `RegisterTap`.
2. Rolling tap history produces target effort.
3. Smoothed effort plus hysteresis selects Malbers gait.
4. Animator evaluates the selected gait in its fixed animation loop.
5. `OnAnimatorMove` captures that evaluation's root-motion distance once.
6. `LateUpdate` consumes accumulated distance, advances the spline parameter, and applies spline position/yaw.
7. Cinemachine follows the already-positioned horse with stable damping and no continuous noise.

## Error Handling

- Missing horse Animator, `MAnimal`, or valid spline disables movement and logs one actionable error instead of failing every frame.
- Empty key configuration means no input, not all keys.
- Invalid spline length prevents movement and logs once.
- Runtime configuration is clamped in `OnValidate` and initialization.
- Tap history has a fixed capacity and prunes old entries.

## Testing and Verification

### EditMode regression tests

- Configured key press registers one tap.
- Unconfigured key, mouse click, and touch do not register taps.
- Duplicate configured keys cannot double-count one press.
- No taps decay effort to exactly idle.
- Effort remains clamped under extreme spam.
- Gait hysteresis prevents boundary oscillation.
- One root-motion delta is consumed once, never once per render frame.
- Equal root-motion input produces equal spline distance at different render frame rates.

### Unity verification

- Run EditMode and PlayMode tests with zero failures.
- Validate changed scripts and wait for compilation.
- Clear Console, enter Play Mode, and confirm no errors from `RaceSplineTapDriver`.
- Hands off keyboard: horse remains idle for at least five seconds.
- Click Game view repeatedly: horse remains idle.
- Tap every configured key individually: each advances effort.
- Spam keys: horse reaches Gallop but never exceeds native animation travel.
- Release keys: horse decelerates smoothly and reaches true idle quickly.
- Observe straight sections and turns at multiple Game-view frame rates; feet and travel remain synchronized.
- Confirm camera has no continuous shake or post-processing motion blur.

## Non-Goals

- Replacing Malbers animations or the horse model.
- Modifying the Arabian jockey preview.
- Adding touch, mouse, networking, scoring, stamina, audio, or race-opponent systems.
- Retuning unrelated scene colliders or missing third-party scripts reported elsewhere in the scene.
