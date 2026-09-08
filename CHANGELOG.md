# Changelog

## v1.2.1 — 2026-09-07

### Name and identity

- Renamed the public mod from **Endurance Open Access** to **Endurance Mode: Open + Persistent**.
- Renamed the runtime namespace, entry type, DLL, and package filename to match the new branding.
- Retained stable manifest ID `cmz.endurance-open-access` so CMZ Mod Manager treats the renamed package as an update rather than a separate mod.
- Retained legacy incompatible predecessor ID `cmz.endurance-open-join`.
- Hardened build-time identity/version consistency so package metadata, assembly metadata, filenames, logs, and manifest values use one canonical release identity.

### Persistent Endurance behavior

Retains the v1.2.0 persistence pass:

- Normal Endurance executes the persistent-mode `ChunkCache.Flush(true)` save path instead of skipping the forced terrain flush.
- The active normal-Endurance world is protected from the automatic `EndGame()` deletion path.
- Manual world deletion is not intentionally disabled.
- Vanilla host/guest per-world `.inv` persistence is left intact rather than replaced with a custom persistence system.

### Existing features retained

- v1.0.0 late-join behavior remains intact.
- v1.1.0 vanilla saved-world / **Choose A Server** routing remains intact.
- Normal Endurance gameplay and difficulty remain unchanged.

### Runtime validation

- Builder completed successfully.
- CMZ Mod Manager accepted the generated v1.2.1 package.
- Host inventory persisted across a full game quit and rehost of the same Endurance world.
- Host teleporter access/state persisted across a full game quit and rehost.
- Returning unmodded guest persistence remains a dedicated multiplayer validation target.

## v1.2.0 — development lineage

- Added the persistent-world lifecycle pass after direct comparison of normal Endurance against Castle Miner Z's persistent game modes.
- Identified and addressed Endurance's save-time chunk-flush exception.
- Identified and addressed normal Endurance's automatic world deletion during `EndGame()`.

## v1.1.0

- Added vanilla saved-world selection for normal Endurance so existing worlds could be selected and rehosted.

## v1.0.0

- Removed normal Endurance's late-join lock by patching the existing Endurance session gate without changing Endurance gameplay rules.
