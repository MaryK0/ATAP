#!/usr/bin/env bash
# Захват трафика E2E-сценария (WebLab «Тестирование», п. 5).
#   scripts/capture-traffic.sh
#
# Что делает:
#   1. поднимает тестовый стенд (PostgreSQL, Redis, API);
#   2. запускает mitmproxy в режиме обратного прокси перед API (контейнер в сети стенда);
#   3. выполняет сценарий scripts/e2e-curl.sh через прокси — все запросы и ответы проходят через mitmproxy;
#   4. сохраняет подробный журнал (docs/traffic/e2e-traffic.log) и дамп потоков mitmproxy (docs/traffic/e2e.flow);
#   5. удаляет прокси и стенд — всегда, в том числе при ошибке.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
OUT="$ROOT/docs/traffic"
OUT_HOST="$(cd "$OUT" 2>/dev/null && (pwd -W 2>/dev/null || pwd) || true)"
mkdir -p "$OUT"
OUT_HOST="$(cd "$OUT" && (pwd -W 2>/dev/null || pwd))"
# Git Bash на Windows иначе преобразует пути вида /out в Windows-пути; отключаем это только для docker
docker() { MSYS_NO_PATHCONV=1 command docker "$@"; }

MITM_IMAGE="mitmproxy/mitmproxy:10.4.2"
MITM_NAME="atap-mitm-$$"

eval "$(bash scripts/test-stand.sh up)"
cleanup() {
  docker rm -f "$MITM_NAME" >/dev/null 2>&1 || true
  bash scripts/test-stand.sh down || true
}
trap cleanup EXIT

NETWORK="${ATAP_TEST_PROJECT}_default"
rm -f "$OUT/e2e.flow" "$OUT/e2e-traffic.log"

docker run -d --name "$MITM_NAME" --network "$NETWORK" -p 127.0.0.1::8888 \
  --mount "type=bind,source=${OUT_HOST},target=/out" \
  "$MITM_IMAGE" mitmdump --mode reverse:http://atap-api:8080 --listen-host 0.0.0.0 --listen-port 8888 \
  --set flow_detail=3 -w /out/e2e.flow >/dev/null

PROXY_PORT="$(docker port "$MITM_NAME" 8888/tcp | head -1 | sed 's/.*://')"
PROXY="http://127.0.0.1:${PROXY_PORT}"
for _ in $(seq 1 30); do
  curl -s -o /dev/null "$PROXY/api/v1/airports" && break
  sleep 1
done

echo "Прокси mitmproxy: $PROXY  →  API в сети стенда" >&2
bash scripts/e2e-curl.sh "$PROXY"

sleep 1
docker logs "$MITM_NAME" > "$OUT/e2e-traffic.log" 2>&1
docker stop "$MITM_NAME" >/dev/null
echo "Журнал трафика: docs/traffic/e2e-traffic.log ($(wc -l < "$OUT/e2e-traffic.log") строк)" >&2
echo "Дамп потоков:   docs/traffic/e2e.flow (открывается: mitmproxy -r docs/traffic/e2e.flow)" >&2
