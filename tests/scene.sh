#!/bin/zsh
# Sends one command to the running measurement scene and waits for its ack.
# Verbs: select pawn|pawns|pawns3|bed|zone|shelf|none, tab storage|health|gear|needs|none, materials, closeall,
# speed <0-4>, shot <file.png>, quit,
# fluency pawn|health|pawns|zone|shelf [frames] [vanillafirst]: smoothness probe, result in $WORK/fluency-<scene>.txt (see README).
WORK=${RUITEST_WORK:-${TMPDIR:-/tmp}/ruitest}
id=$(date +%s%N)
echo "$id $*" > "$WORK/scene.txt"
for i in $(seq 1 150); do
	[ -f "$WORK/scene-ack.txt" ] && a=$(cat "$WORK/scene-ack.txt") && [ "${a%% *}" = "$id" ] && { echo "${a#* }"; exit 0; }
	sleep 0.2
done
echo "TIMEOUT ($*)"; exit 1
