# PCBS Reconnaissance — Plan 2 Patch Targets

**Captured:** 2026-04-20 from `Assembly-CSharp-firstpass.dll` (PCBS build unknown — no `version.txt` present; install path `/run/media/system/Storage/Steam/steamapps/common/PC Building Simulator/`). Publisher metadata in `PCBS_Data/app.info`: `The Irregular Corp / PC Building Simulator`.
**Re-run when:** PCBS updates. Re-verify every signature before shipping any mod update.

## Money

| Target | Signature | Visibility | Callers |
|---|---|---|---|
| `CareerStatus.SpendCash` | `bool SpendCash(int cash, bool force)` | public instance | `CareerStatus.PayBankCharges`, `CareerStatus.BuyShare`, `CareerStatus.PayRent`, `CareerStatus.PayUtility`, `FuturLab.DebugITSupportMenu.DoGUI`, `FuturLab.ITSupportStoryDLC.ContinueWithEndDay`, `FuturLab.TabletShopShoppingCart.OnBuy`, `PCBayApp.OnBuyAction`, `Shop.OnBuy`, `BuildFurniture.<GetInteractions>m__0` |
| `CareerStatus.AddCash` | `void AddCash(int amount)` | public instance | `CareerStatus.OnCollect`, `CareerStatus.CollectAuction`, `CareerDebug.DoCareer`, `FuturLab.StoryScheduler.AwardBudget`, `FuturLab.OnboardingManager.<DLC2TriggerStartMessages>m__1`, `FuturLab.ITSupportSellEmailMessage.OnPositiveAction`, `FuturLab.ITSupportDifficultySettingsMenu.OnCashInjectionToggle`, `Inventory.SellItem`, `ItemDisplay.SellItem` |
| `CareerStatus.Get` | `static CareerStatus Get()` | public static | singleton accessor |

## Jobs

| Target | Signature | Visibility | Notes |
|---|---|---|---|
| `CareerStatus.AddJob` | `void AddJob(Job job)` | public instance | Callers: `CareerStatus.InitClassicCareerMode`, `CareerStatus.GeneratePreviewJob`, `CareerStatus.GenerateJob`, `CareerDebug.GenerateJob`, `FuturLab.StoryScheduler.CheckAndAddJobInternal`. Note: a separate `AddJobForDLC2(Job)` exists — likely needs its own patch parity |
| `Job.OnAccept` | `void OnAccept(bool _autoAccepting, BenchSlot _targetSlot, bool _autoTurnOn)` | public instance | Player claims |
| `Job.OnCollected` | `void OnCollected()` | public instance | Player completes |
| `Job.OnQuit` | `void OnQuit()` | public instance | Player abandons |
| `Job.OnDiscard` | `void OnDiscard()` | public instance | Player rejects |
| `Job.GetId` | `int GetId()` | public instance | Stable id → our `PCBSMultiplayer.State.Job.Id = GetId().ToString()` |

## Main Menu

| Target | Signature | Notes |
|---|---|---|
| `MainMenu` class | `MonoBehaviour` | Patchable entry point |
| `MainMenu.Start` | `private void Start()` | Postfix here to inject our buttons |
| `MainMenu.OnEnable` | `private void OnEnable()` | Available as secondary hook if needed |
| `MainMenu.mainMenu` | `public GameObject` field | The root we attach buttons to |
| `MainMenu.menuCanvas` | `public GameObject` field | Canvas parent |
| `MainMenu.howToBuildAPC` | `public Button` field | Reference for sizing our injected buttons |

Additional public fields on `MainMenu` that may be useful: `Scene freePlayScene`, `OptionsMenu optionsMenu`, `GameObject hud`, `GameObject continueOptions`, `GameObject socialGlobal`, `GameObject socialChina`, `WhatsNewPopUp whatsNew`, `MenuContext m_context`, `SaveLoadMenu loadMenu`, `WorkshopSelectionMenu workshopSelectionMenu`.

