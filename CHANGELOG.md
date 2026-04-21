# Changelog

All notable changes to PCBS Multiplayer. Format loosely follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow SemVer with `-rc` tags for pre-release builds awaiting the closing manual gate.

## [0.3.0-alpha-preview13] — 2026-04-21

Twelfth hotfix respin. Two-machine play confirmed preview12's money fix worked — client purchases now correctly debit the host's real cash. Same design flaw still sat on the job-claim path: `HostSession.OnClaimJob` arbitrated against the unseeded `WorldState.JobBoard` mirror, so client-side Accept on any job silently failed with `already_claimed_or_missing`. Additionally, even if the host did accept a claim, clients had no mechanism to flip the job's local `Job.m_status` from NEW to ACCEPTED — PCBS's job-board UI on the client would still show the job as available.

### Fixed — release-blocking

- **`HostSession.OnClaimJob` now arbitrates against `CareerStatus` via the `ClaimAuthority` delegate seam (same pattern as preview12's `SpendAuthority`).** `TryClaimViaCareerStatus` parses the jobId to int, looks up the real PCBS `Job` via `CareerStatus.Get().GetJob(id)`, verifies `m_status` is `NEW` or `READ`, and calls `job.OnAccept(_autoAccepting: true)` with `SessionManager.ApplyingRemoteDelta = true` to suppress the re-entrant `OnAcceptPatch.Postfix` broadcast (which would read the empty mirror).
- **`HostSession.BroadcastJobBoardDelta` now rebuilds the delta from `CareerStatus` via the `JobDeltaFill` delegate seam.** PCBS's `CareerStatus.m_jobs` is the source of truth for Job status; per-slot attribution lives in a new `HostSession._jobClaimedBySlot` side-map populated on successful `OnClaimJob`. `JobDeltaFill` walks `GetJobs()` + `GetDoneJobs()`, slots each into Available / Claimed / Completed based on `Job.Status`, and merges the slot map. Tests fall through to the old mirror path via the no-op default delegate.
- **`ClientSession.OnJobBoardDelta` now also applies claimed entries to the client's own `CareerStatus` via the `ApplyJobDelta` delegate seam.** For each claimed `JobDto`, the client looks up its local `Job` by id, and if `m_status` is still NEW/READ, calls `job.OnAccept(_autoAccepting: true)` with `ApplyingRemoteDelta = true`. PCBS's local job-board UI now moves the claimed job from "available" to "in progress" the moment the host approves.

### Known-good assumptions

- All three new authorities (`ClaimAuthority`, `JobDeltaFill`, `ApplyJobDelta`) default to no-op implementations so xUnit tests — which don't ship Assembly-CSharp.dll — don't fail at JIT prep. `PCBSMultiplayerPlugin.Awake` swaps all three to their CareerStatus-backed impls in prod. All 79 xUnit tests still green.
- Quit/Release and Complete/Collect paths remain host-side-only (mirror-driven, same pre-preview13 behavior). If a client quits a claimed job, their local PCBS rolls back but the host and other clients don't see it. Scoped for a follow-up preview; claim-and-hold is the alpha-gate path.

## [0.3.0-alpha-preview12] — 2026-04-21 (superseded by preview13)

Eleventh hotfix respin. Two-machine play confirmed preview11's Welcome-send fix landed — client reached `OnWelcome: IsLive=true, slot=1`, and subsequent broadcasts flowed. But purchases by either player left both balances unchanged, and the host log showed `[Info :HostSession] BroadcastMoneyChanged: total=1717` (host spending its own cash, fine) with no accompanying client-spend handling. Root cause: `HostSession.OnSpendMoney` arbitrated against `_mgr.World.Money`, a mirror that's only written by `AddCashPatch`/`SpendCashPatch` postfixes — it's **never seeded from the loaded save**, so it reads 0 at session start and rejects every client purchase with `insufficient_funds`.

### Fixed — release-blocking

- **`HostSession.OnSpendMoney` now arbitrates against `CareerStatus` (PCBS's authoritative money state) instead of the unseeded `WorldState.Money` mirror.** Spent amount is debited via `career.SpendCash(amount, true)`, which triggers `SpendCashPatch.Postfix → BroadcastMoneyChanged` to push the new total to all clients. The `CareerStatus` reference lives behind a custom delegate seam (`HostSession.SpendAuthority`) so xUnit tests — which don't ship `Assembly-CSharp.dll` — don't fail at JIT prep time. The default delegate target is a no-op that returns `Usable=false`, causing `OnSpendMoney` to fall through to a mirror-path branch that keeps tests deterministic. `PCBSMultiplayerPlugin.Awake` swaps the delegate to `HostSession.TrySpendViaCareerStatus` once BepInEx has resolved Assembly-CSharp.

### Known-good assumptions

- Same design flaw exists on `HostSession.OnClaimJob` (arbitrates on `WorldState.JobBoard`, also unseeded) — client job claims will still silently fail after preview12. Scoped for a follow-up preview; money is the user-visible path exercised during play.
- All 79 xUnit tests still green with the delegate seam default.

## [0.3.0-alpha-preview11] — 2026-04-21 (superseded by preview12)

Tenth hotfix respin. `preview10`'s router-catch widening paid off: the client log was still unhelpful, but the **host** log surfaced the real bug — `Handler threw: Could not load type 'System.Func\`2' ... at HostSession.OnHello`. `SnapshotBuilder.Serialize` used LINQ (`state.JobBoard.Available.Select(Dto)`), which forced Mono 2018 to JIT a method referencing `Func<T,TResult>` (`Func\`2`), a type the trimmed mscorlib 4.0.0.0 shipped with PCBS cannot load. `OnHello` threw *after* `_transports[slot] = transport` but *before* `transport.Send(new Welcome{…})`, so the client's transport was registered for every subsequent broadcast (StartGame, save chunks, MoneyChanged) but never received Welcome — IsLive stuck False, client's spend/claim actions routed locally instead of through the host.

### Fixed — release-blocking

- **Host's Welcome send was blocked by a Mono 2018 `Func\`2` type-load failure deep inside `SnapshotBuilder.Serialize`.** Rewrote `SnapshotBuilder.Serialize` with plain `foreach` loops over `List<Job>` / `Dictionary<,>.ValueCollection` — no LINQ, no `Func\`2` references anywhere. Also removed the now-unused `using System.Linq;` from `JobBoardState.cs`. Verified: 79/79 xUnit tests still pass on desktop .NET; Mono 2018 will no longer refuse to JIT the method.

### Known-good assumptions

- `MessageRouter`'s widened catch (added in preview10) stays — it's what made this diagnosable at all. Any future silently-swallowed handler exception will now print its type and stack trace.
- Diagnostic log lines added in preview9/10 (`OnMoneyChanged`, `SpendCash postfix`, `OnAccept postfix`) remain in preview11 so the first post-fix playthrough can confirm `IsLive=True` on the client after `OnWelcome` runs.

## [0.3.0-alpha-preview10] — 2026-04-21 (superseded by preview11)

Ninth hotfix respin. `preview9`'s diagnostic logs proved the client was receiving host MoneyChanged frames fine but sitting at `IsLive=False` — every client-side spend/claim action ran locally instead of routing to the host. Hypothesis at the time: `OnWelcome` was throwing silently inside the snapshot-apply block, so `IsLive = true` (the last line of the handler) never executed.

### Changed

- `ClientSession.OnWelcome` now flips `IsLive = true` *before* applying the snapshot, and wraps the snapshot-apply block in try/catch so a failure can't block the flip. (Turned out to be the wrong layer — see preview11 for the actual root cause.)
- `MessageRouter.Dispatch` widened its catch: previously it only caught `NotSupportedException`, `EndOfStreamException`, `IOException` — everything else propagated and was silently lost. Now a generic `catch (Exception ex)` logs the message and stack trace, which is what ultimately made preview11 diagnosable.

## [0.3.0-alpha-preview9] — 2026-04-21 (superseded by preview10)

Eighth hotfix respin. `preview8` got both players into the same save, but Plan 2 state sync (money + jobs) didn't propagate between host and client.

### Changed

- **Diagnostic build only, no functional changes.** Added `Log.LogInfo` at every hop in the money + job sync chains: `MoneyPatches.SpendCashPatch.Postfix`, `AddCashPatch.Postfix`, `JobPatches.AddJobPatch/OnAcceptPatch/OnCollectedPatch/OnQuitPatch.Postfix`, `HostSession.BroadcastMoneyChanged`, `HostSession.BroadcastJobBoardDelta`, `ClientSession.OnMoneyChanged`, and the inline `JobBoardDelta` handler lambda. Every hop logs the current `SessionManager.Role` and `IsLive` state so we can pinpoint which gate is silently dropping the signal.

## [0.3.0-alpha-preview8] — 2026-04-21

Seventh hotfix respin. `preview7` fixed the ordering so `StartGame` arrived before the save bytes — two-machine testing confirmed the client now receives `StartGame`, the save bytes stream in, the `mp-<lobbyId>.binary` file is written to the client's `Saves/<steamid>/` directory, and the client fires `SaveReady`. But PCBS then popped a "Save Game Error" modal: `"Sorry, something went wrong loading the save game 'Saves/76561198117103558/mp-109775242619721885'."` — note the **missing `.binary` extension** in the path. Root cause: the client passed the save name *without* the `.binary` suffix to `LoadGameFromDir`, while the host passes `"auto.binary"` (with suffix). PCBS's loader didn't auto-append the extension, so it looked for `mp-<lobbyId>` (no extension), which doesn't exist on disk.

### Fixed — release-blocking

- **Client's `LoadGameFromDir` call used the wrong filename.** `ClientSession.OnSaveTransferEnd` constructed `saveName = "mp-" + _lobbyId` (no extension), wrote the file as `saveName + ".binary"` (so the on-disk name was correct), then fired `SaveReady(saveName)` — which propagated the *extension-less* name to `LobbyPanel.OnSaveReady → TryLoadLocally → llp.LoadGameFromDir(saveName, scene)`. Host-side `LoadGameFromDir` is always called with the full `"auto.binary"` filename from the save list and works fine; the asymmetry meant only the client hit the bug. Fix: `saveName` now includes `.binary` end-to-end on the client, and the `SaveReady` contract is documented as "save filename including `.binary`, matching host's `LoadGameFromDir` convention."

## [0.3.0-alpha-preview7] — 2026-04-21 (superseded by preview8)

Sixth hotfix respin. `preview6`'s ready/connecting UI worked as designed — the host could see peers arrive and flip to ready — but two latent bugs in the actual save-transfer flow prevented the client from ever loading into the shared scene. Both were inherited from preview5; preview6 just made them visible by getting the handshake to complete reliably.

### Fixed — release-blocking

- **Client received save bytes but dropped them because `StartGame` arrived last.** Host was sending `SaveTransferBegin → SaveChunk… → SaveTransferEnd → StartGame` — in that order. Client's `OnSaveTransferEnd` fires the `SaveReady` event, which calls `LobbyPanel.OnSaveReady` — which reads `_pendingSceneName`. But `_pendingSceneName` is populated from the `StartGame` message, which hadn't arrived yet. Result: `SaveReady but no pending scene; ignoring` in the client log, save bytes sitting on disk unused, client stuck at "Receiving host's save data…" while the host entered the scene alone. Fix: `LobbyPanel.OnHostStartClicked` now sends `StartGame` **before** calling `BeginSaveTransfer`, so `_pendingSceneName` is set before the transfer-end trigger fires. Order is now `StartGame → SaveTransferBegin → SaveChunk… → SaveTransferEnd`, and the client's load path activates on `SaveReady` with the scene name already cached.

### Added — diagnostics

- **Gate refusal now logs `Host.Clients` contents.** When `OnHostStartClicked` refuses with "N peer(s) not ready", the log line now includes the full `HostSession._clients` slot→steamid map plus the `_players` count, so a mismatch between Steam lobby membership and handshaked clients is obvious at a glance. (Preview6 log showed a gate refusal immediately after `OnHello` had logged — source unknown without this diagnostic.)

## [0.3.0-alpha-preview6] — 2026-04-21 (superseded by preview7)

Fifth hotfix respin. `preview5` closed the P2P accept race so the Hello handshake works reliably, but two-machine testing surfaced a different failure: the host clicked "Start Game" before the client's Hello arrived, so `BeginSaveTransfer` iterated an empty `HostSession.Transports` dict and silently sent the save bytes to no one. The client was stuck forever on "Waiting for host's lobby state…" because it never received `Welcome` or `LobbyState`. Compounding the problem, the host's player list only showed peers *after* they'd handshaked — so the host saw an empty lobby and had no visual cue that a friend had joined but wasn't ready yet.

### Fixed — release-blocking

- **Host could click Start before the client's Hello arrived, sending the save bytes to nobody.** Root cause: `LobbyPanel.OnHostStartClicked` called `HostSession.BeginSaveTransfer` unconditionally, and `BeginSaveTransfer` broadcasts to `_transports` (populated only in `OnHello`). If the host clicked Start in the gap between `LobbyChatUpdate_t` (peer joined the Steam lobby) and `OnHello` (peer completed the mod handshake), `_transports` was empty and the save transfer was a no-op. Host then loaded its save locally and entered the scene alone. Fix: `OnHostStartClicked` now refreshes the player list, counts non-host players without `IsReady=true`, and refuses to start with a clear `Still waiting for <name> to finish joining` message. Gate clears automatically once `OnHello` fires and the periodic refresh marks the peer ready.
- **Host's lobby panel didn't show peers who'd joined the Steam lobby but hadn't handshaked yet.** `RefreshPlayers` only enumerated `HostSession.Clients`, which is populated in `OnHello`. So between lobby-join and Hello, the host's player list looked empty — giving the impression the friend hadn't joined at all. Fix: `RefreshPlayers` now enumerates `SteamMatchmaking.GetLobbyMemberByIndex` to catch all lobby members, cross-references `HostSession.Clients` for handshake state, and marks each peer `IsReady=true|false` on the `LobbyPlayer` struct. Connecting peers now render as `GUEST  <name>  ◌ connecting…` in red; ready peers render as `GUEST  <name>  ● ready` in green.

### Added

- **`LobbyPlayer.IsReady`** wire-format field — handshake state propagates to clients so they'll see the same ready/connecting indicators when there are 3+ peers. (Wire change is compatible in-version only — preview6 ↔ preview6.)
- **Periodic `RefreshPlayers` refresh** from `LobbyPanel.Update` at 2 Hz. Host UI now reflects peer joins, leaves, and handshake completions in real time without requiring any clicks.

## [0.3.0-alpha-preview5] — 2026-04-21 (superseded by preview6)

Fourth hotfix respin. `preview4` fixed the pump deadlock but pairing still failed intermittently because the `P2PSessionRequest_t` callback was registered too late — inside the per-peer `SteamTransport` constructor, which runs after `OnPeerJoined`. The client's Hello can arrive at the host *before* that callback exists, and Steam drops the packet when no handler is registered.

### Fixed — release-blocking

- **Race: host dropped client's first packet when request handler wasn't registered yet.** On the host, `LobbyChatUpdate_t` (which triggers `OnPeerJoined` → constructs `SteamTransport` → registers `Callback<P2PSessionRequest_t>`) can fire *after* the client's first `SendP2PPacket`. If no handler is registered at the moment the request posts, Steam drops the packet and never retries — the host never sees the Hello, so `OnHello` never fires and the lobby stalls. Fix: register a plugin-scope `Callback<P2PSessionRequest_t>` handler in `Awake` immediately after `SteamAPI.Init`, auto-accepting any incoming request. This closes the race window entirely — the handler is live before any lobby activity is possible.

### Added — diagnostics

- **`OnHello` entry log** on host: `OnHello: from <name> (steam=..., mod=...)`. Narrows future stalls to either "Hello never arrived" or "Hello arrived but broadcast failed".
- **`P2P session auto-accepted from <id>`** log at the new plugin-scope handler, proving Steam delivered the first packet.

## [0.3.0-alpha-preview4] — 2026-04-21 (superseded by preview5)

Third hotfix respin. `preview3` added the host-side P2P-accept call and the auto-join UX, but pairing still deadlocked because the Update loop's pump list was wrong — the client's Hello packet sat unread in Steam's buffer forever. Also fixes a save-path bug (double `.binary` extension) that would have surfaced the instant pairing worked.

### Fixed — release-blocking

- **Host never pumped pre-handshake client transports — Hello packet lost.** `PCBSMultiplayerPlugin.Update` iterated `mgr.Host.Transports` (backed by `HostSession._transports`), but that dictionary is only populated *inside* `OnHello` after a successful handshake. So the freshly-attached transport sat in `SessionManager._clientTransports` with no pump, `SteamNetworking.ReadP2PPacket` was never called for it, the client's Hello never moved into the inbox, `OnHello` never fired, `ClientAccepted` never raised, `LobbyState` never broadcast — the friend's panel hung forever on "Waiting for host's lobby state…". Classic chicken-and-egg: pump depends on OnHello, OnHello depends on pump. Latent since Plan 2 — no two-machine testing ever ran, and single-machine smokes use a null transport. Fix: added `SessionManager.PumpHostTransports()` which iterates the raw `_clientTransports` list (populated immediately on `AttachClient`), and the plugin's Update now calls that instead of `mgr.Host.Transports`. `HostSession.Transports` still has the "post-handshake, slot-assigned" semantics that `LobbyPanel` broadcast paths rely on.
- **`BeginSaveTransfer` tried to read `<save>.binary.binary`.** PCBS's `SaveFileInfo.Name` already includes the `.binary` extension (e.g. `auto.binary`), and the host was unconditionally appending another one. First host to pick an existing save would have hit `save read failed at … auto.binary.binary: Could not find file`. Fix: strip trailing `.binary` (case-insensitive) before re-appending.

## [0.3.0-alpha-preview3] — 2026-04-21 (superseded by preview4)

Second hotfix respin. `preview2` fixed the dispatcher crash and phantom-session bug, but two issues remained once a real host + client tried to pair: the client got as far as "Client session started" but the host never saw them, and the Steam "Join Game" button required a redundant second click on "Join Multiplayer" in-game. No feature changes.

### Fixed — release-blocking

- **Host never accepted the client's P2P session**, so the client's `Hello` packet was silently dropped by Steam and the host never broadcast `LobbyState` back. Symptom: client's lobby panel stuck on "Waiting for host's lobby state…" forever, host's player list never showed the friend, host log had `"Peer joined lobby: <id>"` but no `"Client accepted (slot …)"`. Root cause: `SessionLifecycle.OnPeerJoined` constructed `SteamTransport` but never called `SteamNetworking.AcceptP2PSessionWithUser(peerId)`. The client side did accept on join, but Steam requires BOTH sides to accept before packets flow — packets sent before the host accepts are discarded. The `P2PSessionRequest_t` callback inside `SteamTransport` was supposed to catch this, but it was registered too late (after the client's Hello had already been dropped). Fix: call `AcceptP2PSessionWithUser` as the first line of `OnPeerJoined`, before the `SteamTransport` is constructed.

### Changed — UX

- **Accepting a Steam invite now auto-opens the lobby**. The previous flow required three clicks (Steam invite → "Join Game" in overlay → "Join Multiplayer" button on main menu), because `OnInvite` only stashed the pending invite and waited for the main-menu button. Now `OnInvite` calls `JoinPendingInvite()` directly once the `GameLobbyJoinRequested_t` callback fires, collapsing to a single "Join Game" click. The "Join Multiplayer" main-menu button still works as a fallback if the Steam overlay click is dismissed. Guard added: if a session is already active when an invite arrives, the auto-join is skipped and a warning logs "leave first, then use 'Join Multiplayer'".

## [0.3.0-alpha-preview2] — 2026-04-21 (superseded by preview3)

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
