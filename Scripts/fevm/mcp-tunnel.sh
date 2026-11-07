#!/bin/bash
# PC -> FEVM tunnel for the AI Game Developer MCP: PC localhost:22395 -> FEVM localhost:22394.
# .mcp.json registers it as "ai-game-developer-fevm" (http://localhost:22395). Keep this running while using it.
# Env: FEVM_HOST, FEVM_KEY (see sync.sh). Host key checking stays on.
HOST=${FEVM_HOST:-jdyj@192.168.45.163}
KEY=${FEVM_KEY:-$HOME/.ssh/fevm_setup_ed25519}
exec ssh -i "$KEY" -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes \
  -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 -N -L 127.0.0.1:22395:127.0.0.1:22394 "$HOST"
