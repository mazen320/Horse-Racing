# Render-Smooth Horse and Sprint Tuning Design

## Goal

Remove the vibration/distortion visible on the preferred realistic horse and rider while preserving the responsive tap-to-run event gameplay, then provide a slightly faster but bounded Sprint setting.

## Diagnosis

- URP motion blur is effectively off and the active camera uses SMAA, not temporal antialiasing, so post-processing is not producing the reported distortion.
- The preferred horse Animator, mounted rider Animator, and Cinemachine Brain currently update on the fixed physics clock.
- `RaceSplineTapDriver` moves the horse along the spline every rendered-frame `LateUpdate`, and its current execution order is after Cinemachine. At display rates above the fixed timestep, bones and camera repeat old poses while the root advances, which presents as vibration, smearing, or distortion.
- The Malbers Sprint speed modifier already drives horse and rider animation at approximately `1.1x`, so a modest travel increase can stay visually matched without editing vendor controllers.

## Considered approaches

1. **Render-clock alignment (selected):** run both locomotion Animators and Cinemachine in the normal/LateUpdate render clock, and execute spline pose ownership before camera evaluation. This directly removes the mismatched clocks and retains current responsiveness.
2. **Raise the global physics rate:** move the fixed timestep toward the display rate. This increases CPU cost for the entire event build and still cannot guarantee a match on high-refresh displays.
3. **Mask the symptom:** lower Sprint speed, add more camera damping, or disable antialiasing. This would make the game less energetic and does not address stale animation/camera samples.

## Runtime design

- Change `RaceSplineTapDriver` execution order from `100` to `-100`. Its `Update` then selects the gait before Malbers (`MAnimal` is `-10`), while its `LateUpdate` applies the spline pose before the rider link and Cinemachine Brain.
- Capture the horse Animator update mode, set it to `AnimatorUpdateMode.Normal` while the race driver owns movement, and restore it when disabled.
- The mounted rider already copies the horse Animator update mode through Malbers `MRider.End_Mounting`, so it will use the same render clock. A PlayMode regression test will verify both rigs rather than relying on that assumption.
- Configure the scene Cinemachine Brain to `LateUpdate`, matching the spline target.
- Keep camera noise at zero and existing centered camera composition unchanged.

## Sprint tuning

- Raise the event scene Sprint target from `8.5 m/s` to `9.25 m/s`.
- Mark `8.5–10.5 m/s` as the recommended Inspector range and clamp the field to a hard `10.5 m/s` presentation ceiling in `OnValidate`.
- Retain the Malbers Sprint animation modifier (`~1.1x`) and do not globally scale the Animator. At `9.25 m/s`, the travel increase from the prior baseline is approximately `1.088x`, closely matching that existing modifier.
- Keep the current acceleration, coasting, gait thresholds, and input bindings.

## Verification

- A PlayMode regression test loads `Main` and verifies the horse Animator, mounted rider Animator, and Cinemachine Brain all use the render clock.
- Existing W-spam coverage must still reach Sprint with locomotion animation, coast after release, and stop exactly.
- EditMode coverage verifies excessive Sprint values clamp to the presentation ceiling.
- Fresh full EditMode and PlayMode suites, Console inspection, a runtime screenshot, and repository diff checks gate commit and push.

