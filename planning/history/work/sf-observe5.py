from synframe_session import *
io=host();op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'))
op=io.preserve_files(op,[(str(ROOT/'work/sf-artifact-check5.json'),'현재 원문/후보/출력 대조 및 실제 파일동일성 확인'),(str(ROOT/'outputs/업무정리_SynFrame_개발계획.html'),'현재 작성한 방법권고 보고서'),(str(ROOT/'outputs/업무정리_SynFrame_개발계획.json'),'현재 작성한 방법권고 JSON')]);save('synframe-operation.json',op)
c=io.work_context(op);save('sf-context5.json',c)
rv=io.review_context({'id':'candidate-local-native','revision':2});save('sf-review5-context.json',rv)
print(json.dumps({'revision':c['revision'],'digest':c['state_digest'],'pending':c['pending_propagation_count'],'operation':c['operation'],'snapshot':rv['snapshot'],'source_receipts':op['extensions']['actual_observations']},ensure_ascii=False))
