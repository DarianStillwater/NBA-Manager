# KayKit — Character Pack: Adventurers (1.0)

**Origin:** https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0
(mirror of https://kaylousberg.itch.io/kaykit-adventurers) by Kay Lousberg.

**License:** Creative Commons Zero (CC0 1.0 Universal) — public domain, free for personal
and commercial use, no attribution required. Full text in `KAYKIT_LICENSE.txt`.

## What we vendored (and why)

Only the minimum needed for the 3D match view (P3 — characters & animation):

| File | Purpose |
|------|---------|
| `Rogue.fbx` | One rigged low-poly humanoid, reused for every player. Contains all 75 animation takes; imported as **Mecanim Humanoid** (forced by `Assets/Scripts/Editor/KayKitModelPostprocessor.cs`) so its clips retarget onto one shared AnimatorController. Prop meshes it ships with (`Rogue_Cape`, `Knife`, `Knife_Offhand`, `1H/2H_Crossbow`) are stripped when the player prefab is built — players hold/wear nothing. |
| `rogue_texture.png` | 1024×1024 gradient atlas. Used on the **head mesh only** (face + hair, mildly skin-tinted per player). The torso/arms/legs deliberately drop the atlas and use a flat URP material tinted by team color, because the atlas's green tunic washed out team identity. |

The reference `.glb` used to read the clip names was deleted after extraction — the FBX is the
retargetable source of truth. The other pack characters (Knight/Mage/Barbarian) and 25+ weapon
props were intentionally **not** vendored — one humanoid covers all players (jersey color + height
differentiate them), per the "don't hoard packs" guidance.

## Clip roles mapped by the animator builder

`Idle → Idle`, `Walk → Walking_A`, `Run → Running_A`, `Jump → Jump_Full_Long`,
`Throw → Throw`, `Stance → Blocking` (with fallbacks — see `CharacterAnimatorBuilder.cs`).
Basketball-specific motion (dribble/shoot) is layered procedurally from sim data
(`VerticalOffset`, `ActionPhase`, `SpeedFeetPerSec`) on top of these generic clips.
