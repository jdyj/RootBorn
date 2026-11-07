#!/bin/bash
# Windows (Git Bash) -> FEVM: bundle a committed hash, upload it, check it out in ~/farmer-unity-verify/<short hash>.
# Usage: Scripts/fevm/sync.sh [commit]   (default HEAD)
# Env: FEVM_HOST (default jdyj@192.168.45.163), FEVM_KEY (default ~/.ssh/fevm_setup_ed25519). IP can change; check with whoami; hostname.
set -eu
HOST=${FEVM_HOST:-jdyj@192.168.45.163}
KEY=${FEVM_KEY:-$HOME/.ssh/fevm_setup_ed25519}
O=(-i "$KEY" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes)
REPO=$(git rev-parse --show-toplevel)
H=$(git -C "$REPO" rev-parse "${1:-HEAD}")
S=${H:0:9}
echo "commit $H"
git -C "$REPO" status --short | grep -q . && echo "note: uncommitted changes exist locally and are NOT part of this verification"
TMP=$(mktemp -d); B=$TMP/rootborn-$S.bundle
git -C "$REPO" bundle create "$B" "$H" main 2>/dev/null || git -C "$REPO" bundle create "$B" "$H"
ssh "${O[@]}" "$HOST" 'mkdir -p ~/farmer-unity-verify/bundles ~/farmer-unity-verify/tools'
scp "${O[@]}" -q "$REPO"/Scripts/fevm/remote/rb-*.sh "$HOST":farmer-unity-verify/tools/
scp "${O[@]}" -q "$B" "$HOST":farmer-unity-verify/bundles/
ssh "${O[@]}" "$HOST" "chmod +x ~/farmer-unity-verify/tools/rb-*.sh && ~/farmer-unity-verify/tools/rb-checkout.sh ~/farmer-unity-verify/bundles/rootborn-$S.bundle $H"
# Read-only test fixtures under output/ that git does not track (list: Scripts/fevm/test-fixtures.txt).
FX=$(grep -vE '^(#|$)' "$REPO/Scripts/fevm/test-fixtures.txt" | while read -r d; do [ -e "$REPO/$d" ] && echo "$d"; done)
if [ -n "$FX" ]; then
  (cd "$REPO" && tar -cf - $FX) | ssh "${O[@]}" "$HOST" "tar -xf - -C ~/farmer-unity-verify/$S"
  echo "copied test fixtures: $(echo $FX | wc -w) folders"
fi
rm -rf "$TMP"
echo "next: ssh ... '~/farmer-unity-verify/tools/rb-test.sh ~/farmer-unity-verify/$S EditMode'"
