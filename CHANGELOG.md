# Changelog

All notable changes to PCBS Multiplayer. Format loosely follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow SemVer with `-rc` tags for pre-release builds awaiting the closing manual gate.

## [Unreleased] — Plan 3 (Join Loop)

Implementation complete for tasks P3.B-T1 through P3.B-T10 + T7a. Closing gate M4b (two-machine playtest) pending peer arrangement; promote to `[0.3.0-alpha]` once M4b passes.

### Added

- **Save-bytes transfer** over Steam P2P. Host packs the selected `.binary` save into a `SaveTransferBegin` / N × `SaveChunk` (8 KB) / `SaveTransferEnd` stream. `SaveSyncPacker` handles the chunking + IEEE CRC32; `SaveSyncReassembler` handles ordered/out-of-order reception and CRC verification. Both are pure classes with xUnit coverage.
- **`HostSession.ClientAccepted`** event + **`BeginSaveTransfer(saveName, sceneName, savesDirAbsolute, out err)`** — called from `LobbyPanel.OnHostStartClicked` after the host picks a save. Per-transport `Send` failures are caught and logged so one dead peer doesn't abort the fan-out.
- **`ClientSession.SaveReady`** event + save-sync handlers. `OnSaveTransferEnd` writes the reassembled bytes to `<savesDir>/mp-<lobbyId>.binary` and raises `SaveReady`. On any failure, sends `Bye { Reason = "save_transfer_failed" | "save_write_failed" }` before tearing down.
- **`LobbyPanel.OnSaveReady`** — client's load trigger. Consumes a pending scene name stashed by `OnStartGameReceived`, clears it (prevents spurious repeat loads), and calls `TryLoadLocally("mp-<lobbyId>", scene)`.
- **Autosave suppression patch** — Harmony prefix on `SaveLoadSystem.AutoSave` returns `false` when `SessionManager.Current.IsLive && Role == Client`. Prevents PCBS's fixed-filename autosave (`auto.binary`, `autohard.binary`) from clobbering the client's real save during an MP session. Gate predicate is `public static` for xUnit coverage (3 gate tests + 1 not-live regression test).
- **Pre-join save backup** — `SessionLifecycle.OnLobbyJoined` copies every non-MP `*.binary` in `s_saveDir` into `<savesDir>/backup-mp-<lobbyId>/` before any save-sync wiring. Second safety layer behind the autosave patch.
- **`mp-*.binary` cleanup on `Stop()`** for client sessions, and **stale-file sweep on plugin `Awake`** that deletes `mp-*.binary` files AND `backup-mp-*/` directories older than 7 days. Best-effort — failures log and continue.
- **`InternalsVisibleTo`** for the test assembly (via new `AssemblyAttributes.cs`) so gate tests can set `SessionManager.IsLive`.

### Fixed

- **B1 — lobby rebroadcast timing.** `OnPeerJoined` was calling `LobbyPanel.RebroadcastState` at the Steam-lobby-join moment, before the handshake completed — the client's `LobbyState` handler wasn't wired yet. Now `HostSession` raises `ClientAccepted` from `OnHello` (post-handshake), and `SessionLifecycle` subscribes to that.
- **B2 — client load path.** `OnStartGameReceived` used to call `TryLoadLocally(s.SaveName, s.SceneName)` with the host's save filename, which doesn't exist on the client's disk → silent load failure. Now stashes the scene and shows "Receiving host's save data…"; the actual load fires from `SaveReady`.
- **B3 — client-side LobbyPanel hardcodes.** `RefreshPlayers` is now guarded against client-side invocation (was a latent landmine that would inject a hardcoded "Host (you)" slot-0 entry). `ShowForClient` shows a "Waiting for host's lobby state…" placeholder until the first broadcast lands. Host-side self-name fallback changed from "Host (you)" to "Host" so it composes cleanly with the separate "HOST" badge column.
- **Host broadcast fan-out resilience.** Per-transport `Send` failures in `BeginSaveTransfer` and `LobbyPanel.OnHostStartClicked`'s `StartGame` fan-out are now caught and logged so one disconnected peer doesn't abort the loop.
- **Event-subscription symmetry in `Stop()`.** Client branch now unsubscribes `mgr.Client.SaveReady -= LobbyPanel.OnSaveReady`, mirroring the existing `ClientAccepted` cleanup on the host branch.

### Known Limitations (carried to Plan 4+)

- **New-career path not supported.** `StartNewGame` performs no disk write before scene-load, so `BeginSaveTransfer` has nothing to read. Host must pick Continue (existing save). New-career MP deferred pending a save-on-demand hook.
- **No client-side transfer timeout.** If the host crashes mid-transfer, the "Receiving host's save data…" toast hangs forever. Workaround: F7-hold panic hotkey.
- **XP / inventory / shop / bench / time sync** not yet implemented (Plan 4).
- **Presence, chat, HUD connection panel, host-only save/load block** deferred to Plan 5.

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
