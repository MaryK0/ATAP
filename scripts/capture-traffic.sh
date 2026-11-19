#!/usr/bin/env bash
# E2E curl -> mitmproxy -> реальный API. Сохраняются копии без паролей/JWT.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
OUT="${ATAP_TRAFFIC_OUT:-$ROOT/TestResults/lr2/traffic}"
mkdir -p "$OUT"
OUT_HOST="$(cd "$OUT" && (pwd -W 2>/dev/null || pwd))"
docker() { MSYS_NO_PATHCONV=1 command docker "$@"; }
MITM_IMAGE=mitmproxy/mitmproxy:10.4.2
MITM_NAME="atap-mitm-${ATAP_DEVELOPER:-local}-$$"
cleanup() {
  docker rm -f "$MITM_NAME" >/dev/null 2>&1 || true
  if [ "${ATAP_CAPTURE_EXISTING:-0}" != 1 ] && [ -n "${ATAP_TEST_PROJECT:-}" ]; then
    bash scripts/test-stand.sh down || true
  fi
}
trap cleanup EXIT
if [ "${ATAP_CAPTURE_EXISTING:-0}" != 1 ]; then
  export ATAP_TEST_PROJECT="${ATAP_TEST_PROJECT:-atap-traffic-$(date +%s)-$$}"
  eval "$(bash scripts/test-stand.sh up)"
fi
NETWORK="${ATAP_TEST_PROJECT:?}_default"
output_args=(--mount "type=bind,source=${OUT_HOST},target=/out")
if [ -n "${ATAP_TRAFFIC_VOLUME:-}" ]; then output_args=(-v "${ATAP_TRAFFIC_VOLUME}:/out"); fi
docker create --name "$MITM_NAME" --user 0 --network "$NETWORK" -p 127.0.0.1::8888 \
  "${output_args[@]}" "$MITM_IMAGE" mitmdump \
  --mode reverse:http://atap-api:8080 --listen-host 0.0.0.0 --listen-port 8888 \
  --set flow_detail=0 -s /tmp/traffic-addon.py >/dev/null
docker cp scripts/traffic-addon.py "$MITM_NAME:/tmp/traffic-addon.py"
docker start "$MITM_NAME" >/dev/null
PROXY_PORT="$(docker port "$MITM_NAME" 8888/tcp | head -1 | sed 's/.*://')"
PROXY="http://${ATAP_DOCKER_HOST:-127.0.0.1}:${PROXY_PORT}"
# Для Jenkins запрос делается контейнером в сети стенда: порт опубликован только на loopback хоста.
if [ "${ATAP_CAPTURE_IN_CONTAINER:-0}" = 1 ]; then
  docker run --rm --network "$NETWORK" --entrypoint bash \
    -e "ATAP_DEVELOPER=${ATAP_DEVELOPER:-local}" \
    -v "${WORK_VOL:?}:/work:ro" "${IMAGE:?}" \
    -c 'for i in {1..30}; do curl -fs -o /dev/null "$1/api/v1/airports" && break; sleep 1; done; bash /work/src/scripts/e2e-curl.sh "$1"' -- "http://$MITM_NAME:8888"
else
  ready=0
  for _ in $(seq 1 30); do
    if curl -fs -o /dev/null "$PROXY/api/v1/airports"; then ready=1; break; fi
    sleep 1
  done
  [ "$ready" = 1 ] || { docker logs "$MITM_NAME" >&2; exit 1; }
  echo "[traffic] developer=${ATAP_DEVELOPER:-local} project=$ATAP_TEST_PROJECT proxy=$PROXY" >&2
  bash scripts/e2e-curl.sh "$PROXY"
fi
docker stop "$MITM_NAME" >/dev/null
echo "Traffic capture: ${ATAP_TRAFFIC_VOLUME:-$OUT}/e2e-traffic.log and e2e.flow (credentials redacted)" >&2
