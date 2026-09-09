# Castle Miner Z — Endurance Mode: Open + Persistent

A host-side mod for **Castle Miner Z 1.9.9.8** that makes normal Endurance behave like a persistent, rehostable world mode without changing Endurance gameplay itself.

> **Unofficial community project:** Endurance Mode: Open + Persistent is independently created and published by AnlionGamer. It is not an official Castle Miner Z release and is not affiliated with, sponsored by, approved by, or endorsed by the game's developers or publisher.

**Publisher:** AnlionGamer  
**Current release:** v1.2.1  
**Stable Mod ID:** `cmz.endurance-open-access`

## What it does

Vanilla normal Endurance contains several lifecycle assumptions that treat a session as disposable. This mod removes those session limitations while leaving Endurance rules and difficulty intact.

- Keeps normal Endurance open to late joiners beyond the vanilla Endurance join cutoff.
- Exposes Castle Miner Z's vanilla saved-world / **Choose A Server** flow for normal Endurance.
- Lets an Endurance world be closed and later rehosted instead of treating the session as the lifetime of the world.
- Makes normal Endurance use the same blocking terrain/chunk flush path used by persistent game modes when saving.
- Prevents `EndGame()` from automatically deleting the active normal-Endurance world.
- Preserves Castle Miner Z's existing per-world player inventory (`.inv`) system instead of introducing a custom save format.
- Leaves normal Endurance gameplay mechanics, difficulty, progression rules, and enemy behavior unchanged.
- Does not intentionally alter Dragon Endurance or the other game modes.

## Returning players and vanilla clients

The mod is designed to be **host-side**. Joining players do not need to install it simply to join the hosted Endurance session.

Castle Miner Z already has a vanilla per-world host/guest inventory protocol. This mod deliberately leaves that system intact so returning players can use the game's normal player-state retrieval/storage path when the same world is rehosted.

Runtime verification currently confirms the host's inventory and teleporter state survive quitting Castle Miner Z and rehosting the same Endurance world. Returning **unmodded guest-player persistence** is supported by the inspected vanilla path but still requires dedicated multiplayer runtime verification before it should be described as fully proven.

## Requirements

- Castle Miner Z **1.9.9.8** (Steam)
- CMZ Mod Manager / Mod Framework **1.0.0 or newer**
- CMZ Mod API **1.0.0**
- Windows / x86 game process

## Installation

1. Download `CMZ_Endurance_Mode_Open_Persistent_v1.2.1.cmzmod` from the GitHub Releases page.
2. Install the `.cmzmod` through CMZ Mod Manager.
3. Enable **Endurance Mode: Open + Persistent** for the host's active profile.
4. Launch Castle Miner Z through the Mod Manager.
5. Use **Host Online → Endurance** and select or create a world through the exposed vanilla world chooser.

## Current verification status

| Area | Status |
| --- | --- |
| Builder/package accepted by CMZ Mod Manager | Verified |
| v1.0 late-join behavior | Previously runtime verified |
| Saved-world chooser / rehost route | Working in current use |
| Host inventory after full quit + rehost | Runtime verified |
| Host teleporter access/state after full quit + rehost | Runtime verified |
| Persistent-mode terrain flush patch | Code/IL verified; exercised by host persistence test |
| EndGame automatic-world-deletion guard | Code/IL verified; world survives rehosting |
| Returning vanilla guest inventory/teleporter persistence | Vanilla path inspected; multiplayer runtime test pending |
| Explicit manual Delete World regression | Dedicated runtime test pending |
| Abnormal guest-disconnect persistence | Dedicated runtime test pending |

Back up valuable worlds before testing new mod versions. A world whose player `.inv` data was already lost or mismatched before the persistence fix may contain orphaned placed objects that cannot be reconstructed safely from terrain alone.

## Compatibility identity

The display name changed from the earlier **Endurance Open Access** branding, but the stable manifest ID intentionally remains:

`cmz.endurance-open-access`

CMZ Mod Manager uses the manifest ID as the canonical install/update identity. Keeping it stable lets v1.2.1 update the earlier mod instead of installing as a separate unrelated package.

The legacy incompatible predecessor ID `cmz.endurance-open-join` is also intentionally retained in the package incompatibility metadata.

## Source

The repository contains the exact v1.2.1 runtime source, the generated v1.2.1 identity source needed by that runtime source, and a reference copy of the shipped v1.2.1 manifest.

**Development/builders are intentionally not published in this public repository.** They are not required to install, use, or review the finished mod.

## License and attribution

The current repository `main` branch and future Endurance Mode: Open + Persistent work are governed by the **AnlionGamer Community Distribution Terms v1.0**. See [`LICENSE`](LICENSE).

The terms allow normal use, source inspection, and private modification. Public redistribution of the original project, source, packaged mod, forks, or modified builds requires **prior permission from AnlionGamer** and must remain **non-commercial**. Sale and paid access are prohibited without separate permission.

The released v1.2.1 package already states the same core distribution condition: non-commercial redistribution only with author permission. Copies previously distributed under documented terms retain the permissions that accompanied those copies.

Castle Miner Z and its original game material remain the property of their respective rights holders. See [`NOTICE.md`](NOTICE.md).
