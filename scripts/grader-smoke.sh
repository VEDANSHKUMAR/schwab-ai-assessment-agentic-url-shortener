#!/usr/bin/env bash
set -euo pipefail
BASE="${1:-http://127.0.0.1:5488}"
KEY="${API_KEY:-dev-api-key-change-me}"

echo "== Health =="
curl -fsS "$BASE/health" | tee /tmp/lf-health.json
echo
curl -fsS "$BASE/ready" | tee /tmp/lf-ready.json
echo

ALIAS="smoke-$(date +%s | tail -c 6)"
echo "== Create $ALIAS =="
CREATE=$(curl -fsS -X POST "$BASE/api/v1/urls" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: $KEY" \
  -d "{\"url\":\"https://example.com/smoke\",\"customAlias\":\"$ALIAS\"}")
echo "$CREATE"

echo "== Redirect =="
curl -s -o /dev/null -w "HTTP %{http_code} Location: %{redirect_url}\n" "$BASE/$ALIAS"

echo "== Orchestration Greenfield =="
RUN=$(curl -fsS -X POST "$BASE/api/v1/orchestration/runs" \
  -H "Content-Type: application/json" \
  -d '{"scenario":"Greenfield"}')
RID=$(echo "$RUN" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
STATUS=$(echo "$RUN" | python3 -c "import sys,json; print(json.load(sys.stdin)['status'])")
for i in 1 2 3 4 5 6; do
  echo "status=$STATUS"
  [[ "$STATUS" == "Succeeded" ]] && break
  if [[ "$STATUS" == "WaitingApproval" ]]; then
    RUN=$(curl -fsS -X POST "$BASE/api/v1/orchestration/runs/$RID/approve" \
      -H "Content-Type: application/json" \
      -d '{"approve":true,"comment":"smoke"}')
    STATUS=$(echo "$RUN" | python3 -c "import sys,json; print(json.load(sys.stdin)['status'])")
  else
    break
  fi
done
[[ "$STATUS" == "Succeeded" ]] || { echo "Orchestration did not succeed: $STATUS"; exit 1; }

echo "== Replan =="
REPLAN=$(curl -fsS -X POST "$BASE/api/v1/orchestration/runs/$RID/replan" \
  -H "Content-Type: application/json" \
  -d '{"brief":"Replanned after feedback","changedArtifactKey":"requirements.json","changedArtifactValue":"{\"replanned\":true}","reason":"smoke replan"}')
echo "$REPLAN" | python3 -c "import sys,json; r=json.load(sys.stdin); assert r['replanCount']>=1; print('replanCount', r['replanCount'], 'contextVersion', r['contextVersion'])"

echo "== Metrics =="
curl -fsS "$BASE/api/v1/orchestration/metrics" | python3 -c "import sys,json; m=json.load(sys.stdin); print(m); assert 'mttrMs' in m and 'averageEndToEndLatencyMs' in m"
echo "SMOKE OK"
