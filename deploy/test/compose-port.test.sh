#!/usr/bin/env bash
# Pins the configurable published port (stone 6).
#
# `docker compose config` renders the effective compose file without a running
# daemon, so this asserts that the host port workflow-api publishes tracks
# WORKFLOW_HOST_PORT, and falls back to 8080 when it is unset. The container
# side (target 8080) and the loopback bind (127.0.0.1) must stay fixed.
#
# Requires docker compose (the CLI, not a daemon). Skips cleanly without it.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
compose="$here/../docker-compose.yml"

if ! docker compose version >/dev/null 2>&1; then
    echo "SKIP - 'docker compose' CLI unavailable"
    exit 0
fi

# API_KEY is required for a clean render but irrelevant to the port assertion.
render() { API_KEY=test WORKFLOW_HOST_PORT="$1" docker compose -f "$compose" config 2>/dev/null; }
render_default() { API_KEY=test docker compose -f "$compose" config 2>/dev/null; }

fail=0
check() { # <description> <needle> <haystack>
    local desc="$1" needle="$2" hay="$3"
    if printf '%s' "$hay" | grep -q "$needle"; then
        echo "ok   - $desc"
    else
        echo "FAIL - $desc (expected to find: $needle)"
        fail=1
    fi
}

custom="$(render 9090)"
check "custom WORKFLOW_HOST_PORT is published"  'published: "9090"' "$custom"
check "custom render keeps loopback bind"       'host_ip: 127.0.0.1' "$custom"
check "custom render keeps container target"    'target: 8080'       "$custom"

default="$(render_default)"
check "defaults to 8080 when unset"             'published: "8080"'  "$default"

if [ "$fail" -ne 0 ]; then
    echo "COMPOSE PORT TEST FAILED"
    exit 1
fi
echo "COMPOSE PORT TEST PASSED"
