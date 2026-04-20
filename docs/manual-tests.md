# Manual Test Log — Plan 2

Running record of M1/M2/M3 playtests. One entry per attempt.

---

## T10 — M1 smoke (plugin loads)

**Date:** 2026-04-20
**DLL:** `bin/Release/net46/PCBSMultiplayer.dll` @ `7cbded9` (post-T10a refactor)
**Platform:** Linux / Proton / PCBS 621060
**Launch options:** `WINEDLLOVERRIDES="winhttp=n,b" %command%`
**Result:** **PASS** (with yellow flag)

### Evidence

`BepInEx/LogOutput.log`:

```
[Info   :   BepInEx] Loading [PCBS Multiplayer 0.1.0]
[Warning:  HarmonyX] AccessTools.GetTypesFromAssembly: assembly PCBSMultiplayer
  => System.Reflection.ReflectionTypeLoadException: The classes in the module cannot be loaded.
[Info   :PCBS Multiplayer] PCBS Multiplayer 0.1.0 loaded. Steam user: Red
[Message:   BepInEx] Chainloader startup complete
```

### Flags

- `ReflectionTypeLoadException` during `Harmony.PatchAll` reflection pass. Harmless for T10 (no `[HarmonyPatch]` classes shipped yet) but will cause Harmony to silently skip any patch class in T11–T14 if the same type fails to load. Needs diagnosis before T11 lands.

### Prior attempts

- **15:57 (pre-T10a):** plugin failed to load — `FileNotFoundException: netstandard, 2.0.0.0` + `TypeLoadException: NullableAttribute`. Root-caused to netstandard2.0 TFM on a Unity 2018.4 Mono runtime that can't resolve netstandard facades. Fixed by retargeting net46 (T10a).

---

## T10b — Mono 2018 type-load fix (closes T10's yellow flag)

**Date:** 2026-04-20
**DLL:** `bin/Release/net46/PCBSMultiplayer.dll` (36864 bytes, post-cleanup)
**Result:** **PASS**

### Evidence

`BepInEx/LogOutput.log` — clean load, no `ReflectionTypeLoadException`, no `LoaderExceptions`:

```
[Info   :   BepInEx] Loading [PCBS Multiplayer 0.1.0]
[Info   :PCBS Multiplayer] PCBS Multiplayer 0.1.0 loaded. Steam user: Red
[Message:   BepInEx] Chainloader startup complete
```

### Root cause

Unity 2018.4's Mono type loader refuses to bind **fields** of these mscorlib delegate/tuple types at class-load time, even though their assembly resolves fine:

| Field type | Loads? |
|---|---|
| non-generic `System.Action` | FAIL |
| `System.Action<T>` (1 type param) | OK |
| `System.Action<T1,T2>` (2+ type params) | FAIL |
| `System.Func<T,TResult>` (2+ type params) | FAIL |
| `System.ValueTuple<,>` (any arity) | FAIL |
| custom delegate | OK |
| Steamworks.NET `Callback<T>` | OK |

When any type fails, its compiler-generated `<>c` (stateless-lambda cache) and `<>c__DisplayClass…` (captured-locals) siblings also fail, and HarmonyX's `AccessTools.GetTypesFromAssembly` silently drops the whole assembly — so `[HarmonyPatch]` classes would never register.

Isolated via a controlled probe matrix (`ProbeTypes.cs`, now deleted) that tested one suspect pattern per type.

### Fix

Refactored 5 files in `src/PCBSMultiplayer/Session/`:

