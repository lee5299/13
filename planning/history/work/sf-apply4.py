from synframe_session import *
io=host();op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'));r=json.loads((ROOT/'work/sf-result4.json').read_text(encoding='utf-8'))
r['unresolved_items']=['현재후보의 의미/구조 Review와 Gate·채택·완료']
r['observed_effects']=['6자산의 실제근거를 연결하고 availability와 문서/실행 범위는 유지','현재후보를 조합·해석에 대조하여 8건 반영을 실제수정본에 연결','닫힌 학습을 재개하지 않고 과거판단 이력을 보존']
r['next_decision_inputs']=['적용 후 현재후보와 Review 구조·질문 확인']
save('sf-result4-final.json',r)
p=io.finish(op,r);save('sf-applied4.json',p)
c=io.work_context();save('synframe-context.json',c)
rv=io.review_context(io.result_ref(p,'candidate-local-native'));save('synframe-review-context.json',rv)
print(json.dumps({'applied':p,'state':c['state'],'revision':c['revision'],'digest':c['state_digest'],'pending_propagation_count':c['pending_propagation_count'],'unfinished':c['unfinished_learning'],'unapplied':c['unapplied_observation_count'],'review_keys':list(rv),'structure':rv.get('structure'),'reference_errors':rv.get('reference_errors'),'questions':[{k:v for k,v in x.items() if k not in ['task','answer','understanding','expansion']} for x in rv.get('questions',[])]},ensure_ascii=False))
