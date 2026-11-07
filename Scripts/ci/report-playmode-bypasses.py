"""Lists PlayMode tests that reach their result through shortcuts instead of real player input.

Usage: python Scripts/ci/report-playmode-bypasses.py [--tsv out.tsv]

A shortcut is fine as setup, but a test whose checks depend on one cannot be reported as
"verified by playing" (.claude/rules/core-logic-and-play-evidence.md). This is a report, not a gate:
read the flagged lines and decide whether each shortcut is setup or the thing being verified.
"""
import io
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
TESTS = os.path.join(ROOT, 'Assets', 'Tests', 'PlayMode')

PATTERNS = [
    ('scene-load', re.compile(r'SceneManager\.LoadScene(Async)?\s*\(')),
    ('configure-for-tests', re.compile(r'\.ConfigureForTests\s*\(')),
    ('direct-interact', re.compile(r'\.(TryInteract|TryInteractWithNearest|Interact)\s*\(')),
    ('execute-events', re.compile(r'ExecuteEvents\.Execute\s*\(')),
    ('onclick-invoke', re.compile(r'onClick\.Invoke\s*\(')),
    ('field-text-set', re.compile(r'\b(InputField|TMP_InputField)>?\(\)\.text\s*=|\.text\s*=\s*"[^"]*"\s*;')),
    ('teleport', re.compile(r'\.transform\.position\s*=')),
    ('force-time-or-day', re.compile(r'\b(SetStudentDay|AdvanceDay|SetTimeMinutes|ForceDay|SetDayForTests)\s*\(')),
    ('direct-apply', re.compile(r'\.Apply\s*\(\s*\d')),
]

TEST_METHOD = re.compile(r'public\s+(?:IEnumerator|void)\s+(\w+)\s*\(')
IGNORED = re.compile(r'\[Ignore\(')


def scan(path):
    rows = []
    current = None
    ignored_next = False
    with io.open(path, encoding='utf-8-sig', errors='replace') as handle:
        for number, line in enumerate(handle, 1):
            if IGNORED.search(line):
                ignored_next = True
            match = TEST_METHOD.search(line)
            if match:
                current = None if ignored_next else match.group(1)
                ignored_next = False
                continue
            if current is None or line.strip().startswith('//'):
                continue
            for kind, pattern in PATTERNS:
                if pattern.search(line):
                    rows.append((os.path.relpath(path, ROOT).replace('\\', '/'), number, current, kind, line.strip()[:160]))
    return rows


def main():
    rows = []
    for folder, _, files in os.walk(TESTS):
        for name in files:
            if name.endswith('.cs'):
                rows.extend(scan(os.path.join(folder, name)))
    rows.sort()
    out = sys.stdout
    if '--tsv' in sys.argv:
        out = io.open(sys.argv[sys.argv.index('--tsv') + 1], 'w', encoding='utf-8')
    for row in rows:
        out.write('\t'.join(str(value) for value in row) + '\n')
    summary = {}
    for row in rows:
        summary[row[3]] = summary.get(row[3], 0) + 1
    methods = len({(row[0], row[2]) for row in rows})
    sys.stderr.write('flagged lines: %d in %d active test methods\n' % (len(rows), methods))
    for kind, count in sorted(summary.items(), key=lambda item: -item[1]):
        sys.stderr.write('  %-20s %d\n' % (kind, count))


if __name__ == '__main__':
    main()
