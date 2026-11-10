#!/usr/bin/env bash
# Builds and serves the WebGL player.
# Usage: tools/webgl.sh [build|serve|stop]   (no argument: build, then serve)
# The editor must be closed for "build": it runs a batch-mode editor on this project.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/Builds/WebGL"
PORT=8765
URL="http://localhost:$PORT/"
PID_FILE="$ROOT/Logs/webgl-serve.pid"
LOG_FILE="$ROOT/Logs/webgl-serve.log"

answers() {
  curl -s -o /dev/null --max-time 1 "$URL"
}

build() {
  # --allow-dirty-build: builds are tested before the orchestrator commits.
  unity build "$ROOT" --profile WebGL --output-path "$OUT" --allow-dirty-build
}

serve() {
  if answers; then
    echo "$URL"
    return 0
  fi
  if [ ! -f "$OUT/index.html" ]; then
    echo "No build at $OUT; run: $0 build" >&2
    return 1
  fi
  mkdir -p "$ROOT/Logs"
  nohup python3 -m http.server "$PORT" --bind 127.0.0.1 --directory "$OUT" >"$LOG_FILE" 2>&1 &
  echo $! >"$PID_FILE"
  for _ in $(seq 1 50); do
    if answers; then
      echo "$URL"
      return 0
    fi
    sleep 0.2
  done
  echo "Server did not answer at $URL; see $LOG_FILE" >&2
  return 1
}

stop() {
  if [ -f "$PID_FILE" ]; then
    kill "$(cat "$PID_FILE")" 2>/dev/null || true
    rm -f "$PID_FILE"
  fi
}

case "${1:-all}" in
  build) build ;;
  serve) serve ;;
  stop) stop ;;
  all) build && serve ;;
  *) echo "Usage: $0 [build|serve|stop]" >&2; exit 2 ;;
esac
