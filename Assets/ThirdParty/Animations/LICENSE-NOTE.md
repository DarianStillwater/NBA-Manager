# ThirdParty/Animations — license note

Drop the basketball animation FBX clips here. `KayKitModelPostprocessor` auto-imports
everything in this folder as Humanoid (root motion locked into pose, `_Loop` files looped);
`CharacterAnimatorBuilder` (Tools ▸ NBA Head Coach ▸ Build Character Animator) wires them
into the controller by the canonical filenames below.

## Licensing

- **Mixamo** clips (Adobe): royalty-free for use in games, **but not redistributable** as
  standalone files. Fine to commit here **only because this repo is private** — do not open-source
  the repo or publish these FBX. Export as *FBX for Unity, without skin*.
- **Quaternius Universal Animation Library**: CC0 (public domain) — no restriction. Used for the
  Phase 5 sideline actors (refs / bench / crowd: strafes, jog, sprint, crouch, sit, cheer).

## Canonical filenames expected (AnimationClip name = filename)

Locomotion / dribble / defense:
`DribbleIdle_Loop.fbx`, `DribbleWalk_Loop.fbx`, `DribbleRun_Loop.fbx`, `DefensiveSlide_Loop.fbx`

Shots: `JumpShot.fbx`, `Fadeaway.fbx`, `Floater.fbx`, `Layup.fbx`, `LayupAcrobatic.fbx`,
`Dunk.fbx`, `Heave.fbx`

Ball actions: `ChestPass.fbx`, `BouncePass.fbx`, `Catch.fbx`, `Steal.fbx`, `Block.fbx`,
`Rebound.fbx`, `BoxOut_Loop.fbx`, `Screen_Loop.fbx`, `Celebrate.fbx`

Phase 5: `Sit_Loop.fbx`

Missing files are fine: the builder skips that state (or aliases a variant onto its base clip —
Fadeaway/Floater/Heave→JumpShot, LayupAcrobatic→Layup, BouncePass→ChestPass), and `CharacterBody`
falls back to the legacy Jump/Throw triggers for any absent state.
