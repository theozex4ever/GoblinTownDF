#!/usr/bin/env bash
# Runs the sim tests, builds the Godot C# project, and boots the game headless.
# Fails on any test failure, build error, or console error.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
GODOT="${GODOT_PATH:-godot-mono}"

echo "== sim tests"
dotnet test "$ROOT/sim/tests" --nologo -v q

echo "== godot build"
"$GODOT" --headless --path "$ROOT/game" --build-solutions --quit

echo "== godot headless boot"
OUT="$("$GODOT" --headless --path "$ROOT/game" --quit-after 3 2>&1)"
echo "$OUT"
if grep -qE "ERROR|SCRIPT ERROR|Unhandled exception" <<<"$OUT"; then
	echo "console errors found" >&2
	exit 1
fi

# Exercise the bridge with real Godot input events; the scene exits nonzero on a failed check.
echo "== selection input checks"
"$GODOT" --headless --path "$ROOT/game" res://tests/SelectionInputChecks.tscn
