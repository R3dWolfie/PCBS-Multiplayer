# Changelog

All notable changes to PCBS Multiplayer. Format loosely follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow SemVer with `-rc` tags for pre-release builds awaiting the closing manual gate.

## [0.2.0-rc1] — 2026-04-20

Plan 2 (Game Bridge) implementation complete through M2 smoke. Ships the Harmony patch surface, real Steam P2P transport, and the host/client lobby flow. **Not yet validated on two machines** — M3 (T27) pending remote peer availability. Promote to `0.2.0` after M3 passes.

### Added

- **Harmony patches** for `CareerStatus.SpendCash` / `AddCash` (money deltas) and `JobBoardState.AddJob` / `OnAccept` / `OnCollected` / `OnQuit` / `OnDiscard` (job-lifecycle deltas). All patches early-return on `SessionManager.Current == null` — zero effect on vanilla single-player.
- **Steam lobby** via `SteamLobby` — `CreateLobby` / `JoinLobby` / `ActivateGameOverlayInviteDialog`. Friends-only lobby type, 4-member cap, version stamped in lobby metadata.
- **Real `SteamTransport`** — P2P over `SteamNetworking.SendP2PPacket` / `ReadP2PPacket`, pumped once per `Update`.
- **`SessionLifecycle`** façade — `StartHost` (idempotent, re-opens invite overlay on re-click), `JoinPendingInvite`, `Stop`. Handles lobby-chat `k_EChatMemberStateChangeEntered` to attach peer transports.
- **`MainMenuPatcher`** — `Postfix` on `MainMenu.Start`; adds top-center OnGUI "Host Multiplayer" / "Join Multiplayer" buttons (420×70, 28pt bold).
- **Panic hotkey** — `PanicHotkeyTracker` (pure class, 4 xUnit tests) + `PanicHotkeyHandler` MonoBehaviour. Hold **F7 for 3s** → disconnects transport, writes `EnableMultiplayer = false` to config, persists. Top-right yellow-on-black status pill.
- **`scripts/deploy-to-game.sh`** — builds Release and copies DLL (+ `System.ValueTuple.dll` if present) to `$PCBS/BepInEx/plugins/`.
- **Mod docs** — `docs/pcbs-recon.md` (T1 recon gate), `docs/manual-tests.md` (T10→T26 playtest log).

### Changed

- **Plugin version** bumped to `0.2.0-rc1`.
- **Plugin `Update`** now calls `SteamAPI.RunCallbacks`, pumps all `SteamTransport` instances, and calls `SessionManager.Tick` + `Heartbeat`.
- **`HostSession`** exposes `Transports` and `BroadcastJobBoardDeltaPublic` for use by patch classes.
- **`ClientSession`** applies inbound `MoneyChanged` deltas to `CareerStatus` via `AddCash`/`SpendCash`, reentrancy-guarded by `SessionManager.ApplyingRemoteDelta`.

### Fixed

- **Mono 2018.4 type-load blockers** (T10b). Unity 2018.4's Mono refuses to bind fields typed as non-generic `Action`, `Action<T1,T2>+`, `Func<T1,T2>+`, or `ValueTuple<,>+` at class-load, and its compiler-generated `<>c` siblings fail with them — causing HarmonyX `AccessTools.GetTypesFromAssembly` to silently drop the whole assembly. Refactored `Session/*.cs` to use custom delegates and eliminate stateless-lambda caches.
- **MainMenu button injection** — UGUI clone approach was clipped by PCBS's menu `VerticalLayoutGroup + ContentSizeFitter`. Replaced with pure IMGUI fallback.
- **`StartHost` was fire-once** — subsequent clicks silently no-op'd. Now re-opens invite overlay when already hosting.

### Known Limitations (carried to Plan 3)

- **Client job-board UI not mirrored.** `WorldState.JobBoard` syncs but the client's in-game Career job list renders its local pool. No double-claim / money is authoritative (safe invariant) but the UX of "see which job your friend took" does not work yet.
- **No lobby screen.** Hosting opens the Steam invite overlay directly; saves are picked via the vanilla main menu after closing the overlay. Plan 3 replaces this with an in-game lobby panel + save selection.
- **No save synchronization.** Host and peer must independently pick the *same* savefile (or a compatible `[MP]` save) from the main menu. Plan 3 adds host-broadcast `StartGame { saveId }` handshake.

## [0.1.0] — 2026-04-20

Plan 1 (Netcode Foundation) — headless in-memory netcode, 42 xUnit tests green. No Harmony patches, no Steam wiring. Internal milestone only; not released.
