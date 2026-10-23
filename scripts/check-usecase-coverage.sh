#!/usr/bin/env bash
# Проверяет, что все тесты, перечисленные в docs/use-case-coverage.md, существуют в тестовых проектах
# (таблица не должна расходиться с кодом). Нужна собранная решётка: dotnet build ATAP.sln.
#   bash scripts/check-usecase-coverage.sh
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

DOC="docs/use-case-coverage.md"
LIST="$(mktemp)"; trap 'rm -f "$LIST"' EXIT

for project in ATAP.BusinessLogic.Tests ATAP.DataAccess.Tests ATAP.IntegrationTests ATAP.E2ETests; do
  dotnet test "$project" --no-build --list-tests 2>/dev/null | sed -n 's/^    //p'
done > "$LIST"
echo "Тестов в проектах: $(wc -l < "$LIST")"

# Имена тестов в таблице: Класс.Метод или просто Класс (оканчивается на Tests, IntegrationTest или E2ETest)
mapfile -t refs < <(grep -oE '`[A-Za-z0-9_]+(Tests|IntegrationTest|E2ETest)(\.[A-Za-z0-9_]+)?`' "$DOC" | tr -d '`' | sort -u)

missing=0
for ref in "${refs[@]}"; do
  if ! grep -qF ".${ref}" "$LIST" && ! grep -qF ".${ref}." "$LIST"; then
    echo "НЕ НАЙДЕН: $ref"
    missing=$((missing + 1))
  fi
done

echo "Ссылок на тесты в $DOC: ${#refs[@]}, не найдено: $missing"
[ "$missing" -eq 0 ]