1. **GraceTimer.cs** — added `public delegate void GraceCallback()`; replaced `Action Callback = () => { }` field with plain `GraceCallback Callback` (no lambda field init).
2. **SteamLobby.cs** — added `HostCreatedHandler`, `LobbyJoinedHandler`, `InviteHandler` custom delegates; replaced `Action<CSteamID, string?>` and friends; moved stateless `ev => onInvite(ev.m_steamIDLobby)` lambda inside `Callback<T>.Create(...)` to a named instance method `OnJoinRequested`.
3. **SessionManager.cs** — replaced `List<(ITransport, MessageRouter)>` with `List<ClientTransportEntry>` (private readonly struct); rewrote `foreach` destructuring sites.
4. **HostSession.cs** — replaced 3× `.Select(j => new JobDto {...}).ToList()` in `BroadcastJobBoardDelta` with plain `foreach` loops (eliminates `HostSession+<>c`).
5. **ClientSession.cs** — replaced 3× `.Select(...)` in `OnWelcome` with `foreach` loops (eliminates `ClientSession+<>c`).

Also removed `using System.Linq` from HostSession/ClientSession, dropped `?` on reference types (project already has `<Nullable>disable</Nullable>` from T10b debugging).

Cecil post-refactor dump confirms: no `<>c` nested class exists on any Session type; `<>c__DisplayClass*` classes are clean (no forbidden field types).

All 50 xUnit tests still green.

---

## Known Limitations (Plan 2)

**Client-side job board UI is not mirrored.** The host owns authoritative `JobBoardState` and broadcasts `JobBoardDelta` on every change. The client's `WorldState.JobBoard` stays in sync, but the client's in-game Career UI (the job list the player sees) is not patched to reflect the shared board. Deferred to Plan 3.

Consequence for Plan 2 playtests: claiming/completing a job on the client still sends `ClaimJobRequest` and drives state through the authoritative path, but each client sees its own local pool of jobs until the next day roll or UI refresh. The multiplayer invariant (no double-claim, money is authoritative) holds; the UX of "see which job your friend just took" does not.

**Client-side job lifecycle is single-origin.** `OnCollected`/`OnQuit`/`OnDiscard` only fire server-authoritative broadcasts on the host. A client completing a job will route through `RequestClaimJob` / money deltas only; its local `Job.OnCollected` does not replay to other peers. This is acceptable because the money delta is what other peers care about, and that is broadcast.

---

## T18 — M1b smoke (patches load, vanilla unaffected)

**Date:** 2026-04-20
**DLL:** `bin/Release/net46/PCBSMultiplayer.dll` (40960 bytes, post-T11–T17)
**Result:** **PASS**

### Evidence

`BepInEx/LogOutput.log` (mtime 19:55:40, post-deploy 19:52):

```
[Info   :   BepInEx] Loading [PCBS Multiplayer 0.1.0]
[Info   :PCBS Multiplayer] PCBS Multiplayer 0.1.0 loaded. Steam user: Red
[Message:   BepInEx] Chainloader startup complete
```

Zero `ReflectionTypeLoadException`, zero `LoaderException`, zero patch log errors. Six `[HarmonyPatch]` classes (`SpendCashPatch`, `AddCashPatch`, `AddJobPatch`, `AddJobForDLC2Patch`, `OnAcceptPatch`, `OnCollectedPatch`, `OnQuitPatch`, `OnDiscardPatch`) all registered cleanly via `Harmony.PatchAll`. Vanilla gameplay unaffected (no session active → all patches early-return on `SessionManager.Current == null`).

---

## T20 — Panic hotkey manual verify

**Date:** 2026-04-20
**DLL:** `bin/Release/net46/PCBSMultiplayer.dll` (44032 bytes)
**Result:** **PASS**

### Verified

- Top-right yellow-on-black pill "F7 hold 3s - Disable mod" visible on main menu at 4K.
- Tap F7 briefly → progress pill shows "Panic: hold F7 (XX%)" then reverts.
- Hold F7 3s → label vanishes; panic handler flipped `EnableMultiplayer = false` and saved config.
- Restart PCBS → log line 15: `PCBS Multiplayer disabled via config — skipping all hooks.` Patch registration completely skipped, plugin still loads.

### Cosmetic fix during the test

