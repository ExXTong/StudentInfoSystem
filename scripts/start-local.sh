#!/usr/bin/env bash
set -euo pipefail

# 本地一键启动：后端全部服务 + 前端
# 需要先 dotnet build 和 npm install

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

export ASPNETCORE_ENVIRONMENT=Development
export JWT__KEY="${JWT__KEY:-TestSecretKeyForLocalTestingOnly1234567890}"
export Portal__Proxy="${Portal__Proxy:-http://192.168.0.69:6152}"
export Portal__FingerprintCookies="${Portal__FingerprintCookies:-}"

SERVICES=(
  "StudentInfoSystem.AuthService"
  "StudentInfoSystem.StudentService"
  "StudentInfoSystem.GradeService"
  "StudentInfoSystem.ScheduleService"
  "StudentInfoSystem.Gateway"
)

PIDS=()

cleanup() {
  echo ""
  echo "正在停止本地服务..."
  for pid in "${PIDS[@]}"; do
    kill "$pid" 2>/dev/null || true
  done
  pkill -f "vite --host 0.0.0.0" 2>/dev/null || true
  exit 0
}
trap cleanup INT TERM EXIT

for svc in "${SERVICES[@]}"; do
  echo "启动 $svc ..."
  dotnet run --project "$ROOT/$svc" --no-build > "/tmp/${svc}.log" 2>&1 &
  PIDS+=($!)
done

echo "启动前端 ..."
cd "$ROOT/course-schedule-frontend"
npm run dev -- --host 0.0.0.0 > /tmp/frontend_dev.log 2>&1 &
PIDS+=($!)

sleep 6
echo ""
echo "✅ 本地服务已启动："
echo "  前端: http://localhost:5173"
echo "  后端: http://localhost:10000"

if command -v xdg-open >/dev/null 2>&1; then
  xdg-open http://localhost:5173 >/dev/null 2>&1 || true
elif command -v open >/dev/null 2>&1; then
  open http://localhost:5173 >/dev/null 2>&1 || true
fi

wait
