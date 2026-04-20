<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/pcbs-m-social-dark.png">
    <source media="(prefers-color-scheme: light)" srcset="assets/pcbs-m-social-light.png">
    <img alt="PCBS Multiplayer" src="assets/pcbs-m-social-dark.png" width="640">
  </picture>
</p>

# PCBS Multiplayer Mod

Co-op multiplayer mod for **PC Building Simulator** (2018, Steam AppID 621060).

**Status:** `0.2.0-rc1` — alpha. Shared money + job board sync on two machines; full save-sync join loop is in active development for `0.3.0-alpha`. See [CHANGELOG.md](CHANGELOG.md).

---

## Requirements

- **PC Building Simulator** (original / 2018), Steam version
- **BepInEx 5.4.21** (x64)
- Both host and client need:
  - The same game version
  - The same mod version
  - Steam running (friends-only lobbies over Steam P2P)

## Install (host and client — same steps)

The mod is symmetric: host and client install it the same way.

**1. Install BepInEx 5.4.21 (x64)**

Download from <https://github.com/BepInEx/BepInEx/releases> (pick `BepInEx_x64_5.4.21.0.zip` or newer 5.4.x).

Extract the archive directly into your PCBS install folder — the one containing `PCBS.exe`:

```
steamapps/common/PC Building Simulator/
├── PCBS.exe
├── PCBS_Data/
├── BepInEx/          ← new
├── doorstop_config.ini  ← new
└── winhttp.dll       ← new
```

Launch PCBS once so BepInEx generates its `BepInEx/plugins/` folder, then quit.

**2. Install the mod**

Grab `PCBSMultiplayer.dll` from the [Releases page](https://github.com/R3dWolfie/PCBS-Multiplayer/releases) and drop it into:

```
steamapps/common/PC Building Simulator/BepInEx/plugins/PCBSMultiplayer.dll
```

That's it. Both host and client repeat these steps on their own machines.

**3. Verify**

Launch PCBS. On the main menu you should see two new buttons near the top: **Host Multiplayer** and **Join Multiplayer**. If they don't show up, check `BepInEx/LogOutput.log` for load errors.

## Playing

### Host

1. Click **Host Multiplayer** on the main menu.
2. Steam's invite overlay opens — invite your friend.
3. Pick a save file from the vanilla main menu to start the session.

Your game becomes authoritative: money, XP, job board, and (in `0.3.0+`) inventory / shop / bench all live on your machine. Your friend's client mirrors your state.

### Client (joining)

1. Accept the Steam invite from your host.
2. Click **Join Multiplayer** on the main menu (if the game isn't already joining automatically).
3. Wait for the host to start — the session loads you into the host's scene.

**In `0.2.0-rc1`:** you and the host both need to pick the *same* save file from the main menu — there's no save transfer yet. Upgrade to `0.3.0-alpha` when it ships for automatic save-bytes transfer.

### Panic hotkey

If multiplayer misbehaves, **hold F7 for 3 seconds**. This disconnects the transport and writes `EnableMultiplayer = false` to the mod config so the next launch boots as vanilla single-player.

## Uninstalling

Delete `BepInEx/plugins/PCBSMultiplayer.dll`. To remove BepInEx entirely, also delete `BepInEx/`, `doorstop_config.ini`, and `winhttp.dll` from the game root.

## Building from source

```bash
PCBSGameDir="/path/to/PC Building Simulator/PCBS_Data/Managed" dotnet build -c Release
```

The default (dev machine) is `/run/media/system/Storage/Steam/steamapps/common/PC Building Simulator/PCBS_Data/Managed`.

At build time, `BepInEx.AssemblyPublicizer.MSBuild` generates a publicized copy of `Assembly-CSharp-firstpass.dll` in `obj/.../publicized/`. Mod code references private game fields via this compile-time stub; the runtime game loads the vanilla DLL.

Tests:

```bash
dotnet test
```

Deploy to your local PCBS install (Linux dev loop):

```bash
scripts/deploy-to-game.sh
```

## Project docs

- [CHANGELOG.md](CHANGELOG.md) — release notes
- [docs/manual-tests.md](docs/manual-tests.md) — playtest log
- [docs/pcbs-recon.md](docs/pcbs-recon.md) — game-side reverse-engineering notes
