# Event Coast, Sprint, and Chase Camera Design

## Goal

Make the preferred `Horse Realistic` spline racer feel natural and dependable at a public event: rapid keyboard tapping builds speed, stopping input produces a readable coast to rest, sustained top effort unlocks Sprint, and the camera stays calmly behind the rider.

## Controls and effort

- Keep the configurable keyboard-only tap bindings and ignore pointer input.
- Preserve the bounded rolling tap-effort model so frame rate and key-repeat cannot create unlimited speed.
- Keep gait hysteresis, adding Sprint as gait 5.
- Enter Sprint only at sustained effort `>= 0.92`; leave it below `0.86`. This prevents animation flicker near the threshold.

## Travel and stopping

- Use an explicit meters-per-second target per gait, including a capped Sprint target near `8.5 m/s`.
- Approach the target with rate-limited acceleration/deceleration (`MoveTowards` behavior), not a raw frame-dependent Lerp or physics momentum.
- On release, effort and requested gait step down while actual travel speed decelerates at a separate, configurable rate near `3 m/s²`.
- Do not enter Idle while the horse is still travelling. Select the closest locomotion gait for the remaining speed, then switch to Idle only at the exact stop threshold. This prevents visible sliding without leg animation.
- Clamp very small speed to zero so the horse cannot drift indefinitely.

## Sprint animation

- Re-enable Malbers Sprint support for the active mounted horse while the race driver owns it.
- Drive Malbers speed index 5 and its existing Sprint animation only when the effort threshold is crossed.
- Restore every temporarily overridden horse setting when the race driver is disabled.

## Camera

- Use `CM Third Person Mount` as the active event camera and retain `CM Third Person Main` as a matched fallback.
- Center the shoulder offset behind the rider, use a moderate chase distance around `6.5-7 m`, and retain modest damping.
- Disable manual look/orbit for the kiosk race so clicks or mouse movement cannot rotate or propel the experience.
- Keep Cinemachine noise and motion-blur-like shake disabled.
- Do not add sprint FOV effects in this pass; stable framing is preferable for an event display.

## Verification

- EditMode tests cover Sprint enter/exit hysteresis and frame-rate-independent acceleration/deceleration to an exact stop.
- PlayMode input simulation verifies repeated W taps reach Sprint, release coasts through animated locomotion, and the horse becomes stationary in Idle.
- Runtime checks verify the preferred horse is used, the mount camera is centered and locked behind it, and camera noise remains zero.
- Re-run all race EditMode and PlayMode tests before committing and pushing.