First deploy used `GUI.Label` defaults at (10, 10) — nearly invisible at 4K (~12pt, near-white text on the bright menu background). Fixed by drawing a translucent black `GUI.DrawTexture` rectangle behind a 28pt bold yellow label anchored top-right via `Screen.width - CalcSize().x`. Cached GUIStyle in a static field to avoid per-frame allocation.

---

## T26 — M2 smoke (host lobby + invite overlay)

**Date:** 2026-04-20
**DLL:** `bin/Release/net46/PCBSMultiplayer.dll` (post-MainMenuPatcher cleanup)
**Result:** **PASS**

### Verified

- Launched PCBS, main menu shows top-center OnGUI buttons "Host Multiplayer" / "Join Multiplayer" (420×70, 28pt bold).
- Clicked **Host Multiplayer** → `SessionLifecycle.StartHost` called `_lobby.CreateLobby` → `OnHostLobbyCreated` fired → session created + `SteamFriends.ActivateGameOverlayInviteDialog` opened.
- Selected friend **AlexRainbowCookies** from Steam overlay invite → Steam chat confirmed: *"You invited AlexRainbowCookies to play PC Building Simulator"*.
- No exceptions in log.

### Cosmetic fixes during the test

1. **UGUI clone injection was half-broken.** Cloned buttons appeared as clipped/invisible stubs in the main-menu column (PCBS's VerticalLayoutGroup + content-sizer clipping overflow). Dropped the clone code entirely — the OnGUI fallback is now the sole path. Removed ~60 lines of diagnostic code + `TMPro`/`UnityEngine.UI` imports from `MainMenuPatcher.cs`.
2. **OnGUI buttons overlapped with the F7 panic label** (both top-right). Moved to top-center, 28pt bold, 420×70 each.
3. **`StartHost` was fire-once.** Clicking Host a second time just logged "already in a session" and did nothing. Fixed in `SessionLifecycle.StartHost` to re-open the invite overlay when `SessionManager.Current.Role == Host`, so users can re-invite.

---

## T27 — M3 closing gate (two-machine real P2P)

**Pending.** Requires a second machine (or friend on another box) running the same DLL build. Protocol:

1. **Host machine:** launch PCBS → Host Multiplayer → invite friend via Steam overlay → click **Career → Continue** (load existing save).
2. **Peer machine:** accept Steam invite → PCBS launches → **Join Multiplayer** → click **Career → Continue** (any save).
3. **Verify:**
   - Host log has `Peer joined lobby: <steamID>` when client connects.
   - Host spends money in-game → client's cash updates in HUD.
   - Host accepts a Job from the board → client's `WorldState.JobBoard` reflects it (log-level verify; client UI not mirrored — known limitation T17).
   - No exceptions, no desync warnings, no freezes for ~5 min of normal play.
   - Panic hotkey F7-hold still works on either side.
4. **Closing criteria:** both sides conclude the session via main menu "Quit to main menu" without errors.

---

## P3.B-T1 — Recon gate (Plan 3 save system)

**Date:** 2026-04-21
**Result:** GO with caveats
**Summary:** `SaveLoadSystem.s_saveDir` is `public static string` — no publicizer or reflection needed; initialized to `"Saves/<steamID64>"` by `SteamController.SetSaveDirForUser` on Awake. `LoadGameFromDir` takes a bare `name.binary` filename and prepends `s_saveDir + "/"`. The **existing-save (Continue) path is GO**: bytes are on disk before any transfer call. The **new-career (StartNewGame) path is NO-GO for 0.3**: `StartNewGame` performs no disk write whatsoever — the first save only exists after the user manually saves in-game — so `BeginSaveTransfer` would read nothing on a fresh session.
**Follow-up (caveat):** P3 scope for 0.3 must be restricted to **existing-save path only** (host picks "Continue", not "New Game"). `BeginSaveTransfer` should be triggered from the `ContinueCareer` code path. New-game multiplayer requires a save-on-demand hook (`StartNewGame` → `CreateSaveGame` post scene-load) and is deferred to a later milestone.
