#!/usr/bin/env bash
# Agent eval for the MCP server (issue #27, Phase 5).
# Seeds the planted-problems dataset into a SQLite file, starts the WebApi on it, and runs Claude Code
# against /mcp twice: once with every tool, once with only the data tools (and curl for exports).
# Each answer is graded by grade.py: all planted problems found, no noise reported.
#
# Usage: tools/agent-eval/run.sh [model]   (default model: sonnet; needs the claude CLI and the .NET 10 SDK)
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
src="$here/../../src"
model="${1:-sonnet}"
out="${AGENT_EVAL_OUT:-$(mktemp -d)}"
db="$out/agent-eval.db"
port=5199

echo "Output: $out"
dotnet build "$src/Agoda.DevExTelemetry.McpTests" -nodeReuse:false -v q

echo "Seeding $db"
AGENT_EVAL_DB="$db" dotnet test "$src/Agoda.DevExTelemetry.McpTests" --no-build \
  --filter "FullyQualifiedName~AgentEvalDatabase" -v q

echo "Starting the WebApi on :$port"
ConnectionStrings__DefaultConnection="Data Source=$db" ASPNETCORE_URLS="http://127.0.0.1:$port" \
  ASPNETCORE_ENVIRONMENT=Production \
  dotnet "$src/Agoda.DevExTelemetry.WebApi/bin/Debug/net10.0/Agoda.DevExTelemetry.WebApi.dll" \
  --contentRoot "$src/Agoda.DevExTelemetry.WebApi" > "$out/server.log" 2>&1 &
server=$!
trap 'kill $server 2>/dev/null || true' EXIT
for _ in $(seq 1 60); do curl -sf "http://127.0.0.1:$port/api/health" >/dev/null 2>&1 && break; sleep 1; done

analysis="get_overview find_slow_tests find_flaky_tests find_failing_tests find_slow_builds explain_build_time find_regressions"
blocked=""
for t in $analysis; do blocked="$blocked mcp__dx-telemetry__$t"; done

run() {
  local name="$1"; shift
  echo "== Run: $name"
  (cd "$out" && claude -p --model "$model" --mcp-config "$here/mcp.json" --strict-mcp-config \
     --allowedTools "mcp__dx-telemetry" "Bash(curl:*)" "$@" < "$here/prompt.md") > "$out/$name.md"
  python3 "$here/grade.py" "$out/$name.md" | tee "$out/$name.grade.txt"
}

status=0
run all-tools || status=1
run data-tools-only --disallowedTools $blocked || status=1
exit $status
