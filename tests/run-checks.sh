#!/bin/zsh
# Automated in-game checks of Inspector Plus.
# Usage: tests/run-checks.sh <name> ["<extra package ids>"] [game args...]
#   tests/run-checks.sh A
#   tests/run-checks.sh B "astryl.radiusui.everywhere astryl.radiusui.healthtab astryl.radiusui.needstab mrbabilo.radiusui.colonistbarplus"
#   tests/run-checks.sh C "ludeon.rimworld.royalty ludeon.rimworld.ideology ludeon.rimworld.biotech ludeon.rimworld.anomaly ludeon.rimworld.odyssey"
#   tests/run-checks.sh D "" -ruinoflatfix
# Starts a separate quick test game (own save data folder), runs every check once, writes PASS/FAIL lines to
# $WORK/<name>.txt, quits. Prints the lines that are not PASS; exit code 1 when a check failed or the run broke.
# Deploy Inspector Plus first (deploy.sh): the checks run against the installed mod.
source "$(dirname "$0")/common.sh"
name=${1:?name}; extra=${2:-}; shift; [ $# -gt 0 ] && shift

results="$WORK/$name.txt"
rm -f "$results"
write_modsconfig "$extra"
install_testmod
"$EXE" -quicktest -ruitest="$results" -savedatafolder="$SANDBOX" -logFile "$WORK/$name.log" \
	-screen-fullscreen 0 -screen-width 1600 -screen-height 1000 "$@" > "$WORK/$name.stdout" 2>&1 &
pid=$!
for i in $(seq 1 120); do grep -q "# END" "$results" 2>/dev/null && break; kill -0 $pid 2>/dev/null || break; sleep 2; done
wait_exit $pid 15
restore_prefs
remove_testmod

echo "== $name ($results)"
grep -v "^PASS" "$results" 2>/dev/null || echo "no results: see $WORK/$name.log"
grep -E "Exception|\[Radius UI[^]]*\] (skipped|Skipped|Falling)" "$WORK/$name.log" | head -5
grep -q "# END" "$results" 2>/dev/null && grep -q " 0 failed" "$results"
