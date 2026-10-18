#!/bin/zsh
# Measurement scene for Dubs Performance Analyzer (Workshop 2038874626, must be installed).
# Usage: tests/run-scene.sh [game args...]   e.g. tests/run-scene.sh -ruinoflatfix
# Starts a fullscreen quick test game with DPA, builds the scene (20 colonists, a full stockpile, shelves, a bed),
# then waits for commands sent with tests/scene.sh. Returns when the game exits ("tests/scene.sh quit").
# DPA's settings for the sandbox come from tests/dpa/: single-threaded patching, no cleanup, Inspector Plus
# methods saved in Custom Update. The player's own DPA settings are not touched.
source "$(dirname "$0")/common.sh"

write_modsconfig "dubwise.dubsperformanceanalyzer.steam"
cp "$TESTS/dpa/Mod_2038874626_Modbase.xml" "$SANDBOX/Config/"
install_testmod
rm -f "$WORK/scene.txt" "$WORK/scene-ack.txt"
"$EXE" -quicktest -ruiscene="$WORK" -savedatafolder="$SANDBOX" -logFile "$WORK/scene.log" -screen-fullscreen 1 "$@" > "$WORK/scene.stdout" 2>&1 &
pid=$!
for i in $(seq 1 90); do [ -f "$WORK/scene-ack.txt" ] && break; sleep 2; done
cat "$WORK/scene-ack.txt" 2>/dev/null; echo
wait_exit $pid 86400
restore_prefs
remove_testmod
echo "scene closed"
