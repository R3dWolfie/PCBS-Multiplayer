#!/usr/bin/env bash
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
PCBS="${PCBS_GAME_DIR:-/run/media/system/Storage/Steam/steamapps/common/PC Building Simulator}"
PLUGINS="$PCBS/BepInEx/plugins"

echo "Building Release DLL..."
cd "$REPO"
dotnet build -c Release

echo "Deploying to $PLUGINS"
mkdir -p "$PLUGINS"
cp "$REPO/src/PCBSMultiplayer/bin/Release/net46/PCBSMultiplayer.dll" "$PLUGINS/"

if [[ -f "$REPO/src/PCBSMultiplayer/bin/Release/net46/System.ValueTuple.dll" ]]; then
    cp "$REPO/src/PCBSMultiplayer/bin/Release/net46/System.ValueTuple.dll" "$PLUGINS/"
fi

echo "Done. Launch PCBS with: WINEDLLOVERRIDES=\"winhttp=n,b\" %command%"
