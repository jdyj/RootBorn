#!/bin/bash
# Usage: rb-checkout.sh <bundle-file> <commit-hash>
# Checks the commit out into ~/farmer-unity-verify/<short hash>, then verifies the hash.
set -eu
. ~/farmer-unity-verify/tools/rb-env.sh
B=$1; H=$2
git bundle list-heads "$B" >/dev/null
S=$(git -C "$RB_HOME" rev-parse --short=9 "$H" 2>/dev/null || echo "${H:0:9}")
D=$RB_HOME/$S
if [ -e "$D" ]; then echo "exists: $D"; exit 1; fi
git clone --no-checkout -q "$B" "$D"
git -C "$D" checkout -q --detach "$H"
GOT=$(git -C "$D" rev-parse HEAD)
case "$GOT" in "$H"*) ;; *) echo "hash mismatch: $GOT vs $H"; exit 1;; esac
# Seed Library from the newest earlier checkout that no Unity is using (Library is only a cache).
PREV=""
for L in $(ls -1dt "$RB_HOME"/*/Library 2>/dev/null); do
  case "$L" in "$D/"*) continue;; esac
  if rb_project_busy "${L%/Library}"; then echo "skip Library seed: Unity is using ${L%/Library}"; continue; fi
  PREV=$L; break
done
if [ -n "$PREV" ]; then cp -a --reflink=auto "$PREV" "$D/Library"; echo "seeded Library from $PREV"; fi
echo "checked out $GOT at $D"
