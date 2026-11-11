#!/usr/bin/env bash
set -euo pipefail
T_NAME=meta-files
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
C=Packages/com.rootborn.core/Runtime

new_root; put $C/A.cs 'class A {}'; put $C/A.cs.meta 'x'; put $C/Sub/B.cs 'class B {}'; put $C/Sub/B.cs.meta 'x'; put $C/Sub.meta 'x'; put Packages/com.rootborn.core/Runtime.meta x
run_check check-meta-files.sh; expect ".cs와 폴더마다 .meta가 있으면 통과" pass

new_root; put $C/A.cs 'class A {}'; put Packages/com.rootborn.core/Runtime.meta x
run_check check-meta-files.sh; expect ".cs의 .meta가 없으면 걸림" fail "A.cs.meta"

new_root; put $C/A.cs 'class A {}'; put $C/A.cs.meta 'x'; put $C/Sub/B.cs 'class B {}'; put $C/Sub/B.cs.meta 'x'; put Packages/com.rootborn.core/Runtime.meta x
run_check check-meta-files.sh; expect "폴더의 .meta가 없으면 걸림" fail "Sub.meta"

new_root; run_check check-meta-files.sh; expect "Core 폴더가 없으면 실패" fail
finish
