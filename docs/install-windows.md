# Installing PCBS Multiplayer on Windows

Step-by-step install for Windows users. If you're on Linux, the `README.md` root section covers you.

## Requirements

- **PC Building Simulator** (original / 2018) on Steam
- **BepInEx 5.4.21** (x64) — [releases page](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.21)
- Steam running (lobbies are friends-only over Steam P2P)
- Both host and client need the same PCBS version and the same mod version

The mod is **symmetric** — host and client install it the same way.

## 1. Find your PCBS install folder

In Steam:

1. Right-click **PC Building Simulator** in your Library
2. **Manage** → **Browse local files**
3. A File Explorer window opens. This is your PCBS folder. You should see `PCBS.exe` in it.

The full path usually looks like:

```
C:\Program Files (x86)\Steam\steamapps\common\PC Building Simulator\
```

Keep this window open — you'll drop files into it in the next two steps.

## 2. Install BepInEx 5.4.21 (x64)

1. Download `BepInEx_x64_5.4.21.0.zip` from the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.21)
2. Right-click the ZIP → **Properties** → check **Unblock** → **OK**. (Windows flags files from the internet; this prevents "access denied" errors when extracting.)
3. Extract the contents **directly into your PCBS folder** — the one containing `PCBS.exe`. After extracting, the folder should look like this:

```
PC Building Simulator\
├── PCBS.exe
├── PCBS_Data\
├── BepInEx\              ← new
├── doorstop_config.ini   ← new
└── winhttp.dll           ← new
```

4. **Launch PCBS once** from Steam and wait for the main menu. This generates the `BepInEx\plugins\` folder. Quit the game.

If BepInEx didn't load, open `BepInEx\LogOutput.log` with Notepad and look for errors.

## 3. Install the mod

1. Download `PCBSMultiplayer.dll` from the [PCBS-Multiplayer releases page](https://github.com/R3dWolfie/PCBS-Multiplayer/releases)
2. Right-click the DLL → **Properties** → check **Unblock** → **OK**. (Required — Windows will otherwise block unsigned downloaded DLLs at load time, silently.)
3. Move the DLL into:

```
PC Building Simulator\BepInEx\plugins\PCBSMultiplayer.dll
```

## 4. First launch — firewall + Steam overlay

1. Launch PCBS from Steam.
2. On first launch, Windows may pop a **Windows Defender Firewall** dialog asking about network access. Allow both **Private** and **Public** networks. Steam P2P won't work otherwise.
3. Steam overlay must be enabled — check **Steam → Settings → In-Game → Enable the Steam Overlay while in-game**. The mod uses the overlay's invite dialog.

## 5. Verify the mod loaded

On the main menu, look for two new buttons near the top-center: **Host Multiplayer** and **Join Multiplayer**.

If they don't appear:

1. Open `PC Building Simulator\BepInEx\LogOutput.log` with Notepad.
2. Search for `PCBS Multiplayer` — you should see a line like:
   ```
   [Info   :PCBSMultiplayer] PCBS Multiplayer 0.2.0-rc1 loaded. Steam user: <your_name>
   ```
3. If that line is missing, confirm the DLL was unblocked (step 3.2) and that BepInEx itself loaded (search for `BepInEx` near the top of the log).

## Playing

### Hosting

1. Click **Host Multiplayer** on the main menu.
2. Steam's invite overlay opens — pick a friend and send the invite.
3. When the overlay closes, pick a save from the vanilla main menu to start your session.

Your game becomes authoritative for money and the job board. Your friend's client mirrors your state.

### Joining

1. Accept the Steam invite from your host (via the Steam desktop notification or the friends list).
2. If the game doesn't join automatically, click **Join Multiplayer** on the main menu — it consumes the pending invite.
3. Wait for the host to pick a save.

**In `0.2.0-rc1`:** save sync isn't implemented yet — you and the host both need to manually pick the *same* save file from the main menu. Save-bytes transfer arrives in `0.3.0`.

### Panic hotkey

If multiplayer misbehaves in-game, **hold F7 for 3 seconds**. This disconnects the transport and writes `EnableMultiplayer = false` to the mod config so the next launch boots as vanilla single-player. A top-right yellow-on-black status pill shows the countdown.

## Troubleshooting

**Buttons don't appear on the main menu**
Confirm the DLL is at `BepInEx\plugins\PCBSMultiplayer.dll` and that the file was **Unblock**-ed in Properties. Check `BepInEx\LogOutput.log` for a `PCBS Multiplayer … loaded` line.

**"Join Multiplayer" click does nothing**
The button only works when you have a pending Steam invite from your host. Ask your host to click **Host Multiplayer** and invite you via the overlay; then click **Join Multiplayer** again. (In future versions this will show a toast instead of silently no-op'ing.)

**"Receiving host's save data…" hangs forever (0.3.0+ only)**
Host may have crashed or quit mid-transfer. Hold **F7** for 3 seconds to panic-disconnect, then relaunch vanilla.

**Version mismatch between host and client**
Both machines must run the **same mod version** and the **same PCBS build**. Mismatched versions will refuse to form a session. Check the version string in `LogOutput.log` on both machines.

**SmartScreen / antivirus flags the DLL**
The mod isn't code-signed. If your AV quarantines `PCBSMultiplayer.dll`, you'll need to add an exclusion for the `BepInEx\plugins\` folder. Source and build instructions are in the repository README if you'd prefer to build it yourself.

## Uninstalling

- **Remove the mod only:** delete `BepInEx\plugins\PCBSMultiplayer.dll`.
- **Remove BepInEx entirely (back to vanilla):** delete `BepInEx\`, `doorstop_config.ini`, and `winhttp.dll` from the PCBS folder.

Your save files are stored by PCBS under `%USERPROFILE%\AppData\LocalLow\The Irregular Corporation\PC Building Simulator\Saves\<your_steam_id>\` and are untouched by the mod (except that `0.3.0+` writes `mp-<lobby>.binary` save files during multiplayer sessions and cleans them up afterward).
