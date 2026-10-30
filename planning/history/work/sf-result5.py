from synframe_session import *
io=host();op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'));c=json.loads((ROOT/'work/sf-context5.json').read_text(encoding='utf-8'));rv=json.loads((ROOT/'work/sf-review5-context.json').read_text(encoding='utf-8'));ref=lambda x:{'id':x,'revision':1};pr=HostIO.patch_ref
check=json.loads((ROOT/'work/sf-artifact-check5.json').read_text(encoding='utf-8'))
ev={'id':'ev-output-review','type':'EvidenceRecord','claim':'현재 JSON 본문 읽기일치, HTML 열항목 존재, 현재후보5필드 동일성 및 원문전체/최종초안 대조를 실제 수행했다. 내용판단의 진실·실제앱성공은 이 측정의 범위가 아니다.','source':str(ROOT/'work/sf-artifact-check5.json'),'observation_kind':'measured','observed_at':check['assessed_at'],'environment':{'event':'현재 원문/후보/패키지/파일 확인','followup_request':'사용자가 이 확인 뒤 개발시작을 명시함. 이번 방법권고 후속 실행은 별도로 연결하며 기존 명세를 설치/제품검증성공으로 바꾸지 않음.'},'verification_status':'VERIFIED','scope':'파일/대조 수행의 확인; 기술추천의 충분성은 현재호스트 추론으로 Review에 분리'}
er=[ref(x) for x in ['ev-plan','ev-comparison','ev-support','ev-omission']];out=pr('ev-output-review')
def assess(reason,ers):return {'status':'PASS','rationale':reason,'evidence_refs':ers}
checks={
'coverage':assess('원문 전체25절과 현재후보/5단계를 대조했다. 제외·조건·승인대기/후속개발승인을 구별하고 전체제품범위를 삭제하지 않았다.',er),
'io':assess('현재조언 제공자는 실제호스트다. 예정제품의 정본→색인/변경신호→대조/일관된사본→백업→별도복원/실행창→화면검증의 주체·전제·실패를 명시했다. 구조 I/O오류는 없고 미래제품성공은 주장하지 않는다.',[ref('ev-comparison'),ref('ev-file'),ref('ev-store'),ref('ev-backup')]),
'knowledge':assess('문서창꾸미기와Shell층·원본/색인과대조·파일교체와전체원자성·암호/부분실패/별도복원·UIA와시각검토를 구별하여 선택/초기관문에 실제소비했다.',[ref(x) for x in ['ev-window','ev-file','ev-write','ev-store','ev-backup','ev-bpolicy','ev-ui']]),
'evidence':assess('실제자료취득의 원본receipt/버전과 해석근거를 구분한다. 0.19.1/SqliteAPI10/pinned UI스킬의 확인범위와 실제앱미시험을 유지. 현재파일확인은 별도측정이다.',er+[out]),
'conflicts':assess('로컬파일과 개인정보·본문읽기제외·진행률제외·오늘포함·외부편집/이동·이력복원은 현재방법에서 충돌하지 않는다. 실제Shell/복원조건을 성공으로 승격하지 않고 초기시험관문으로 둔다.',[ref('ev-plan'),ref('ev-comparison'),ref('ev-local')]),
'complexity':assess('폴더/메모장 단독은 부담·검색·반복 연결이 남고, SQLite는 검색·restic은 이력복원으로 역할이 다르다. 새백업엔진 구현 대신 기존엔진의 실패/복구계약을 사용하되 앱제어/키복구는 남긴다. UI배치검사는 조건부로 유지.',[ref('ev-comparison'),ref('ev-backup'),ref('ev-ui')]),
'selection':assess('WPF의 확인된 투명/테두리 경로로 Windows전용 핵심창을 먼저시험한다. WinUI/Tauri/Avalonia/Kopia는 유효대안이며 Kopia내장자동주기와 WPF의UIA지원도 실제비교에 소비했다. 속도/전체최적성 우위를 주장하지 않는다.',[ref(x) for x in ['ev-comparison','ev-tools','ev-window','ev-ui']]),
'marginal_value':assess('현재중요한 역할/입출력/배포환경 경계·키복원·조건부UI검증까지 판별했다. 다음 결정은 실제 시제품 관측이며 같은 문서비교의 추가공백은 드러나지 않았다. 모든가능성 소진을 주장하지 않음.',[ref('ev-comparison'),ref('ev-omission')]),
'constraint:local':assess('완성제품은 로컬WPF/파일/색인/로컬백업이며 개발스킬이나AI서버가 런타임필수인 구조가 아니다. 실제업무파일을 외부전송하지 않았다.',[ref('ev-plan'),ref('ev-comparison')]),
'constraint:authority':assess('방법권고와 제품설치/구현승인을 구분했다. 후속의 명시적 개발시작 요청은 이 방법검토와 연결된 별도실행으로 처리하고 과거승인을 소급 생성하지 않는다.',[ref('ev-plan'),out]),
'constraint:preserve':assess('전체계획·사용일수/진행률제외·오늘포함·원래주기/과거원본·디자인30~50줄 요구가 계획/관문에 유지된다.',[ref('ev-plan'),ref('ev-support'),ref('ev-omission')])}
capdetails={
'cap-alignment':('C1','원문/목적과 실제기술방법을 대조해 누락·과잉 및 기능성공에도 원본/판단/복원실패를 구별','요구사항전체/지지·별도누락/현재조언범위',er,'정본/업무ID·대조·양쪽편집보존·보수부담·별도복원·전체인수를 후보와 순서에 소비했다.'),
'cap-selection':('C2','현재역할·출력·전제·실패와대안의 내장능력을 구별하여 기본/조건부/대체를 선택','공식문서·스킬절차 근거의 방법권고',er+[ref('ev-tools'),ref('ev-ui')],'기본6구성과 UI배치 조건부·대안5구성을 추가기여/연결/실패차이로 비교했다.'),
'cap-roadmap':('C3','전체파일/업무/주기흐름과 실패·복원검증을 개발선후 및 관찰가능관문으로 연결','제안된5단계, 현재제품실행 미포함',er+[ref('ev-file'),ref('ev-backup')],'한건전과정과 초기복원 뒤 실자료, 후속업무판단/반복/완료검색/설정/배포로 전체범위를 연결했다.'),
'cap-delivery':('C4','파일동일성/절차검사와 의미충족/실제제품성공을 구분해 현재근거에 맞는 결과를 보존','현재방법권고의 Review/채택/패키지 및 완료전 검증',er+[out],'현재후보2와 본문/파일을 실제대조했고 exact Review/CommitGate 및 완료확인은 코어에서 계속검사한다. 후속구현요청은 별도범위로 연결한다.')}
cc={}
for ident,(cid,ability,scope,evrefs,reason) in capdetails.items():cc[ident]=dict(assess(reason,evrefs),understanding_basis=[{'criterion_ids':[cid],'ability':ability,'scope':scope,'task_requirements':[],'evidence_refs':evrefs,'consumer_refs':[{'id':ident,'revision':1}],'rationale':reason}])
qa={}
for need in rv['information_needs']:
 ident=need['owner_ref']['id']
 if ident.startswith('asset-'):
  reason={'asset-host':'실제호스트의방법권고/근거관리 역할과 미래제품을 구분했다.','asset-wpf':'문서상창꾸미기→초기Shell/배율시험의 역할과 미검증을 구분했다.','asset-sqlite':'정본과조회색인·실행중DB지원사본 조건을 연결했다.','asset-restic':'로컬암호·부분실패·스케줄제어/별도복원·키회복을 조건으로 연결했다.','asset-computeruse':'즉시창관측과 파일/계산검증의 보완 및금지를 유지했다.','asset-specframe':'전체명세/제안·미검증·승인경계를 유지했다.'}[ident]
 elif need['id'].endswith('goal-interpretation'):reason='지지파일과 별도양방향누락/목적실패파일을 구분하여 현재후보에 소비했다. 외부체계로 목표를 확대하지 않는다.'
 elif need['id'].endswith('tool-discovery'):reason='기존후보밖 기능검색과 Avalonia/Kopia/MS스킬을 실제찾아 역할/전제/실패/범위를 비교하고 결과/작업에 반영했다.'
 elif need['id'].endswith('tool-scope'):reason='스킬/기술선택 요청의 적용판정과 두 필수의무를 실제수행했다.'
 else:reason='화면단독성공으로 놓치는 원본/식별·복원·판단을 후보의초기관문 및 전체개발에 소비했다.'
 qa[need['key']]=assess(reason,er+([ref('ev-search')] if need['id'].endswith('tool-discovery') else []))
