# PCBS Multiplayer Mod

Co-op multiplayer mod for PC Building Simulator (2018).

See `docs/design.md` for the full spec. Plans under `docs/plans/`.

## Build

```
dotnet build
```

## Test

```
dotnet test
```

## Building

This mod compiles against PCBS game assemblies. Set `PCBSGameDir` env var to your install's `PCBS_Data/Managed` directory:

```bash
PCBSGameDir="/path/to/PC Building Simulator/PCBS_Data/Managed" dotnet build
```

The default (dev machine) is `/run/media/system/Storage/Steam/steamapps/common/PC Building Simulator/PCBS_Data/Managed`.

At build time, `BepInEx.AssemblyPublicizer.MSBuild` generates a publicized copy of `Assembly-CSharp-firstpass.dll` in `obj/.../publicized/`. Mod code references private game fields via this compile-time stub; the runtime game loads the vanilla DLL.
