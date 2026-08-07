#!/bin/bash
# 시나리오 목록을 에디터에 물어서 세고, 시나리오마다 호출 csx 한 장을 찍어 낸다.
#
# 왜 생성하는가. 시나리오는 계속 늘어난다. 74장을 저장소에 커밋해 두면 다음에
# 시나리오를 더한 사람이 그 사실을 모른 채 스윕을 돌려 "전수 통과"를 보고하게 된다.
# 목록은 **반사로 세는 것**이 유일하게 늦지 않는 길이다.
#
# 실행 (저장소 루트에서):
#   bash tools/e2e-sweep/gen.sh
#
# 남기는 것 (저장소 루트의 .sweep/, 커밋하지 않는다):
#   .sweep/scenarios.txt  — 시나리오 이름 한 줄씩
#   .sweep/list.txt       — "번호<TAB>시나리오"
#   .sweep/gen/NNN.csx    — 그 시나리오를 시작시키는 한 장
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
WORK="$ROOT/.sweep"
U="npx --yes uloop-cli@2.2.0"

mkdir -p "$WORK/gen"
cd "$ROOT" || exit 1

RAW=$($U execute-dynamic-code --code-file "$HERE/list.csx" 2>/dev/null \
      | grep -o '"Result": "[^"]*"' | sed 's/"Result": "//;s/"$//')
if [ -z "$RAW" ]; then
  echo "시나리오를 세지 못했다. 에디터가 떠 있고 컴파일이 끝났는지 보라." >&2
  exit 1
fi

# csx 안의 "\n"이 문자 그대로 돌아온다. 줄로 되돌린다.
printf '%b\n' "$RAW" | tr -d '\r' | sed '/^[[:space:]]*$/d' > "$WORK/scenarios.txt"

: > "$WORK/list.txt"
rm -f "$WORK"/gen/*.csx
N=0
while read -r SC; do
  N=$((N+1))
  NUM=$(printf '%03d' "$N")
  printf 'Survive.Testing.E2ERunner.Run("%s", Survive.Testing.%s());\nreturn "started:%s";\n' \
         "$SC" "$SC" "$SC" > "$WORK/gen/$NUM.csx"
  printf '%s\t%s\n' "$NUM" "$SC" >> "$WORK/list.txt"
done < "$WORK/scenarios.txt"

echo "시나리오 $N개 -> $WORK/list.txt"
