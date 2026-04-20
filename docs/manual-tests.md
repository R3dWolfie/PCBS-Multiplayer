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
