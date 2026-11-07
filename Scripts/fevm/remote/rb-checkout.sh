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
# Seed Library from the newest earlier checkout to save the first import (Library is only a cache).
PREV=$(ls -1dt "$RB_HOME"/*/Library 2>/dev/null | grep -v "^$D/" | head -1 || true)
if [ -n "$PREV" ]; then cp -a --reflink=auto "$PREV" "$D/Library"; echo "seeded Library from $PREV"; fi
echo "checked out $GOT at $D"