## F-Key Availability

F5, F6, F7 — zero `KeyCode.Fn` references in either Assembly-CSharp assembly. Safe to bind.

```
F5: 0
F6: 0
F7: 0
```

## Go/No-Go Verdict

**GO.** Every patch target is a single stable chokepoint. No transpilers required. CareerStatus singleton reachable via static `Get()`. MainMenu provides a clean `Start()` postfix hook.

### Verification Checklist

- [x] `CareerStatus.SpendCash(Int32, Boolean) → Boolean` — public instance
- [x] `CareerStatus.AddCash(Int32) → Void` — public instance
- [x] `CareerStatus.AddJob(Job) → Void` — public instance
- [x] `CareerStatus.Get() → CareerStatus` — public static
- [x] `Job.OnAccept(Boolean, BenchSlot, Boolean) → Void` — public instance
- [x] `Job.OnCollected() → Void` — public instance
- [x] `Job.OnQuit() → Void` — public instance
- [x] `Job.OnDiscard() → Void` — public instance
- [x] `Job.GetId() → Int32` — public instance
- [x] `MainMenu.Start()` + `MainMenu.OnEnable()` exist as private methods
- [x] `MainMenu.mainMenu` + `MainMenu.menuCanvas` are public `GameObject` fields
- [x] F5/F6/F7 unbound in both assemblies

## Save System (Plan 3)

**Captured:** 2026-04-20 from `Assembly-CSharp-firstpass.dll`. This section documents the career save enumeration and loading API available from the main menu.

### Key Classes and Managers

| Class | Namespace | Role |
|-------|-----------|------|
| `SaveLoadSystem` | (root) | Singleton MonoBehaviour; central save/load orchestrator. No explicit `Instance` property; accessed as standard Unity singleton pattern (find component). |
| `SaveLoadMenu` | (root) | UI controller for save/load menus. Invoked by `MainMenu.ShowLoadMenu()` |
| `SaveGameHeader` | (root) | Data class holding metadata about a save (name, version, game mode, cash, kudos, hard mode flag, app version) |
| `SaveLoadSystem.SaveGameInfo` | (root) | Nested struct returned by `GetSaveGames()`. Contains `FileInfo m_info` and `SaveGameHeader m_header` |
| `LevelLoadPersistency` | (root) | Singleton that bridges save selection (main menu) to scene load. Sets `SaveName` property before loading game scene |

### Enumeration API

**Method Signature:**
```csharp
static IEnumerable<SaveLoadSystem.SaveGameInfo> SaveLoadSystem.GetSaveGames()
```

**Returns:** An enumerable of `SaveGameInfo` objects. Each contains:
- `FileInfo m_info` — file path on disk (use `.Name` for filename, `.FullName` for full path)
- `SaveGameHeader m_header` — metadata including:
  - `string m_saveName` — user-facing save display name
  - `GameMode m_gameMode` — enum: `NOT_SET`, `CAREER`, `FREEBUILD`, `HOW_TO_BUILD_A_PC`, `DLC_ESPORTS`, `DLC_ITSUPPORT`
  - `int m_cash`, `int m_kudos`, `bool m_hardMode` — progression snapshot
  - `int m_version` — save file format version
  - `string m_scene` — which scene/workshop was active

**Usage from main menu:**
```csharp
// From MainMenu.ShowLoadMenu(GameMode gameMode):
foreach (var saveInfo in SaveLoadSystem.GetSaveGames())
{
    // Filter by gameMode if needed:  if (saveInfo.m_header.m_gameMode == gameMode)
    string displayName = saveInfo.m_header.m_saveName;
    string filename = saveInfo.m_info.Name;  // or .FullName for full path
}
```

### Loading API

**Primary Load Methods:**

