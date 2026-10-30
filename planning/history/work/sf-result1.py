from synframe_session import *
io=host(); c=json.loads((ROOT/'work/synframe-context.json').read_text(encoding='utf-8')); op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'))
pr=HostIO.patch_ref; gr=HostIO.reference(c['goal'])
ev={'id':'ev-plan','type':'EvidenceRecord','claim':'최종확인본 전체25절은 Windows 로컬 개인 앱, 요청별 폴더·텍스트/외부 편집·완료 이동·반복·계산·검색·백업과 원본 보호를 포함하며 최종 승인은 대기라고 명시한다. 이전 권고안 hash는 이전 관측과 같지만 그 선택은 검토 대상이다.','source':op['extensions']['actual_observations'][0]['path'],'observation_kind':'measured','observed_at':'2026-10-30T02:58:50+00:00','environment':{'scope':'이번 intent 뒤 전체 본문 대조; 실제 앱 시험 아님'},'verification_status':'VERIFIED','scope':'계획 문서의 요구와 제안/미검증 상태 확인. 사용효용이나 구현 성공 증거 아님.'}
t=HostIO.edit(c['selected_tasks'][0]['current'])
t.update(question='Windows 로컬 업무앱의 개발 진행과 스킬 추천에 도구 선택·연결 판단이 필요한가?',
 context={'purpose':'신규 탐색/통합 조합 의무 적용','environment':{'target':'Windows 로컬 개인 앱의 방법 권고'},'version':'현재 계획 출력 2026-10-30','conditions':['최종승인대기','구현 미요청'],'constraints':['전체범위 보존'],'scope':'기술·스킬의 역할과 검증 순서 선택'},
 answer_requirements=[{'id':'scope.applicability','ability':'계획의 파일·창·복원 기능과 원문의 추천 요청을 근거로 도구 선택 필요와 현재 실행 권한을 구별한다.','discriminator':'스킬/기술 역할을 정해야 하는 권고와 단순 문장 교정, 구현 착수를 구별','sufficient_when':'선택 대상과 조사 의무·설치/실행 권한 경계를 설명한다.','evidence_requirement':'원문과 전체 계획에서 실제 기능·승인상태 확인'}],
 assessment={'relevance':'RELEVANT','materiality':'MATERIAL','role':'SCOPING','rationale':'사용자가 스킬 추천을 명시했고 로컬 파일·Windows 창·복원의 방법 선택이 필요하다.','evidence_refs':[pr('ev-plan')],'unknowns':[]},
 answer={'status':'SUPPORTED','claims':[{'id':'applicable','text':'적용 대상이다. 전체 계획을 구현할 기술·스킬 선택과 연결을 권고해야 하며 설치된 도구만으로 신규탐색을 생략할 수 없다.','evidence_refs':[pr('ev-plan')],'source_locator':'원문 F1/F2와 계획 CONSTRAINTS·DESKTOP·RECOVERY·DF07','conditions':['권고 목표; 설치·구현은 별도'],'inference':True}],'counterevidence_refs':[],'gaps':[],'scope':'도구선택 의무의 적용 판정'},
 understanding={'status':'SUFFICIENT','k_level':3,'abilities':[{'id':'scope.applicability','status':'MET','evidence_refs':[pr('ev-plan')],'rationale':'기술 선택이 필요한 기능과 최종 승인 대기를 실제 문서에서 구별했다.'}]},
 expansion={'status':'ACCOUNTED','discoveries':[],'scope_rationale':'적용 범위만 판정했다. 발견·조합 조사는 코어가 등록할 별도 필수 질문에서 수행하며 이 적용 답의 선행이 아니다.','evidence_refs':[pr('ev-plan')]},
 work_state='IDLE',disposition='CLOSED',closure_assessment={'verdict':'ANSWERED','rationale':'원문 추천 요청과 계획의 기술 선택 필요·권한 경계를 판별했다. 실제 기술/스킬 적합성은 다음 필수 의무로 보존한다.','evidence_refs':[pr('ev-plan')],'basis':'pending','scope':'적용 여부만','remaining_uncertainties':[]},reopen_conditions=['사용자 목표가 단순 편집 등 도구선택 없는 목표로 실제 변경될 때'])
t['extensions']={'requirements_change':{'rationale':'미평가 자동등록 능력을 실제 사용자 계획의 판별 조건으로 구체화','evidence_refs':[pr('ev-plan')]}}
caps=[]
for ident,name,criterion,condition in [
 ('cap-alignment','계획·원문 정합성 판단','C1','25절 전체 범위와 확정/제안/미검증을 보존한다. 예쁜 화면만 성공하고 폴더 원본·검색·복원이 실패하는 경우를 구별하며 구현/설치 권한을 확대하지 않는다.'),
 ('cap-selection','기술·스킬 조합 선택','C2','실제 역할·입출력·조건·대안을 비교해 한 가지 우선조합을 제시한다. 투명 창과 아이콘 아래층, 문서 설명과 실측, 스킬 지침과 실행도구를 구별하며 미확인을 부재로 바꾸지 않는다.'),
 ('cap-roadmap','전체범위 개발·검증 순서 구성','C3','요청별 폴더/텍스트/외부변경/부담/반복/이미지·결과물/검색·완료/주기/백업을 단계와 실패·복구 통과조건에 연결한다. 단계 분리는 범위 삭제가 아니며 백업 복원을 실자료 사용 전에 확인한다.'),
 ('cap-delivery','검토·채택·보존 결과 전달','C4','현재 근거·Review·Gate·채택·패키지와 최종 답이 일치한다. 파일동일성/형식검사와 추천 의미의 충분성, 방법연구 완료와 실제 앱 검증을 구별한다.')]:
 caps.append({'id':ident,'type':'Capability','goal_ref':gr,'name':name,'inputs':[],'outputs':[{'name':'advice','type':'advice-section'}],'satisfaction_conditions':condition,'mandatory':True,'origin':'inferred','derivation_refs':[gr],'extensions':{'criterion_ids':[criterion]}})
changes=[ev,t,*caps]
r=io.build_result(op,changes,result_ids=[x['id'] for x in changes],preserved_refs=[],evidence_refs=[pr('ev-plan')],unresolved_items=['해석 지지/누락 검토','실제 신규 탐색','통합 조합 비교'],observed_effects=['전체 계획과 원문을 대조해 도구 선택 의무가 적용됨을 판단','품질 실패를 구별할 네 기능 조건을 도출'],next_decision_inputs=['신규탐색/조합 질문 등록 확인 후 실제 자료 조사'],outcome='SUCCEEDED',learning_results=[{'task_ref':HostIO.reference(c['selected_tasks'][0]['current']),'result_ref':pr(t['id']),'answer_refs':[pr('ev-plan')],'understanding_change':'권고가 기술/스킬 역할 판단을 포함하며 구현 승인과 별개임을 구별','discovery_ids':[],'upward_effects':'현재 적용 판정으로 신규탐색·조합 의무를 활성화. 원문 목표 변경 없음.','observations':'문서 전체25절과 원문 추천 요청을 대조','prediction_difference':'기술 검증이 남아 있어도 추천을 위한 조사는 지금 수행해야 함을 분리'}],propagation_resolutions=[],path_assessments=[],expected_revision=c['revision'])
for item in r['proposed_state_changes']:
 if item['id']==t['id']: item['closure_assessment']['basis']=io.call('learning-basis',payload=item)['basis']
save('sf-result1.json',r)
print(json.dumps(io.preflight(op,r),ensure_ascii=False))

