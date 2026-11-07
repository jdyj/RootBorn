#!/bin/bash
# Usage: rb-editor.sh <project-dir> [start|stop|status]
# Runs the interactive Unity editor on Xvfb :99 + VirtualGL GPU so the AI Game Developer MCP plugin can serve
# http://localhost:22394 on FEVM (reach it from the PC through Scripts/fevm/mcp-tunnel.sh).
set -u
. ~/farmer-unity-verify/tools/rb-env.sh
P=$(cd "$1" && pwd); CMD=${2:-start}
export RB_DISPLAY=:99
C=$P/UserSettings/AI-Game-Developer-Config.json
case "$CMD" in
  stop)
    pkill -TERM -f "[E]ditor/Unity.*-projectPath $P( |$)"; sleep 20
    pkill -KILL -f "[E]ditor/Unity.*-projectPath $P( |$)" 2>/dev/null; echo stopped; exit 0;;
  status)
    pgrep -af "[E]ditor/Unity.*-projectPath $P( |$)" | cut -c1-120 || echo "editor not running"
    ss -ltn | grep -q ":22394 " && echo "mcp port 22394 listening" || echo "mcp port 22394 closed"; exit 0;;
esac
if rb_project_busy "$P"; then echo "Unity already running on $P"; exit 1; fi
# Same connection settings as the PC editor: local server (Custom), streamable HTTP, port 22394, no auth.
# Other keys keep the plugin defaults; a token, if any, is left to the plugin and never printed.
mkdir -p "$P/UserSettings"
python3 - "$C" <<'PY'
import json, os, sys
p = sys.argv[1]
d = json.load(open(p)) if os.path.exists(p) else {}
d.update({"host": "http://localhost:22394", "connectionMode": "Custom", "transportMethod": "streamableHttp",
          "authOption": "none", "keepServerRunning": True, "keepConnected": True})
json.dump(d, open(p, "w"), indent=2)
PY
rb_xvfb || exit 1
L=$P/rb-results/editor-$(date +%Y%m%d-%H%M%S).log; mkdir -p "$P/rb-results"
nohup setsid bash -c ". ~/farmer-unity-verify/tools/rb-env.sh; RB_DISPLAY=:99; rb_unity_gpu -projectPath '$P' -buildTarget StandaloneWindows64 -logFile '$L'" >/dev/null 2>&1 < /dev/null &
echo "editor starting: log $L"
