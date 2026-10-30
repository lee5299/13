from synframe_session import *
from datetime import datetime,timezone
io=host();op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'))
local={'confirmed_at':'2026-10-30T03:48:18+00:00','source':'로컬 computer-use SKILL.md와 guidance.md 전체 읽기 및 도구경로 확인','facts':['computer-use는 @oai/sky로 반환된 Windows창을 선택한 뒤 관측·행동·재관측한다.','Win키 단축키·보안설정조작·터미널UI조작은 금지이므로 이 스킬로 Win+D 입력을 시험한다고 약속하지 않는다.','PATH의 dotnet/winapp은 확인되지 않았고 기본 SDK폴더는 2.1.526이다. 전체 설치 위치 점검은 아님.'],'interpretation':'실제 앱 화면 확인에 사용할 수 있는 절차다. 핵심 계산/파일 보존/복원 검증과 개발환경 준비를 대신하지 않는다.'}
save('sf-local-observation.json',local)
names=['sf-search.json','sf-assets.json','sf-foundation.json','sf-detail.json','sf-conditions.json','sf-more.json','sf-final-sources.json','sf-automation.json','sf-ui-testing.json','sf-scopecheck.json','sf-local-observation.json']
op=io.preserve_files(op,[(ROOT/'work'/n,'현재 DISCOVER intent 이후의 실제 공식자료/로컬 관측: '+n) for n in names])
save('synframe-operation.json',op)
c=io.work_context(op);save('synframe-context.json',c)
print(json.dumps({'revision':c['revision'],'state_digest':c['state_digest'],'operation':c['operation'],'selected_tasks':c['selected_tasks'],'pending_propagation_count':c['pending_propagation_count']},ensure_ascii=False))
