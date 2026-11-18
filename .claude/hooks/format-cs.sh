#!/usr/bin/env bash
# PostToolUse (Edit|Write): whitespace-format the edited C# file (~1s, no MSBuild load).
# Style and analyzer rules are enforced by the build itself
# (EnforceCodeStyleInBuild + TreatWarningsAsErrors in Directory.Build.props).
set -euo pipefail

file=$(jq -r '.tool_input.file_path // empty')
[[ "$file" == *.cs ]] || exit 0

cd "$CLAUDE_PROJECT_DIR"
rel="${file#"$CLAUDE_PROJECT_DIR"/}"
[[ -f "$rel" ]] || exit 0

dotnet format whitespace --folder --include "$rel" >/dev/null 2>&1 || true
