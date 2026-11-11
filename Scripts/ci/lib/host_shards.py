#!/usr/bin/env python3
"""Host 테스트 묶음(CI 매트릭스) 정의를 읽고 검사하고 dotnet test 필터를 만든다.

사용: host_shards.py <명령> [묶음]
  check           정의가 겹치거나 비지 않는지, 정의의 클래스가 모두 있는지 본다. 위반이면 종료 코드 1.
  filter <묶음>   그 묶음의 dotnet test --filter 식을 낸다. 나머지 묶음(*)은 다른 묶음에 없는 전부.
  names           묶음 이름을 정의 순서대로 JSON 배열로 낸다(매트릭스용).
  list            클래스마다 어느 묶음에 드는지 표로 낸다.
환경 변수: HOST_TESTS_DIR(기본 game/tests/Rootborn.Host.Tests), HOST_SHARDS(기본 Scripts/ci/host-test-shards.txt),
  CI_WORKFLOW(기본 .github/workflows/ci.yml — 있으면 그 안의 'shard: [...]' 목록이 묶음 이름과 같은지 본다). 경로는 저장소 루트 기준.

정의 파일 형식(# 뒤는 주석):
  <묶음 이름>: <클래스 이름> <클래스 이름> ...    (이름은 소문자·숫자·하이픈. 클래스는 네임스페이스 없이)
  <묶음 이름>: *                                (나머지 전부. 반드시 하나, 맨 끝)
  줄 끝의 \\ 로 다음 줄에 이어 쓴다.
"""
import json
import os
import re
import sys

sys.dont_write_bytecode = True  # 저장소에 __pycache__ 를 남기지 않는다
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cs_scan import scan  # noqa: E402

TEST_ATTR = re.compile(r"\[\s*(?:NUnit\.Framework\.)?(Test|TestCase|TestCaseSource)\b")
NAME = re.compile(r"^[a-z0-9][a-z0-9-]*$")


def fail(msg):
    print("FAIL: " + msg, file=sys.stderr)
    sys.exit(1)


def fixtures(tests_dir):
    """테스트 메서드를 가진 클래스: {이름: (완전한 이름, 파일)}. 중첩 클래스 안의 테스트는 오류(필터가 어긋난다)."""
    found = {}
    errors = []
    for base, dirs, files in os.walk(tests_dir):
        dirs[:] = sorted(d for d in dirs if d not in ("obj", "bin"))
        for f in sorted(files):
            if not f.endswith(".cs"):
                continue
            path = os.path.join(base, f).replace("\\", "/")
            with open(path, encoding="utf-8-sig") as fh:
                code = "\n".join(text for _, text in scan(fh.read(), "code"))
            # 중괄호 깊이로 네임스페이스·클래스를 따라간다.
            stack = []  # (종류, 이름, 여는 깊이)
            depth = 0
            pending = None  # 다음 { 가 여는 것
            for tok in re.finditer(r"\bnamespace\s+([\w.]+)\s*;?|\b(?:class|struct|record)\s+(\w+)|[{}]|\[[^\]]*\]", code):
                t = tok.group(0)
                if tok.group(1):
                    if t.rstrip().endswith(";"):
                        stack.append(("ns", tok.group(1), -1))
                    else:
                        pending = ("ns", tok.group(1))
                elif tok.group(2):
                    pending = ("class", tok.group(2))
                elif t == "{":
                    depth += 1
                    if pending:
                        stack.append((pending[0], pending[1], depth))
                        pending = None
                elif t == "}":
                    while stack and stack[-1][2] == depth:
                        stack.pop()
                    depth -= 1
                elif TEST_ATTR.match(t):
                    classes = [s for s in stack if s[0] == "class"]
                    if not classes:
                        continue
                    if len(classes) > 1:
                        errors.append(path + ": 중첩 클래스 " + "+".join(c[1] for c in classes) + " 안의 테스트는 묶음 필터가 다루지 않는다")
                        continue
                    ns = ".".join(s[1] for s in stack if s[0] == "ns")
                    name = classes[0][1]
                    fqn = (ns + "." if ns else "") + name
                    if name in found and found[name][0] != fqn:
                        errors.append("클래스 이름이 겹친다: " + found[name][0] + ", " + fqn + " (묶음 정의는 이름만 쓴다)")
                    found.setdefault(name, (fqn, path))
    return found, errors


def read_shards(path):
    if not os.path.isfile(path):
        fail(path + " 가 없다")
    shards = []  # (이름, [클래스] 또는 None=나머지, 줄)
    errors = []
    with open(path, encoding="utf-8-sig") as fh:
        raw = fh.read().replace("\\\r\n", " ").replace("\\\n", " ").splitlines()
    for i, line in enumerate(raw, 1):
        line = line.split("#", 1)[0].strip()
        if not line:
            continue
        if ":" not in line:
            errors.append("%s:%d: '<묶음>: <클래스...>' 형식이 아니다" % (path, i))
            continue
        name, rest = line.split(":", 1)
        name, items = name.strip(), rest.split()
        if not NAME.match(name):
            errors.append("%s:%d: 묶음 이름 '%s'는 소문자·숫자·하이픈만" % (path, i, name))
        if any(s[0] == name for s in shards):
            errors.append("%s:%d: 묶음 이름 '%s'가 두 번 나온다" % (path, i, name))
        if items == ["*"]:
            shards.append((name, None, i))
        elif "*" in items:
            errors.append("%s:%d: '*'는 혼자 써야 한다(나머지 묶음)" % (path, i))
        elif not items:
            errors.append("%s:%d: 묶음 '%s'가 비었다" % (path, i, name))
        else:
            shards.append((name, items, i))
    rests = [s for s in shards if s[1] is None]
    if len(rests) != 1:
        errors.append(path + ": 나머지 묶음('<이름>: *')이 정확히 하나여야 한다 (지금 %d개)" % len(rests))
    elif shards[-1][1] is not None:
        errors.append(path + ": 나머지 묶음은 맨 끝에 둔다")
    return shards, errors


