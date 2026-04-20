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
