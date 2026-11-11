#!/usr/bin/env bash
set -euo pipefail
T_NAME=monogame-references
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
PKG='<Project><ItemGroup><PackageReference Include="MonoGame.Framework.DesktopGL" Version="[3.8.6.1]" /></ItemGroup></Project>'

new_root; put game/Rootborn.Host/H.csproj "$PKG"; put game/Rootborn.Host/G.cs 'using Microsoft.Xna.Framework; class G {}'
put game/tests/Rootborn.Host.Tests/T.csproj "$PKG"; put game/tests/Rootborn.Host.Tests/T.cs 'using Microsoft.Xna.Framework.Graphics; class T {}'
put game/Rootborn.Screens/S.cs 'class S {}'
run_check check-monogame-references.sh; expect "Host와 Host.Tests 안의 참조는 통과" pass

new_root; put game/Rootborn.Screens/S.cs 'using Microsoft.Xna.Framework; class S {}'
run_check check-monogame-references.sh; expect "Screens의 using은 걸림" fail "game/Rootborn.Screens/S.cs:1"

new_root; put game/Rootborn.Ui/U.csproj "$PKG"
run_check check-monogame-references.sh; expect "다른 csproj의 PackageReference는 걸림" fail "U.csproj"

new_root; put game/Rootborn.World/W.cs 'class W { Microsoft.Xna.Framework.Vector2 v; }'
run_check check-monogame-references.sh; expect "완전한 이름도 걸림" fail

new_root; put game/Rootborn.Core/src/C.cs '// Microsoft.Xna 이야기
class C { string s = "Microsoft.Xna"; }'
run_check check-monogame-references.sh; expect "주석·문자열 안은 무시" pass
finish
