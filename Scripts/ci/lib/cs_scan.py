#!/usr/bin/env python3
"""C# 소스에서 주석·문자열을 나눠 보는 작은 도구(검사 스크립트 공용).

사용: cs_scan.py <모드> <파일>...
  파일이 "-" 하나면 표준입력에서 한 줄에 하나씩 읽는다(윈도우 인자 길이 제한 때문).
모드:
  code      주석을 지우고 문자열·문자 리터럴의 내용도 비운 코드 (따옴표만 남는다)
  nocomment 주석만 지운 코드 (문자열은 그대로)
출력 한 줄: <파일>:<줄>:<내용>. 줄 번호는 원본과 같고, 내용이 빈 줄은 내지 않는다.
"""
import sys


def scan(text, mode):
    """(줄 번호, 내용) 목록을 돌려 준다."""
    out = []
    line = 1
    cur = []
    i, n = 0, len(text)

    def flush():
        nonlocal cur
        out.append((line, "".join(cur)))
        cur = []

    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if c == "\n":
            flush()
            line += 1
            i += 1
        elif c == "/" and nxt == "/":  # 한 줄 주석
            while i < n and text[i] != "\n":
                i += 1
        elif c == "/" and nxt == "*":  # 블록 주석(줄 수는 유지)
            i += 2
            while i < n and not (text[i] == "*" and i + 1 < n and text[i + 1] == "/"):
                if text[i] == "\n":
                    flush()
                    line += 1
                i += 1
            i += 2
        elif c in ('"', "'") or (c in "@$" and nxt in ('"', "$", "@")):
            j = i
            verbatim = False
            while j < n and text[j] in "@$":  # 접두어 $, @, $@, @$
                if text[j] == "@":
                    verbatim = True
                j += 1
            if j >= n or text[j] not in ('"', "'"):
                cur.append(c)
                i += 1
                continue
            quote = text[j]
            prefix = text[i:j]
            j += 1
            body = []
            while j < n:
                ch = text[j]
                if verbatim and quote == '"':
                    if ch == '"' and j + 1 < n and text[j + 1] == '"':
                        body.append('"')
                        j += 2
                        continue
                elif ch == "\\" and j + 1 < n:
                    body.append(text[j : j + 2])
                    j += 2
                    continue
                if ch == quote:
                    break
                if ch == "\n":
                    if not verbatim:
                        break  # 닫히지 않은 리터럴: 줄 끝에서 멈춘다
                    flush()
                    line += 1
                body.append(ch)
                j += 1
            content = "".join(body)
            if mode == "nocomment":
                cur.append(prefix + quote + content.replace("\n", " ") + quote)
            else:
                cur.append(prefix + quote + quote)
            i = j + 1
        else:
            cur.append(c)
            i += 1
    flush()
    return out


def main(argv):
    if len(argv) < 3 or argv[1] not in ("code", "nocomment"):
        sys.stderr.write(__doc__)
        return 2
    mode = argv[1]
    paths = argv[2:]
    if paths == ["-"]:
        paths = [p.rstrip("\r\n") for p in sys.stdin if p.strip()]
    for path in paths:
        with open(path, encoding="utf-8-sig", newline="") as f:
            text = f.read().replace("\r\n", "\n")
        for ln, t in scan(text, mode):
            if t.strip():
                sys.stdout.write("%s:%d:%s\n" % (path, ln, t))
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", newline="\n")
    sys.exit(main(sys.argv))
