# Installing PCBS Multiplayer on Linux / Steam Deck

Step-by-step install for Linux users (desktop and Steam Deck alike). If you're on Windows, see [`docs/install-windows.md`](install-windows.md) instead.

## Why this is different from Windows

PC Building Simulator has no native Linux build — on Linux, the game always runs under **Proton** (Steam's Wine-based compatibility layer). BepInEx hooks the game by intercepting `winhttp.dll` at process startup, which on Windows "just works" because Windows loads the local DLL alongside the executable. Wine, however, ships its *own* built-in `winhttp.dll` and prefers it over the local copy. Without telling Wine otherwise, BepInEx's hook never fires and the mod appears to do nothing.

The fix is a single **Steam launch option** — `WINEDLLOVERRIDES="winhttp=n,b" %command%` — which instructs Wine to load the **n**ative (= local) copy first, then fall back to the **b**uilt-in one. This is the most common failure mode for Linux users; if your main-menu buttons don't show up, it's almost certainly this.

## Requirements

- **PC Building Simulator** (original / 2018) on Steam, running through Proton (Experimental or 7.0+ works fine)
- **Steam Deck** users: same steps; no special setup
- Steam running (friends-only lobbies over Steam P2P)
- Both host and client need the same PCBS version and the same mod version

The mod is **symmetric** — host and client install it the same way, regardless of whether either is on Windows or Linux.

## 1. Find your PCBS install folder

In Steam: right-click **PC Building Simulator** → **Properties** → **Installed Files** → **Browse**. That opens a file manager at the PCBS install folder — the one containing `PCBS.exe`.

The full path is typically:

```
~/.steam/steam/steamapps/common/PC Building Simulator/
```

or on Steam Deck:

```
~/.local/share/Steam/steamapps/common/PC Building Simulator/
```

## 2. Extract the bundle

1. Download `PCBSMultiplayer-v0.2.0-rc1-linux.zip` from the [releases page](https://github.com/R3dWolfie/PCBS-Multiplayer/releases).
2. Extract it **directly into your PCBS folder** from step 1. After extracting, the folder should contain:

```
PC Building Simulator/
├── PCBS.exe
├── PCBS_Data/
├── BepInEx/              ← new
├── doorstop_config.ini   ← new
└── winhttp.dll           ← new
```

The mod DLL is pre-placed in `BepInEx/plugins/PCBSMultiplayer.dll`.

## 3. Set the WINEDLLOVERRIDES launch option

**This is the step that makes BepInEx actually load.** Skip it and nothing else will work.

In Steam: right-click **PC Building Simulator** → **Properties** → **Launch Options**. Paste exactly:

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

If you already have launch options set, leave them in place and prepend the `WINEDLLOVERRIDES="winhttp=n,b"` part (space before `%command%`, no semicolon).

**What it means:**
- `winhttp=n,b` — load `winhttp` as **native first, then built-in**. Wine reads this and loads BepInEx's local `winhttp.dll` (which is its hook) instead of Wine's own.
- `%command%` — Steam's placeholder for the game's launch command. Must remain on the same line.
- The quotes around `"winhttp=n,b"` matter; Steam parses the launch options field as a shell line.

### Steam Deck

Same launch option, same place — Properties → Launch Options works in Gaming mode. If typing the quotes on the Deck's virtual keyboard is awkward, plug in a USB keyboard for this one step, or temporarily switch to Desktop mode.

## 4. First launch

Launch PCBS from Steam (green Play button in Gaming mode / Steam client). Allow any firewall prompt for Steam P2P networking.

On the main menu you should see two new buttons at the top-center: **Host Multiplayer** and **Join Multiplayer**.

If they don't appear, **the WINEDLLOVERRIDES is almost certainly not being applied.** See Troubleshooting below.

## 5. Verify the mod loaded

Open your PCBS folder in a file manager and navigate to `BepInEx/LogOutput.log`. Open it with any text editor (on Steam Deck: Dolphin → right-click → Open With → Kate / Kwrite).

Look for:

```
[Info   :PCBSMultiplayer] PCBS Multiplayer 0.2.0-rc1 loaded. Steam user: <your_name>
```

If the log file exists but no PCBSMultiplayer line appears, BepInEx loaded but the plugin DLL didn't — re-extract the zip. If the log file is missing or empty, the `WINEDLLOVERRIDES` launch option isn't being applied — see Troubleshooting.

## Playing

### Hosting

1. Click **Host Multiplayer** on the main menu.
2. Steam's invite overlay opens — pick a friend and send the invite.
3. When the overlay closes, pick a save from the vanilla main menu.

Your game is authoritative for money and the job board. Your friend's client mirrors your state.

### Joining

1. Accept the Steam invite from your host.
2. If the game doesn't auto-join, click **Join Multiplayer** on the main menu — it consumes the pending invite.
3. Wait for the host to pick a save.

**In `0.2.0-rc1`:** save sync isn't implemented — you and the host both pick the *same* save file from the main menu. Save-bytes transfer arrives in `0.3.0`.

## Troubleshooting

**Main-menu buttons don't appear**
99% of the time this is a missed `WINEDLLOVERRIDES` step. Verify:
1. Launch Options contains exactly `WINEDLLOVERRIDES="winhttp=n,b" %command%` (with quotes, with a space before `%command%`, and `%command%` last).
2. `winhttp.dll` is at the **game root** (next to `PCBS.exe`), *not* inside `BepInEx/`.
3. Re-launch from Steam (not from a shortcut) so launch options take effect.

**`BepInEx/LogOutput.log` doesn't exist**
BepInEx's preloader never ran. Same cause as above — launch option not applied.

**Crash on launch / "application failed to initialize"**
Your Proton version may be too old or incompatible. In Steam: right-click the game → Properties → Compatibility → **Force the use of a specific Steam Play compatibility tool** → pick **Proton 7.0-6** or newer, or **Proton Experimental**.

**Mod loaded but no "Host/Join" buttons**
Check the log for plugin load errors. If you see "type-load" errors, confirm you're using `v0.2.0-rc1` or newer — earlier pre-tag builds had Mono 2018 compat issues.

**"Join Multiplayer" click does nothing**
Works only when you have a pending Steam invite. Ask the host to click Host Multiplayer and invite you via the overlay first.

**"Receiving host's save data…" hangs forever (0.3.0+ only)**
Host may have crashed mid-transfer.

**Version mismatch between host and client**
Both sides need the same mod version. Check `LogOutput.log` on both machines for the version string. Windows and Linux builds of the same mod version are compatible.

## Uninstalling

From the PCBS install folder:

- **Remove the mod only:**
  ```bash
  rm BepInEx/plugins/PCBSMultiplayer.dll
  ```
- **Remove BepInEx entirely (back to vanilla):**
  ```bash
  rm -rf BepInEx/ doorstop_config.ini winhttp.dll
  ```
- **Remove the launch option** — Steam → Properties → Launch Options → delete the `WINEDLLOVERRIDES=…` prefix.

Your save files live under `compatdata` for the Proton prefix (typically `~/.steam/steam/steamapps/compatdata/621060/pfx/drive_c/users/steamuser/AppData/LocalLow/The Irregular Corporation/PC Building Simulator/Saves/<steam_id>/`) and are untouched by the mod (`0.3.0+` writes `mp-<lobby>.binary` there during sessions and cleans up afterward).
