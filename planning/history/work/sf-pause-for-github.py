from synframe_session import *
io=HostIO(STATE,json.loads((ROOT/'work/synframe-implementation-run.json').read_text(encoding='utf-8'))['id'])
request=ROOT/'work/사용자_중단과_GitHub이동요청.md'
request.write_text('사용자 명시 지시: 나머지 작업은 주말에 집에서 하겠습니다. (다른 컴퓨터)\nhttps://github.com/lee5299/13.git 에 올려주세요\n\n현재 개발을 중단하고 소스/요구/디자인/검사/미완료 상태를 해당 저장소로 인계한다. 개발 완료나 이전 미적용 관측의 채택을 뜻하지 않는다. 앱 접근 재승인은 이미 성공했으며 차단으로 남기지 않는다.\n',encoding='utf-8')
op=json.loads((ROOT/'work/sf-implementation-operation.json').read_text(encoding='utf-8'))
op=io.preserve_files(op,[(request,'현재 사용자 직접 메시지: 다른 PC로 옮겨 주말에 개발을 이어가므로 현재 개발 중단 및 GitHub 업로드 지시')]);save('sf-implementation-operation.json',op)
c=io.work_context(op);save('sf-implementation-pausing-context.json',c)
print(json.dumps({'revision':c['revision'],'state':c['state'],'pending_operation':c['operation'],'unresolved_items':c['unresolved_items']},ensure_ascii=False))
