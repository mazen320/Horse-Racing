# Tripo Jockey Malbers Rider Design

## Goal

Replace the visible Malbers cowboy on the `Rider` paired with `RaceSetup/Horse Realistic` in `Assets/Scenes/Main.unity` with `Assets/TripoModels/jockey_3d_model/jockey_3d_model.fbx`, while retaining Malbers mounting, riding animation, rein-hand IK, and the existing race controller integration.

## Chosen approach

Keep the scene's `Rider` GameObject and all gameplay components (`Animator`, `MRider`, `IKManager`, physics, input, and Malbers controller). Keep its original Malbers Animator and hidden humanoid rig as the gameplay authority, then add the Tripo FBX as the visible `JockeyVisual` with its native humanoid Avatar. A `RiderAnimatorSynchronizer` mirrors the Malbers parameters, layer weights, and active states to the visual Animator and bridges mounted foot/rein IK.

This is safer than replacing the full Rider prefab because the horse mount, race script, and controller already reference the existing Malbers Rider root. A single-Animator retarget was tested first, but the Tripo import axes caused the skeleton to flatten under the Malbers root Animator. Keeping the source Animator authoritative while driving the Tripo Avatar separately preserves Malbers behavior and correct humanoid orientation.

## Scene structure

- Keep: `RaceSetup/Rider` and all nonvisual Malbers children/components.
- Disable the old cowboy renderer and skeleton at the scene-instance level rather than changing the source Malbers prefab.
- Add a scene-local `JockeyVisual` child created from the Tripo FBX.
- The root `RaceSetup/Rider` Animator keeps `AC Human v5 Rider.controller` and the original Malbers Avatar.
- The `JockeyVisual` Animator uses the same controller with the Tripo Avatar, no root motion, Normal update mode, and Always Animate culling.
- `RiderAnimatorSynchronizer` copies non-curve parameters and mounted layer states from the root Animator to the visual Animator.
- `MRider.LeftHand` and `MRider.RightHand` reference the Tripo Avatar's humanoid hand transforms; the synchronizer applies rein and foot IK to the visual Animator.

## Fit and animation behavior

- Use the mounted pose as the fitting reference.
- Apply only a uniform scale and root local offset; do not distort limb proportions.
- Keep root motion ownership and race spline movement unchanged.
- Validate idle mounted, walk/trot/canter/sprint transitions and both rein hands.
- Confirm the jockey stays seated and feet remain near the stirrup area during locomotion.

## Safety

- Do not edit the source `Cowboy (Mobile).prefab`.
- Do not edit the source Tripo FBX geometry or textures.
- Do not modify the Arabian jockey test prefabs/materials.
- Preserve all unrelated workspace changes.

## Acceptance criteria

1. `Main.unity` contains exactly one active visible rider model for `Horse Realistic`, and it is the Tripo jockey.
2. The existing Malbers Rider root and controller remain in use.
3. The root Animator retains the Malbers Avatar and the visual Animator has the valid humanoid Tripo Avatar.
4. `MRider` hand references resolve to the Tripo humanoid hands.
5. Mounted locomotion states match between the authoritative and visual Animators with no stationary mesh or duplicate cowboy.
6. Unity reports no new compile/runtime errors in the focused test and Play Mode verification.
