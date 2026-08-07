#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# E2E 전체 회귀 스윕 드라이버
#
# 왜 있는가. 74개(그리고 늘어나는) E2E 시나리오를 손으로 하나씩 돌리면 한 라운드가
# 다 간다. 2026-08-07 스윕 라운드가 이 드라이버를 저장소 밖 임시 폴더에 지었다가
# 라운드가 끝나며 사라질 뻔했다 — 다음 사람이 같은 것을 다시 짓지 않게 여기 둔다.
#
# 실행 (저장소 루트에서):
#   bash tools/e2e-sweep/sweep.sh out.tsv                 # 전수
#   bash tools/e2e-sweep/sweep.sh out.tsv list.txt 360    # 목록만, 시나리오당 360초
#
#   list.txt 는 "번호<TAB>시나리오" 줄들. 번호는 tools/e2e-sweep/gen.sh 가 만드는
#   tmp 폴더의 csx 파일 이름과 맞물린다. 목록을 주지 않으면 전수를 새로 센다.
#
# 결과 TSV 열: 시나리오 / 상태 / 걸린초 / 요약 / 실패 로그 꼬리
#   상태: Passed | Failed | NOPLAY(재생 진입 실패) | NOSTART(호출 유실)
#         | DROPPED(재생이 도중에 꺼짐) | BUDGET(시간 초과)
#
# 규율 하나: **시나리오마다 재생을 껐다 켠다.** 이어 돌리면 앞 판이 남긴 월드 상태
# (벤 나무, 세운 토대, 완주한 챕터)가 다음 시나리오를 깨뜨린다. 이것만은 줄이지 마라.
# ─────────────────────────────────────────────────────────────────────────────
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
U="npx --yes uloop-cli@2.2.0"

OUT="${1:?결과 TSV 경로를 다오}"
LIST="${2:-}"
BUDGET="${3:-360}"

# .sweep 자리는 못 바꾼다. 씬 갈아 끼우기가 이 경로를 저장소 루트에서 되짚어 찾는다
# (에디터 프로세스는 셸의 환경변수를 못 본다).
WORK="$ROOT/.sweep"
mkdir -p "$WORK/gen"

cd "$ROOT" || exit 1

res() { grep -o '"Result": "[^"]*"' | sed 's/"Result": "//;s/"$//'; }
poll() { $U execute-dynamic-code --code-file "$HERE/poll.csx" 2>/dev/null | res; }

# 씬을 갈아 끼워야 도는 시나리오. 안 그러면 "둘이 늘 빨갛다"를 매번 다시 겪는다.
# (E2EPrologue·E2ESequenceLocale은 첫 줄에서 StartScene을 단언한다)
scene_for() {
  case "$1" in
    E2EPrologue.*|E2ESequenceLocale.*) echo "Assets/01.Scenes/StartScene.unity" ;;
    *)                                 echo "Assets/01.Scenes/MainScene.unity" ;;
  esac
}

open_scene() {
  printf '%s' "$1" > "$WORK/scene.txt"
  $U execute-dynamic-code --code-file "$HERE/openscene.csx" >/dev/null 2>&1
}

if [ -z "$LIST" ]; then
  LIST="$WORK/list.txt"
  bash "$HERE/gen.sh" >/dev/null || { echo "시나리오 목록을 세지 못했다" >&2; exit 1; }
fi

CUR_SCENE=""
: > "$OUT"

while IFS=$'\t' read -r NUM SC; do
  [ -z "${SC:-}" ] && continue
  T0=$(date +%s)

  $U control-play-mode --action stop >/dev/null 2>&1

  WANT="$(scene_for "$SC")"
  if [ "$WANT" != "$CUR_SCENE" ]; then
    open_scene "$WANT"
    CUR_SCENE="$WANT"
  fi

  # 입력 포커스 우회. 에디터 창이 앞에 없어도 키가 들어가게 한다.
  $U execute-dynamic-code --code-file "$HERE/enablefocus.csx" >/dev/null 2>&1
  $U control-play-mode --action play >/dev/null 2>&1

  # 재생 진입은 비동기다. isPlaying이 설 때까지 기다린 뒤에 불러야 호출이 안 유실된다.
  READY=0
  for _ in $(seq 1 45); do
    case "$(poll)" in True*) READY=1; break;; esac
    sleep 2
  done
  if [ "$READY" = "0" ]; then
    printf '%s\tNOPLAY\t0\t\t재생 모드에 들어가지 못했다\n' "$SC" >> "$OUT"
    continue
  fi
  sleep 1

  START=$($U execute-dynamic-code --code-file "$WORK/gen/$NUM.csx" 2>/dev/null | res)
  case "$START" in
    started:*) ;;
    *) ERR=$($U execute-dynamic-code --code-file "$WORK/gen/$NUM.csx" 2>/dev/null | tr '\n' ' ' | cut -c1-400)
       printf '%s\tNOSTART\t0\t\t%s\n' "$SC" "$ERR" >> "$OUT"
       $U control-play-mode --action stop >/dev/null 2>&1; continue;;
  esac

  STATUS=""; SUMMARY=""
  while :; do
    P=$(poll)
    STATUS=$(printf '%s' "$P" | cut -d'|' -f2)
    SUMMARY=$(printf '%s' "$P" | cut -d'|' -f3-)
    case "$STATUS" in Passed|Failed) break;; esac
    case "$P" in False*) STATUS="DROPPED"; break;; esac
    if [ $(( $(date +%s) - T0 )) -ge "$BUDGET" ]; then STATUS="BUDGET"; break; fi
    sleep 3
  done

  TAIL=""
  [ "$STATUS" != "Passed" ] && TAIL=$($U execute-dynamic-code --code-file "$HERE/logtail.csx" 2>/dev/null | res)

  printf '%s\t%s\t%s\t%s\t%s\n' "$SC" "$STATUS" "$(( $(date +%s) - T0 ))" "$SUMMARY" "$TAIL" >> "$OUT"
  $U control-play-mode --action stop >/dev/null 2>&1
done < "$LIST"

echo "SWEEP_DONE" >> "$OUT"
