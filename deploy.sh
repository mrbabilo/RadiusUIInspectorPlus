#!/bin/sh
# Build RadiusUIInspector and publish this folder (minus sources) to RimWorld's Mods folder.
set -eu

HERE=$(cd "$(dirname "$0")" && pwd)
GAME="/Volumes/BABILOGAMES/JEUX EN COURS/RimWorld.app"
MODS="$GAME/Mods"
DEST="$MODS/RadiusUIInspectorPlus"

[ -d "$MODS" ] || { echo "Dossier Mods du jeu introuvable (volume BABILOGAMES non monté ?) : $MODS" >&2; exit 1; }

# Build out of tree so Source/ never gets bin/ or obj/.
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
dotnet build "$HERE/Source/RadiusUIInspector.csproj" -c Release -nologo -v q \
	-o "$TMP/build" -p:BaseIntermediateOutputPath="$TMP/obj/"

cp "$TMP/build/RadiusUIInspector.dll" "$HERE/Assemblies/"
rsync -a --delete --exclude Source/ --exclude docs/ --exclude tests/ --exclude deploy.sh --exclude .DS_Store --exclude .git/ --exclude .gitignore --exclude .gitkeep "$HERE/" "$DEST/"
echo "Publié : $DEST"
