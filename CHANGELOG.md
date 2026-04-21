# Changelog

All notable changes to PCBS Multiplayer. Format loosely follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow SemVer with `-rc` tags for pre-release builds awaiting the closing manual gate.

## [0.3.0-alpha-preview2] — 2026-04-21

Hotfix respin of `0.3.0-alpha-preview`. The preview build shipped with two bugs that only surfaced once a host + client actually tried to exchange frames on two real Steam peers; surfaced during first live test with a friend. No feature changes.

### Fixed — release-blocking

- **`MessageRouter.Dispatch` crashed on every received frame** with `TypeLoadException: Could not load type 'System.ValueTuple\`2'` under Unity 2018.4's Mono. `Serializer.Unpack` returned a C# tuple and `Dispatch` deconstructed it; the resulting IL references `ValueTuple<,>` which Mono 2018 refuses to JIT even with `System.ValueTuple.dll` shipped alongside. Net effect: no peer-to-peer message dispatched in either direction — client's `LobbyState` handler never fired (panel stuck at "Waiting for host's lobby state…"), host's `OnHello` never fired (friend never appeared in host's player list), and `SessionManager.Tick` spammed the log every frame until the game froze. Added an out-param `Unpack(frame, out tag)` overload used by `Dispatch`; the existing tuple-returning overload is retained for xUnit test call-sites (which run on desktop .NET where tuples are fine). This error had been latent since Plan 2 — `0.2.0-rc1` never loaded, so nobody hit it until the `0.3.0-alpha-preview` build actually executed a real peer exchange.
- **`SteamLobby` spun up a phantom client session on the host** after any prior Join had registered the `LobbyEnter_t` callback. Steam fires `LobbyEnter_t` on lobby *creators* too (the creator is auto-joined), so once `_onEnter` was registered, every subsequent `CreateLobby` re-triggered `OnLobbyEnter` on the host, which in turn invoked the stale `_onJoined` handler and created a `ClientSession` pointed at the user's own `SteamID`. Symptoms: `"Client session started; host <your own steamID>"` log line firing right after every `Host session started`, plus the phantom client's Tick compounding the Bug 1 crash above on every frame. Fixed: `OnLobbyEnter` early-returns when `IsHost && LobbyId == ev.m_ulSteamIDLobby` (we created this very lobby), and `JoinLobby` resets `IsHost = false` so the guard can't misfire against a fresh incoming join.

## [0.3.0-alpha-preview] — 2026-04-21 (withdrawn — see preview2)

> **Do not install `0.3.0-alpha-preview`.** Peer comms are dead — the message dispatcher throws `TypeLoadException` on every received frame under Mono 2018. `0.3.0-alpha-preview2` is the same feature set with the dispatcher rewritten to not touch `ValueTuple<,>`.

Pre-release cut of the Plan 3 join-loop work for early peer-pairing tests. Not yet gated on M4b (two-machine closing validation), so expect rough edges. Published specifically so a host + friend can install matching binaries and exercise the save-sync path together; promote to `0.3.0-alpha` once M4b passes.

### Fixed — release-blocking

- **Plugin version string** — `PluginVersion` is now `"0.3.0.0"` (`System.Version`-parseable) with pre-release info carried as a separate `DisplayVersion = "0.3.0-alpha-preview"` constant used in log/UI strings. BepInEx 5.x's `TryParseLongVersion` rejects anything with a `-suffix` under `[BepInPlugin(…)]`, silently dropping the plugin with `[Warning: BepInEx] Skipping type ... because its version is invalid.` — this was the root cause of the `0.2.0-rc1` installation failing for downstream users. Fresh installs should also delete `BepInEx/cache/chainloader_typeloader.dat` once so BepInEx re-scans instead of remembering any prior "version is invalid" verdict cached from `rc1`.

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
