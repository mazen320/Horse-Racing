# Spline Race Camera Design

## Goal

Keep the camera polished, readable, and directly behind the preferred horse through the full spline, especially during Sprint turns. Build the player-one rig so a second independent rig can later render a side-by-side competition view.

## Root cause

`CM Third Person Mount` currently uses Malbers `ThirdPersonFollowTarget`. Its `CameraRotation` method returns immediately when `AllowCameraRotation` is false. The runtime pivot therefore follows the horse position but does not continue following its yaw. On a bend, the horse rotates while the pivot remains near its old world heading. Cinemachine moves toward a side view and later performs a large angular correction. That rapid lateral screen motion presents as smearing or distortion even though the scene has no motion-blur volume and camera noise amplitude is zero.

The fix must replace that pivot ownership. More damping alone would preserve the wrong heading and increase side-view lag.

## Selected architecture

Add one dedicated `RaceCameraTarget` per racer. It owns a parentless camera pivot and runs after `RaceSplineTapDriver` but before the Cinemachine Brain in `LateUpdate`.

Each frame:

1. Read the serialized horse camera anchor position and horse root yaw.
2. Place the pivot at the anchor without a second positional smoothing layer.
3. Smooth yaw over a short render-time interval.
4. Clamp yaw lag to eight degrees, preventing any sustained side view.
5. Apply yaw-only rotation with a world-up horizon; never inherit horse pitch or roll.
6. Let Cinemachine Third Person Follow perform final positional damping, framing, and obstacle handling.

The active Cinemachine camera tracks this pivot directly. Malbers `ThirdPersonFollowTarget` and `MInputLinkLook` remain disabled so they cannot overwrite pivot rotation or read pointer input.

## Camera presentation

- Use `CM Third Person Mount` as player one's live Cinemachine camera.
- Keep camera centered: `CameraSide = 0.5`, no horizontal shoulder offset.
- Start with `CameraDistance = 7.25 m`, `VerticalArmLength = 1.0 m`, and `FieldOfView = 45 degrees`.
- Use light Third Person Follow damping near `(0.08, 0.14, 0.08)` so the horse remains readable without a rigid camera feel.
- Keep collision avoidance enabled, with small non-zero damping into and out of collision to prevent camera pops.
- Keep Brain and target evaluation on the render clock (`LateUpdate`).
- Disable camera noise and external impulse response for the event race.
- Do not add motion blur, camera roll, sprint zoom, or FOV pumping.

All presentation values remain serialized and safe to tune in the Inspector after live testing.

## Split-screen compatibility

No singleton camera state, shared static pivot, or global camera lookup will be used. Each future racer receives:

- its own horse anchor;
- its own `RaceCameraTarget` pivot;
- its own Cinemachine camera and Brain/output channel;
- its own Unity Camera viewport rectangle.

Player-one behavior stays unchanged when player two is later added. Side-by-side framing can use a separate serialized distance/FOV preset without changing heading logic.

## Failure handling

- Missing horse anchor or Cinemachine camera produces a clear configuration error and disables only that camera target.
- Large heading discontinuities, teleports, or spline wrap snap safely behind the horse instead of orbiting across the scene.
- Component disable restores no global state because the rig owns only its own pivot and assigned camera.

## Verification

- EditMode tests verify heading smoothing never exceeds the eight-degree lag cap and snaps safely across large discontinuities.
- PlayMode tests load `Main` and verify the live camera uses the dedicated target, Malbers rotation/input ownership is disabled, Brain uses `LateUpdate`, camera noise and impulse response are zero, and framing matches the event preset.
- Existing input, coast, Sprint, animation, and camera tests remain green.
- Live Unity MCP testing drives Sprint through the spline's highest-curvature bend and records camera-to-horse yaw error. Expected maximum: eight degrees, with no side view or large catch-up rotation.
- Capture a moving turn screenshot and inspect horse centering, horizon stability, and background clarity before commit and push.
