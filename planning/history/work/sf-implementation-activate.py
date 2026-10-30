from synframe_session import *
io=HostIO(STATE,json.loads((ROOT/'work/synframe-implementation-run.json').read_text(encoding='utf-8'))['id']);c=json.loads((ROOT/'work/sf-implementation-context.json').read_text(encoding='utf-8'))
io.call('transition',payload={'target':'ACTIVE','reason':'사용자의 명시적 개발시작 승인. 전체명세와 미검증을 보존하여 시제품·조건관측부터 시작.','evidence_refs':[]},expected=c['revision'])
c=io.work_context();save('sf-implementation-context.json',c);print(json.dumps({'revision':c['revision'],'state_digest':c['state_digest'],'state':c['state']},ensure_ascii=False))
