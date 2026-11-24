#!/usr/bin/env bash
# Publishes the Native AOT smoke test, fails on any trimming/AOT diagnostic, then runs it.
#
# The smoke test is a single-file program (scripts/aot-smoke.cs) rather than a project of its own:
# the .NET 10 SDK publishes it from that file, pulling in the library through its #:project
# directive. Nothing is added to the solution.
#
# The smoke test talks to the real fcitx5 on the session bus. It exits with 2 when fcitx5 is not
# running, which this script reports as a skip rather than a failure.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
program="$root/scripts/aot-smoke.cs"
output="$root/artifacts/aot-smoke"
log="$(mktemp)"
trap 'rm -f "$log"' EXIT

if [ "${1:-}" = "" ]; then
    rid="linux-x64"
else
    rid="$1"
fi

rm -rf "$output"

echo "==> publishing $program ($rid)"
dotnet publish "$program" -c Release -r "$rid" -o "$output" 2>&1 | tee "$log"

# Trimming and AOT diagnostics are the point of this check, so any of them fails the script.
if grep -qE '(warning|error) (IL[0-9]{4}|NETSDK[0-9]{4})' "$log"; then
    echo "==> FAILED: trimming/AOT diagnostics were reported" >&2
    exit 1
fi

if [ ! -x "$output/aot-smoke" ]; then
    echo "==> FAILED: no native executable was produced" >&2
    exit 1
fi

echo "==> running the native executable"
set +e
"$output/aot-smoke"
status=$?
set -e

case "$status" in
    0) echo "==> AOT smoke passed" ;;
    2) echo "==> AOT smoke skipped: fcitx5 is not running on the session bus" ;;
    *) echo "==> AOT smoke FAILED with exit code $status" >&2; exit "$status" ;;
esac
