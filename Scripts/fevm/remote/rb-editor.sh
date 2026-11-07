#!/bin/bash
# Usage: rb-editor.sh <project-dir> [start|stop|status]
# RB_AA=asset|built sets ROOTBORN_AA_PLAYMODE for PlayMode runs inside the editor (built needs a prior Addressables build).
# Runs the interactive Unity editor on Xvfb :99 + VirtualGL GPU so the AI Game Developer MCP plugin can serve
# http://localhost:22394 on FEVM (reach it from the PC through Scripts/fevm/mcp-tunnel.sh).
set -u
. ~/farmer-unity-verify/tools/rb-env.sh
P=$(cd "$1" && pwd -P); CMD=${2:-start}
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
# A force-stopped editor leaves scene backups that block the next start with a recovery dialog; copies are disposable.
rm -rf "$P/Temp/__Backupscenes"
rb_xvfb || exit 1
L=$P/rb-results/editor-$(date +%Y%m%d-%H%M%S).log; mkdir -p "$P/rb-results"
export ROOTBORN_AA_PLAYMODE=${RB_AA:-asset}
nohup setsid bash -c ". ~/farmer-unity-verify/tools/rb-env.sh; RB_DISPLAY=:99; rb_unity_gpu -projectPath '$P' -buildTarget StandaloneWindows64 -logFile '$L'" >/dev/null 2>&1 < /dev/null &
echo "editor starting: log $L, addressables=$ROOTBORN_AA_PLAYMODE"
# Grow the main window to the whole 1920x1080 screen; the default leaves a ~690x280 Game View that hides UI.
R=$RB_TOOLS/root/x; X="env $RB_XENV DISPLAY=:99 $R/usr/bin/xdotool"
for i in $(seq 1 120); do
  W=$($X search --onlyvisible --name "^Unity - " 2>/dev/null | head -1)
  if [ -n "$W" ]; then $X windowmove "$W" 0 0; $X windowsize "$W" 1920 1080; echo "editor window resized"; break; fi
  sleep 10
done