def assign(shards, found):
    """클래스 → 묶음. 겹침·없는 클래스는 오류."""
    owner, errors = {}, []
    for name, items, line in shards:
        if items is None:
            continue
        for c in items:
            if c not in found:
                errors.append("묶음 '%s'(줄 %d)의 클래스 %s가 테스트 폴더에 없다(이름이 바뀌었거나 지워졌다)" % (name, line, c))
            elif c in owner:
                errors.append("클래스 %s가 묶음 '%s'와 '%s'에 겹친다" % (c, owner[c], name))
            else:
                owner[c] = name
    rest = next((s[0] for s in shards if s[1] is None), None)
    for c in found:
        owner.setdefault(c, rest)
    return owner, errors


def collisions(found):
    """필터는 '완전한 이름.' 포함으로 고른다. 한 클래스의 접두가 다른 클래스 이름 안에 들어 있으면 둘이 함께 골린다."""
    errors = []
    names = sorted(v[0] for v in found.values())
    for a in names:
        for b in names:
            if a != b and (a + ".") in (b + "."):
                errors.append("필터가 %s 를 고르면 %s 도 골린다(이름을 바꾼다)" % (a, b))
    return errors


def workflow_matrix(path, shards):
    """워크플로의 matrix 'shard: [a, b]' 가 묶음 이름과 같은 순서·같은 이름인가(파일이 없으면 보지 않는다)."""
    if not os.path.isfile(path):
        return []
    with open(path, encoding="utf-8-sig") as fh:
        m = re.search(r"^\s*shard:\s*\[([^\]]*)\]", fh.read(), re.M)
    if not m:
        return [path + ": matrix 'shard: [...]' 를 찾지 못했다"]
    listed = [x.strip().strip("'\"") for x in m.group(1).split(",") if x.strip()]
    names = [s[0] for s in shards]
    if listed != names:
        return ["%s 의 matrix shard %s 가 묶음 정의 %s 와 다르다" % (path, listed, names)]
    return []


def term(fqn, negate=False):
    return "FullyQualifiedName" + ("!~" if negate else "~") + fqn + "."


def main(argv):
    if len(argv) < 2:
        print(__doc__, file=sys.stderr)
        return 2
    cmd = argv[1]
    tests_dir = os.environ.get("HOST_TESTS_DIR", "game/tests/Rootborn.Host.Tests")
    shards_path = os.environ.get("HOST_SHARDS", "Scripts/ci/host-test-shards.txt")
    if not os.path.isdir(tests_dir):
        fail(tests_dir + " 폴더가 없다")
    found, errors = fixtures(tests_dir)
    shards, e2 = read_shards(shards_path)
    errors += e2
    owner, e3 = assign(shards, found) if not e2 else ({}, [])
    errors += e3 + collisions(found)
    if not e2:
        errors += workflow_matrix(os.environ.get("CI_WORKFLOW", ".github/workflows/ci.yml"), shards)
    if not found:
        errors.append(tests_dir + " 에 테스트 클래스가 없다")

    if cmd == "check":
        for e in errors:
            print(e)
        if errors:
            print("FAIL: Host 테스트 묶음 정의 위반 %d건 (%s)" % (len(errors), shards_path), file=sys.stderr)
            return 1
        counts = {s[0]: sum(1 for c in owner if owner[c] == s[0]) for s in shards}
        print("OK: Host 테스트 클래스 %d개가 묶음 %d개에 하나씩 든다 (%s)" % (
            len(found), len(shards), ", ".join("%s %d" % (k, v) for k, v in counts.items())))
        return 0
    if errors:
        for e in errors:
            print(e, file=sys.stderr)
        fail("묶음 정의를 먼저 고친다(check)")
    if cmd == "names":
        print(json.dumps([s[0] for s in shards]))
        return 0
    if cmd == "list":
        for c in sorted(found, key=lambda c: (owner[c], c)):
            print("%s\t%s\t%s" % (owner[c], found[c][0], found[c][1]))
        return 0
    if cmd == "filter":
        if len(argv) < 3:
            fail("filter 뒤에 묶음 이름")
        shard = next((s for s in shards if s[0] == argv[2]), None)
        if shard is None:
            fail("묶음 '%s'가 없다. 있는 묶음: %s" % (argv[2], ", ".join(s[0] for s in shards)))
        if shard[1] is None:
            listed = sorted(found[c][0] for c in owner if owner[c] != shard[0])
            print("&".join(term(f, negate=True) for f in listed) or "FullyQualifiedName~.")
        else:
            print("|".join(term(found[c][0]) for c in shard[1]))
        return 0
    print("모르는 명령: " + cmd, file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