review={'id':'review-advice-current','type':'ReviewRecord','candidate_ref':rv['candidate_ref'],'goal_ref':rv['goal_ref'],'snapshot':rv['snapshot'],'checks':checks,'unresolved_items':[],'extensions':{'verdict':'READY','goal_alignment':{f['id']:assess(f['interpretation']+' 현재후보와본문에서 해당범위를 보존하고 요청하지 않은실제성공으로 확대하지 않는다.',er) for f in c['goal']['extensions']['goal_alignment']['facets']},'capability_conditions':cc,'question_assessments':qa,'information_need_refs':rv['information_need_refs'],'independent_followups':{},'search_value':{'alternatives':[{'action':'DEEPEN_KNOWLEDGE','expected_improvement':'새현재조건반박시 재판별','cost':'동일범위 반복독해는 현재결정 구별을 늘리지 않음','rationale':'기술문서/절차의 선택조건은판별되었고 실제제품조건은다음실행관문'},{'action':'EXPAND_COMPOSITION','expected_improvement':'실제시제품 실패때 대안비교','cost':'현재역할/조건을 다시배열해도 새판별없음','rationale':'WinUI/Tauri/Avalonia/Kopia대안과 조건부UI기여를 이미 현재비교에 소비'}],'stop_rationale':'현재방법조사의 중요질문/반박/반영은 처리됐고 후속제품구현·시험은 별도명시요청을 받은 실행범위다. 세계전체최적해/실제제품성공은 주장하지 않는다.'}}}
contents=json.loads((ROOT/'work/sf-package-contents.json').read_text(encoding='utf-8'))
package={'id':'package-advice','type':'SolutionPackage','candidate_ref':rv['candidate_ref'],'review_ref':pr(review['id']),'contents':contents,'completion_scope':'현재 원문/명세에 대한 방법조사·비교·개발경로 권고. 실제프로그램 구현/성능/복원미검증. 후속 개발시작 명시요청은 연결된 실행으로 진행.','artifact_refs':[a for a in op['extensions']['actual_observations'] if a['path'].endswith(('개발계획.html','개발계획.json'))],'validation_refs':[out]+er,'uncertainties':[{'description':x['항목'],'important':False,'rationale':'현재방법권고를 막는 미판별능력이 아니라 '+x['영향']+'; '+x['처리'],'evidence_refs':er} for x in contents['Uncertainties']],'extensions':{'contract_version':'3.2','validation_scope':contents['Provenance']['검증범위'],'followup_authorization':'그럼 이제 개발을 시작해주세요. — 현재보고서작성 뒤 새명시요청. 원문/이전승인상태의 역사보존.'}}
r=io.build_result(op,[ev,review,package],result_ids=[ev['id'],review['id'],package['id']],preserved_refs=[rv['candidate_ref']],evidence_refs=er+[out],unresolved_items=[],observed_effects=['현재후보의 원문/방법·조건·추가기여/대안을 의미검토','산출물일치와 의미/제품검증경계 구분','방법권고의 READY 및 패키지보존; 실제Gate/채택은 다음'],next_decision_inputs=['정확한현재 Review와 Gate로 방법권고채택','사용자의 후속명시구현요청을 전체명세와 별도실행으로 연결'],outcome='SUCCEEDED',learning_results=[],propagation_resolutions=[],path_assessments=[],expected_revision=c['revision']);save('sf-result5.json',r)
v=io.preflight(op,r);save('sf-preflight5.json',v);print(json.dumps({k:v for k,v in v.items() if k!='prepared_result'},ensure_ascii=False))
