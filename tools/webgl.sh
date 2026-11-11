#!/usr/bin/env bash
# Builds and serves the WebGL player.
# Usage: [WEBGL_PORT=<port>] tools/webgl.sh [build|serve|stop]   (no argument: build, then serve; port 8765 by default)
# The editor must be closed for "build": it runs a batch-mode editor on this project.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/Builds/WebGL"
PORT="${WEBGL_PORT:-8765}"
URL="http://localhost:$PORT/"
PID_FILE="$ROOT/Logs/webgl-serve-$PORT.pid"
LOG_FILE="$ROOT/Logs/webgl-serve-$PORT.log"

answers() {
  curl -s -o /dev/null --max-time 1 "$URL"
}

# Prints the PID of the server this project started on PORT, if it is still alive.
# The command-line check guards against a reused PID.
own_pid() {
  [ -f "$PID_FILE" ] || return 1
  local pid
  pid="$(cat "$PID_FILE")"
  ps -p "$pid" -o command= 2>/dev/null | grep -q "http.server $PORT " || return 1
  echo "$pid"
}

build() {
  # --allow-dirty-build: builds are tested before the orchestrator commits.
  unity build "$ROOT" --profile WebGL --output-path "$OUT" --allow-dirty-build
}

serve() {
  if own_pid >/dev/null && answers; then
    echo "$URL"
    return 0
  fi
  if answers; then
    echo "Port $PORT answers, but not from a server this project started; pick a free port: WEBGL_PORT=<port> $0 serve" >&2
    return 1
  fi
  if [ ! -f "$OUT/index.html" ]; then
    echo "No build at $OUT; run: $0 build" >&2
    return 1
  fi
  stop
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
  local pid
  if pid="$(own_pid)"; then
    kill "$pid"
  fi
  rm -f "$PID_FILE"
}

case "${1:-all}" in
  build) build ;;
  serve) serve ;;
  stop) stop ;;
  all) build && serve ;;
  *) echo "Usage: $0 [build|serve|stop]" >&2; exit 2 ;;
esac
