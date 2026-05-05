#!/usr/bin/env bash
set -euo pipefail

FORMAT="${1:-png}"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="$DIR/out"

if ! command -v mmdc >/dev/null 2>&1; then
    echo "mmdc not found. Install: npm install -g @mermaid-js/mermaid-cli" >&2
    exit 1
fi

mkdir -p "$OUT"

for f in "$DIR"/*.mmd; do
    name="$(basename "$f" .mmd)"
    echo "rendering $name -> $OUT/$name.$FORMAT"
    mmdc -i "$f" -o "$OUT/$name.$FORMAT" -b white
done

echo "done. files in $OUT"
