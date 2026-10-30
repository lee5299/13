from synframe_session import *
io=host();c=json.loads((ROOT/'work/synframe-context.json').read_text(encoding='utf-8'));rv=json.loads((ROOT/'work/synframe-review-context.json').read_text(encoding='utf-8'))
j={'action':'SIMPLIFY','ddc':{'observable_state':{'revision':c['revision'],'state_digest':c['state_digest'],'facts':['학습4개는 현재범위 CLOSED이며 pending 전파8개가 실제로 남았다.','review-context는 현재후보1의 기능근거 연결 누락(6개 자산 ungrounded)과 link-discovery-composition의 과거target참조를 노출한다. I/O오류는 없다.']},'decision_objective':'현재의 실제의미판단을 유지하면서 자산출처와 현재소비참조를 정확하게 연결하고 전파를 실제처리','available_actions':['SIMPLIFY','REVIEW_SOLUTION','DEEPEN_KNOWLEDGE'],'decision_policy':'출처는 이미 실제확보한 자료에 연결하며 availability나근거진실을 통과용으로바꾸지 않음. 닫힌질문을 기록수정 때문에 다시 예약하지 않는다.','constraints':['과거판단/관측 보존','근거없는PASS/최신ref치환금지','기술제안과 실제제품능력 구별'],'state_transition':'정확한자산출처·현재대상참조·현재후보를 새버전으로 기록하고 유지/수정전파 평가','feedback':'새현재review-context의 구조와 의미를 다시대조. 실제지식결함이면 학습으로복귀','recovery':'patch-refs로 전이영향/후속의무를 먼저읽고 교정. preflight거부는 구체원인으로수정'},'alternatives':[{'action':'REVIEW_SOLUTION','rationale':'현재구조결함과PENDING을처리하기전 READY를 쓸 수 없다.'},{'action':'DEEPEN_KNOWLEDGE','rationale':'현재오류는 확보한의미/관측의 부재가 아닌출처/참조연결이다.'}],'evidence_refs':[{'id':'ev-comparison','revision':1}],'rationale':'현재진단과실제관측의 차이를 정직하게연결한다.','expected_effects':['근거연결보완','현재참조와 전파정리']}
op=io.begin('SIMPLIFY',j,{'description':'현재의미유효성을 보존하며 자산provenance와 소비대상참조를 실제근거로 교정','learning_task_refs':[]},input_refs=[rv['candidate_ref']],evidence_refs=[{'id':'ev-comparison','revision':1}],key='repair-grounding-current-bindings')
save('synframe-operation.json',op)
changes=[];pr=HostIO.patch_ref
for a in rv['selection_context']['selected_assets']:
 b=HostIO.edit(a);b['provenance']=a['understanding'][0]['evidence_refs'];changes.append(b)
candidate=HostIO.edit(rv['selection_context']['candidate']);candidate['asset_refs']=[pr(a['id']) for a in changes];candidate['provenance']=[{'id':'ev-comparison','revision':1},{'id':'ev-support','revision':1},{'id':'ev-omission','revision':1}];changes.append(candidate)
for cap in ['cap-alignment','cap-selection','cap-roadmap','cap-delivery']:
 link=io.call('get',id='provide-'+cap);b=HostIO.edit(link);b['sources']=[pr('asset-host')];changes.append(b)
link=io.call('get',id='link-discovery-composition');b=HostIO.edit(link);b['target_ref']={'id':'work-organizer-advice:tool-composition','revision':2};changes.append(b)
save('sf-changes4.json',changes)
projection=io.patch_refs(changes);save('sf-projection4.json',projection)
print(json.dumps(projection,ensure_ascii=False))