1. **Load by filename (lowest level):**
   ```csharp
   void SaveLoadSystem.LoadGame(string name)
   ```
   Takes a save filename (not full path; matched in save directory). Blocks thread; loads the save into the workshop scene. Called by `SaveLoadMenu.LoadGame()` internally.

2. **Via LevelLoadPersistency (recommended for main menu):**
   ```csharp
   void LevelLoadPersistency.LoadGame(string save, string scene)
   ```
   Public method; sets `SaveName` property and loads the specified scene. Called via Unity's scene async loader. **This is the safe entry point from main menu** — it bridges save selection to scene load without blocking.
   ```csharp
   void LevelLoadPersistency.LoadCurrentGame(string scene)
   ```
   Alternative: loads whatever save was previously set via `set_SaveName()`.

### Save Directory

**Static field with directory path:**
```csharp
static string SaveLoadSystem.s_saveDir
```
This field is initialized at runtime (likely to `%PersistentDataPath%/Saves/` or similar). The game enumerates this directory when `GetSaveGames()` is called.

**File naming convention (inferred from static names):**
- Career mode: user-provided name + `.save`
- Auto-saves: `_AutoSave_CAREER.save`, `_AutoSave_CAREER_Hard.save`, `_AutoSave_Freebuild.save`, `_AutoSave_DLC2_eSports.save`, `_AutoSave_DLC3_ITSupport.save`

### MainMenu Integration

The main menu flows through these entry points:

| Button / Flow | Method | Target |
|---|---|---|
| "Continue Career" (if save exists) | `MainMenu.ContinueCareer(GameMode gameMode)` | Loads the most recent career save for that mode |
| "Load Game" | `MainMenu.ShowLoadMenu(GameMode gameMode)` | Opens `SaveLoadMenu.Open(gameMode, forSave=false)` → player picks a save → `SaveLoadMenu.LoadGame()` → `SaveLoadSystem.LoadGame()` |
| "New Game" | `MainMenu.PlayCareer(GameMode gameMode, bool hard)` | Starts fresh career (no save enumeration) |

**Key field for load button detection:**
- `MainMenu.continueOptions` (GameObject) — container for Continue button. Will be inactive if no saves exist.
- `SaveLoadMenu` is a UI prefab referenced in MainMenu; its public methods are `Open()`, `Refresh()`, and event subscriptions (`SubscribeForLoad`, `SubscribeForSave` on `SaveGameDisplay`).

### Creation API

**New career creation:**
```csharp
string SaveLoadSystem.CareerSave(string filename)
```
**Internal save (called during gameplay):**
```csharp
void SaveLoadSystem.SaveCurrentGame()
void SaveLoadSystem.SaveCurrentGameForDLC(string _forceSavedSceneName, bool _scrubSaveForReset)
```

These are NOT directly callable from the main menu context (require active game scene). For the MVP, you won't need to create saves from the main menu.

### Go/No-Go Verdict

**GREEN** — Clean enumeration + load API available.

**Justification:**
1. `SaveLoadSystem.GetSaveGames()` is static and returns all saves with metadata (display name, mode, progression).
2. `LevelLoadPersistency.LoadGame(save, scene)` is a public, scene-safe entry point for loading from the main menu — avoids blocking the main thread.
3. No complex state traversal required. Save enumeration and selection can happen entirely on the main menu before any scene load.
4. `SaveGameHeader.m_gameMode` enum allows filtering by mode (CAREER, FREEBUILD, etc.).
5. Full path available via `SaveGameInfo.m_info.FullName`, so file operations are possible.

**Implementation path for Plan 3:**
- Host enumerates its own save list via `GetSaveGames()`.
- Host sends list to clients (save name, game mode, progression snapshot from `m_header`).
- Clients display list on a custom UI overlay on the main menu.
- On selection, both host and client call `LevelLoadPersistency.LoadGame(saveName, scene)` with the same save identifier.
- If save file doesn't exist locally (multiplayer scenario), clients can reject or prompt user to pick their own save.

