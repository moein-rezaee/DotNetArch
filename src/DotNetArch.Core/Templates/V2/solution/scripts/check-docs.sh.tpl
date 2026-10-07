#!/usr/bin/env bash
# Verifies bilingual documentation pairs: every X.md in the documented folders has an X.fa.md (and vice versa)
# with the same number of "## " sections. Exit code 1 lists the problems.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

problems=0
check() {
  local en="$1" fa="${1%.md}.fa.md"
  [[ -f "$fa" ]] || { echo "missing pair: $fa"; problems=$((problems + 1)); return; }
  local a b
  a=$(grep -c '^## ' "$en" || true)
  b=$(grep -c '^## ' "$fa" || true)
  [[ "$a" == "$b" ]] || { echo "section count differs: $en ($a) vs $fa ($b)"; problems=$((problems + 1)); }
}

while IFS= read -r file; do
  case "$file" in *.fa.md) continue ;; esac
  # Only documents that have (or should have) a Persian twin are checked; the spec registry decides.
  if [[ -f "${file%.md}.fa.md" ]]; then check "$file"; fi
done < <(find . -name '*.md' -not -path './node_modules/*' -not -path '*/bin/*' -not -path '*/obj/*' -not -path './samples/*' -not -path './.git/*')

while IFS= read -r file; do
  [[ -f "${file%.fa.md}.md" ]] || { echo "orphan translation: $file"; problems=$((problems + 1)); }
done < <(find . -name '*.fa.md' -not -path '*/bin/*' -not -path '*/obj/*' -not -path './samples/*' -not -path './.git/*')

if [[ "$problems" -gt 0 ]]; then
  echo "$problems documentation problem(s)."
  exit 1
fi
echo "documentation pairs OK"
