#!/usr/bin/env bash
# The lock that keeps two Unity editors (batch or interactive) off this checkout at once.
# Usage: tools/unity-lock.sh run <command> [args...]   hold the lock while the command runs
#        tools/unity-lock.sh take                      take it for an interactive editor session
#        tools/unity-lock.sh release                   give it back after `take`
# Waiting for a held lock gives up after UNITY_LOCK_WAIT seconds (default 1800) with exit 75,
# so a typo or a dead holder can never leave a loop running for ever.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCK="$ROOT/Logs/unity-batch.lock"
WAIT="${UNITY_LOCK_WAIT:-1800}"

take() {
  mkdir -p "$ROOT/Logs"
  local waited=0
  until mkdir "$LOCK" 2>/dev/null; do
    if [ "$waited" -ge "$WAIT" ]; then
      echo "Lock $LOCK still held after ${WAIT}s. Remove it only if no Unity editor is running on $ROOT." >&2
      exit 75
    fi
    sleep 5
    waited=$((waited + 5))
  done
}

release() {
  rmdir "$LOCK" 2>/dev/null || true
}

case "${1:-}" in
  run)
    shift
    [ $# -gt 0 ] || { echo "Usage: $0 run <command> [args...]" >&2; exit 2; }
    take
    trap release EXIT
    "$@"
    ;;
  take) take ;;
  release) release ;;
  *) echo "Usage: $0 run <command> [args...] | take | release" >&2; exit 2 ;;
esac