## Plan 3: Save System (captured 2026-04-21)

### `SaveLoadSystem.s_saveDir`
- **Assembly:** `Assembly-CSharp-firstpass.dll`
- **Declaration:** `public static string s_saveDir = "Saves";` (line 203121)
- **Initializer:** `SteamController.SetSaveDirForUser(string user)` — called from `SteamController.Awake()` after `SteamUser.GetSteamID()` succeeds (line 209111–209336). Sets `s_saveDir = "Saves/" + steamID64`.
- **Runtime value pattern:** `"Saves/<steamID64>"` — e.g. `"Saves/76561198012345678"`. Relative to the Unity working directory (the game's install root under Proton). Already confirmed by the in-game log line: `"Save path for user {0}: {1}"`.
- **Public accessibility:** Already `public static` — visible directly via `SaveLoadSystem.s_saveDir`. No publicizer or reflection required.
- **Sibling fields:** `s_freeBuildSaveName`, `s_autoSaveName`, `s_hardAutoSaveName`, `s_eSportsDLCSaveName`, `s_itSupportDLCSaveName`, `s_itSupportDLCMondaySaveName` — all `public static string`. No `s_mpSaveDir` or equivalent exists; Plan 3 must create `mp-<lobbyId>.binary` inside the existing `s_saveDir`.

### Save-write timing (new-career path)

- **Call chain (new career):** `MainMenu.PlayCareer` → `LevelLoadPersistency.StartNewGame(freebuild: false)` → `LoadingScreenManager.Instance.LoadScene(Scene, isWorkshopScene: true)`. **No save write occurs.** The game loads the workshop scene with a blank `localSaveGame`; the first `.binary` is only written when the user manually saves in-game via the pause menu (`SaveLoadSystem.CareerSave` → `CreateSaveGame`).
- **Call chain (continue/existing save):** `MainMenu.ContinueCareer` → `LevelLoadPersistency.LoadGameFromDir(saveName, scene)` — bytes already on disk before this call.
- **`CreateSaveGame` write kind:** Synchronous. Uses `new FileStream(..., FileMode.Create)` + `BinaryFormatter.Serialize` + `fileStream.Flush()` — no coroutine, no async, no thread pool (line 203443–203458).
- **Existing-save path:** bytes already on disk — always GO.
- **Verdict (new-career path):** **NO-GO** — `StartNewGame` performs zero disk I/O. There is no `.binary` to transfer until the user explicitly saves in-game. `BeginSaveTransfer` cannot be called immediately after `PlayCareer` on a new-career session.

### Mitigation / re-scope for new-career path

Plan 3 has two viable options:

1. **Restrict to existing-save path only (recommended for 0.3):** Host must pick "Continue" (an existing `.binary`) not "New Game". The host's save list is enumerated via `GetSaveGames()`; `BeginSaveTransfer` is called from the `ContinueCareer` handler. New Game multiplayer is deferred to a later milestone.
2. **Add save-on-demand hook:** Harmony-patch `LevelLoadPersistency.StartNewGame` to call `SaveCurrentGame()` after scene load completes (in `OnSceneLoaded`). Creates a save file with a known MP name (`mp-new-<lobbyId>.binary`) that `BeginSaveTransfer` can immediately read. More invasive; deferred to post-0.3 unless the controller explicitly unblocks it.

### `LevelLoadPersistency.LoadGameFromDir`
- **Signature:** `public void LoadGameFromDir(string saveName, string scene)`
- **Argument convention:** `saveName` is the **bare filename including `.binary`** (e.g. `"save1.binary"`). The `.binary` extension is NOT appended internally — callers already provide it.
- **Composition:** `LoadGame(SaveLoadSystem.s_saveDir + "/" + saveName, scene)` (line 202503–202504). So the client must write its received bytes to `SaveLoadSystem.s_saveDir + "/" + saveName` and then call `LoadGameFromDir(saveName, scene)` with the same bare name.

### `SaveLoadSystem.GetSaveGames`
- **Signature:** `public static IEnumerable<SaveGameInfo> GetSaveGames()`
- **Entry type:** `SaveGameInfo` — a nested public class with two fields:
  - `public FileInfo m_info` — full `FileInfo` for the save file (gives `.Name`, `.FullName`, `.LastWriteTime`, `.Length`)
  - `public SaveGameHeader m_header` — deserialized header with `m_version`, `m_gameMode` (enum), `m_scene` (string), `m_saveName` (display name), `m_cash` (int), `m_kudos` (int), `m_hardMode` (bool)
- **Filter:** only `.binary` files with `m_version >= 7` are yielded; `AchievementProgress*.binary` is excluded. Results ordered by `LastWriteTime descending`.

### Go/No-Go verdict for Plan 3
**GO with caveats** — The save infrastructure is sound and all required APIs are public. The single caveat is the new-career path: `StartNewGame` writes no `.binary`, so `BeginSaveTransfer` cannot serve a new-game session without an extra save-on-demand hook. The recommended 0.3 scope is **existing-save ("Continue") path only**. All other assumptions (s_saveDir visibility, LoadGameFromDir argument convention, GetSaveGames shape) are confirmed GO.

## Plan 5: Player controller recon (P5-T1) — 2026-04-22

### Player controller class

- **Class name:** `PCBS.PlayerController` (assembly `Assembly-CSharp-firstpass.dll`, namespace `PCBS`, defined at `csharp-firstpass.cs:115703`). Plain `MonoBehaviour`, no base class other than `MonoBehaviour`.
- **Local player locator:** `FuturLab.WorkshopController.Get().PlayerController` (singleton accessor; `WorkshopController.s_instance` is set in `Awake` and exposes `public PlayerController PlayerController => m_playerController`). Fallback inside `WorkshopController.Awake` is `GameObject.FindWithTag("Player").GetComponent<PlayerController>()`, which confirms the player GameObject is tagged `"Player"` — `Camera.main` would also resolve to its child camera if it retains `MainCamera` tag (not verified in source).
- **Movement model:** `CharacterController.Move(Vector3 motion)` via a sibling `PCBS.Walk` component (`Walk.Move` at `csharp-firstpass.cs:115892`). The player GameObject has a `CharacterController` component accessible via `PlayerController.CharacterController` (lazy-init getter). Position is read/written as `PlayerController.transform.position` (confirmed in `SaveLoadSystem.SaveGameData` at `csharp-firstpass.cs:203549`: `ctrl.PlayerController.transform.position`).
- **Camera transform path:** `PlayerController.MainCamera` (public `Camera MainCamera => m_camera;`, backed by a `[SerializeField] private Camera m_camera`). The camera is a child of the player root; `Transform` path is `<player>/<camera child>`. `Camera.main` is also used extensively elsewhere (e.g. `WorkStation componentInParent = Camera.main.GetComponentInParent<WorkStation>()`), so `Camera.main` is expected to resolve inside workshop scenes — but for Plan 5 the **canonical access is `WorkshopController.Get().PlayerController.MainCamera`** which avoids tag-resolution flakiness during scene transitions.

### Eye-height

- Source of truth: **TBD via live measurement**. No hardcoded constant for eye-height exists in the decompiled player-controller source. The camera's local offset is a prefab-serialized `Transform.localPosition` on the child `m_camera` GameObject — not visible in the decompiled C# (prefab data lives in the Unity asset bundle, not the DLL). There is no `cameraOffset`/`eyeHeight`/`headOffset` field on `PlayerController`, `Walk`, or `MouseLook`. `MouseLook` only owns rotation state (`rotationX`/`rotationY`), not position.
- **Computed offset** (camera root minus player root): `TBD — measure live` via `PlayerController.MainCamera.transform.localPosition.y` in a Harmony postfix on `PlayerController.Update` once the broadcaster lands (or via a one-shot debug hotkey during P5 smoke). Expected value is in the ~1.6–1.8 m range based on Unity's default `CharacterController` (height=2.0, center.y=1.0) and typical FPS camera offset, but **do not assume — measure**.
- Capsule `Vector3.up * 0.9f` offset for P5-T10 (capsule center sits 1.0 m above base): the default `0.9f` in the spec is **TBD** until eye-height is measured. If measured eye-height is ~1.8 m, the capsule centre-above-feet is 0.9 m and the spec default holds. If eye-height differs, adjust so remote capsule-centre sits at `measured_eye_y - 0.9` (i.e. capsule feet rest on the same plane as `PlayerController.transform.position.y`).

### Scene

- Workshop scene name (as it appears in `StartGame.SceneName`): **multi-valued** — taken from `SaveGameHeader.m_scene` on the host's selected save. Default is `"Workshop_V2"` (`LevelLoadPersistency.kDefaultWorkshopSceneName` at `csharp-firstpass.cs:202444`). Known DLC workshop scenes observed in source:
  - `Workshop_V2` (default career workshop)
  - `Workshop_DLC1_Starter`, `Workshop_DLC1_Office`, `Workshop_DLC1_TopTier` (DLC1 eSports chain)
  - `Workshop_DLC2_1`, `Workshop_DLC2_2`, `Workshop_DLC2_3` (DLC2 IT Support chain)
  - `HowToBuildAPC_V2` is the tutorial scene — **should NOT broadcast** (solo-only, not multiplayer-supported).
  - `Menu_V2` is the main menu — **must NOT broadcast**.
- **Recommended gate for Plan 5 broadcaster:** instead of a hardcoded scene-name whitelist, gate on `FuturLab.WorkshopController.Get() != null && WorkshopController.Get().PlayerController != null`. The `WorkshopController` singleton only exists in workshop scenes (set in `WorkshopController.Awake`), so this check naturally excludes `Menu_V2`, `HowToBuildAPC_V2`, and every non-workshop scene. Optionally also verify `SceneManager.GetActiveScene().name != "HowToBuildAPC_V2"` as defence-in-depth.

### Head-bob / FOV flags

- Head-bob: **NO SEPARATE TRANSFORM**. Zero matches for `HeadBob`/`bobCurve`/`bobAmount`/`bobSpeed` in `Assembly-CSharp-firstpass.dll`. The camera is a direct child of the player root; `MouseLook` only applies rotation to its own transform, never position. The stable position source to broadcast is **`PlayerController.transform.position`** (feet/capsule base) for P5 capsule placement. Camera-world-position (`PlayerController.MainCamera.transform.position`) is the source of truth for eye-height derivation but is NOT needed on the wire — the remote end reconstructs eye-height from `posY + eye_offset` locally.
- FOV: not stored on `PlayerController` — there is no `fieldOfView`/`FOV` field on `PlayerController`, `Walk`, or `MouseLook`. FOV is whatever Unity inspector value is baked into the camera prefab (game uses Unity's default `Camera.fieldOfView`, plus per-shader tweaks in post-processing stacks). **Does not affect remote capsule rendering** — FOV is a local render-only effect. Confirmed NOT serialized into saves (no `fieldOfView` field on `SaveGame`).

### Verdict

`GO` — three of the four facts captured with high confidence (class/locator, camera path, scene gating strategy). Eye-height is explicitly deferred to live measurement in P5-T2+ (hardcoded value does not exist in the DLL; this is expected for Unity prefab-driven offsets and is not a blocker). Plan 5 broadcaster can proceed against `WorkshopController.Get().PlayerController` as its local-player source, broadcasting `PlayerController.transform.position` + `MouseLook.rotationX`/`rotationY` (or derived quaternion) on a tick, with a scene gate of `WorkshopController.Get() != null`.

