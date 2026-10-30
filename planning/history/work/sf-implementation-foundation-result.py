from synframe_session import *
io=HostIO(STATE,json.loads((ROOT/'work/synframe-implementation-run.json').read_text(encoding='utf-8'))['id'])
op=json.loads((ROOT/'work/sf-implementation-operation.json').read_text(encoding='utf-8'))
op=io.preserve_files(op,[(ROOT/'work/implementation-observation-20261030.md','호스트 실제 CLI/Computer Use 관측 후 보존; 과거 시각은 해당 기록 범위'),(Path(r'D:\13\업무정리\tests\검사자료\20261030-153700-2bf3e0\검사결과\결과.json'),'실제 핵심동작/restic 검사 결과 2026-10-30T15:37:16+09:00')]);save('sf-implementation-operation.json',op)
save('sf-implementation-after-preserve.json',io.work_context(op))
print(json.dumps({'operation_ref':io.reference(op),'expected_apply_revision':op['extensions']['expected_apply_revision'],'observations':op['extensions']['actual_observations']},ensure_ascii=False))
