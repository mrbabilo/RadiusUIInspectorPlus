#!/bin/zsh
# Shared settings for the in-game test scripts. Sourced, not run.

TESTS=$(cd "$(dirname "$0")" && pwd)
GAME="/Volumes/BABILOGAMES/JEUX EN COURS/RimWorld.app"
MODS="$GAME/Mods"
EXE="$GAME/Contents/MacOS/RimWorld by Ludeon Studios"
TESTMOD="$MODS/RadiusUIInspectorPlusTests"
# Work folder: sandbox save data (-savedatafolder), logs, results, scene command files.
WORK=${RUITEST_WORK:-${TMPDIR:-/tmp}/ruitest}
SANDBOX="$WORK/sandbox"
PLIST_BAK="$WORK/ludeon.rimworld.plist.bak"

[ -d "$MODS" ] || { echo "Mods folder not found (BABILOGAMES volume not mounted?): $MODS" >&2; exit 1; }
mkdir -p "$SANDBOX/Config"

# Unity keeps the window size and fullscreen flag in the player's own preferences, shared with their game:
# save them once, put them back after every run.
[ -f "$PLIST_BAK" ] || defaults export ludeon.rimworld "$PLIST_BAK"

restore_prefs() {
	defaults import ludeon.rimworld "$PLIST_BAK"
}

# Builds the test mod against the installed Inspector Plus and copies it into Mods for this run.
install_testmod() {
	dotnet build "$TESTS/InspectorPlusTests/Source/RUIInspectorTests.csproj" -c Release -nologo -v q \
		-o "$WORK/build" -p:BaseIntermediateOutputPath="$WORK/obj/" >/dev/null || { echo "test mod build failed" >&2; exit 1; }
	rm -rf "$TESTMOD"
	mkdir -p "$TESTMOD/Assemblies"
	cp -R "$TESTS/InspectorPlusTests/About" "$TESTMOD/"
	cp "$WORK/build/RUIInspectorTests.dll" "$TESTMOD/Assemblies/"
}

remove_testmod() {
	rm -rf "$TESTMOD"
}

# write_modsconfig "<extra package ids>": Harmony, Core, DLCs named in the list, the framework, other mods named
# in the list, Inspector Plus and the test mod, in that load order.
write_modsconfig() {
	local mods="<li>brrainz.harmony</li><li>ludeon.rimworld</li>" m
	for m in ${=1}; do case $m in ludeon.*) mods="$mods<li>$m</li>";; esac; done
	mods="$mods<li>astryl.radiusui.framework</li>"
	for m in ${=1}; do case $m in ludeon.*) ;; *) mods="$mods<li>$m</li>";; esac; done
	mods="$mods<li>mrbabilo.radiusui.inspectorplus</li><li>mrbabilo.radiusui.inspectorplus.tests</li>"
	cat > "$SANDBOX/Config/ModsConfig.xml" <<XML
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData><version>1.6.4633 rev1253</version><activeMods>$mods</activeMods>
<knownExpansions><li>ludeon.rimworld</li><li>ludeon.rimworld.royalty</li><li>ludeon.rimworld.ideology</li><li>ludeon.rimworld.biotech</li><li>ludeon.rimworld.anomaly</li><li>ludeon.rimworld.odyssey</li></knownExpansions></ModsConfigData>
XML
}

# Waits for the game process to exit, killing it after the given number of seconds.
wait_exit() {
	local pid=$1 limit=$2 i
	for i in $(seq 1 $limit); do kill -0 $pid 2>/dev/null || return 0; sleep 1; done
	kill $pid 2>/dev/null
	sleep 2
}
