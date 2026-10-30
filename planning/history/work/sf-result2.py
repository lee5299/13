from synframe_session import *
io=host();c=json.loads((ROOT/'work/synframe-context.json').read_text(encoding='utf-8'));op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'));pr=HostIO.patch_ref
items=[
('ev-search','sf-search.json','2026-10-30T03:46:33+00:00','기능 중심 검색에서 기존 WPF/WinUI/Tauri/restic 외에 Microsoft win-dev-skills, Avalonia, Kopia의 공식 자료를 발견했다. 검색 결과의 비공식 자료는 기술 판단 근거로 채택하지 않는다.','현재 후보 밖을 조사한 실제 검색과 범위'),
('ev-tools','sf-assets.json','2026-10-30T03:46:43+00:00','OpenAI winui-app은 C# WinUI 설정·빌드·실행 확인 절차를 제공한다. Avalonia는 Windows Win32 기반 창/입력/파일대화상자를 문서화한다. KopiaUI는 자체 자동 스냅샷·보존·복원을 제공하므로 수동 백업만 가능한 대안으로 제외할 수 없다.','공식 본문 기능과 도입 역할. 현재 설치/실행 성능 아님'),
('ev-window','sf-conditions.json','2026-10-30T03:47:21+00:00','WPF는 WindowStyle=None/AllowsTransparency와 일반창을 문서화한다. AppWindow는 WPF 포함 HWND 창과 함께 사용할 수 있다. 이 자료만으로 아이콘 아래층 동작을 확인한 것은 아니다. restic 복원은 선택 복원과 별도 target을 제공하지만 기본 덮어쓰기를 보호해야 한다.','공식 창/복원 기능과 한계'),
('ev-file','sf-detail.json','2026-10-30T03:47:07+00:00','FileSystemWatcher는 버퍼초과 누락과 중복 이벤트·폴더 이동 해석의 주의를 문서화한다. restic 0.19.1은 Windows VSS 옵션을 제공한다. Kopia는 GUI 정책의 자동주기와 변경분 스냅샷을 제공한다.','변경 감지와 백업의 문서상 조건. VSS 실제 환경/권한은 미시험'),
('ev-store','sf-scopecheck.json','2026-10-30T03:49:31+00:00','Microsoft.Data.Sqlite.Core v10.0.0 API에 BackupDatabase가 존재한다. restic은 Windows 실행파일 제공 경로가 있다. UI 테스트 스킬은 WPF도 다루고 실제 대상/PID·전달된 파일 검증을 요구한다.','확인한 API 버전과 도구 문서 범위. 최신 설치 버전 주장 아님'),
('ev-backup','sf-automation.json','2026-10-30T03:48:39+00:00','restic 0.19.1 scripting은 backup/restore JSON 및 backup 일부 읽기 실패의 종료코드3, 잠금11, 암호12와 미지 코드 실패 처리를 문서화한다. 모든 명령이 JSON인 것은 아니며 prune 혼합 출력을 구별한다.','앱 백업 상태표시를 연결할 문서상 입출력/실패 조건'),
('ev-bpolicy','sf-final-sources.json','2026-10-30T03:47:47+00:00','restic 로컬 저장소는 암호가 필수이며 분실시 복구할 수 없고 일/월 보존과 prune/check를 제공한다. MS winui-setup은 .NETSDK/WinApp/DeveloperMode와 선택적 Sandbox·AOT를 구별한다. OpenAI winui-app 실행 검증은 프로세스 시작만으로 충분하지 않다.','확인한 절차의 전제와 복구/배포 조건'),
('ev-write','sf-more.json','2026-10-30T03:47:36+00:00','File.Replace는 대체 전 파일의 백업과 예외·같은볼륨 조건을 문서화한다. 파일교체와 SQLite/폴더이동 전체를 한 트랜잭션으로 묶는 기능을 확인한 것은 아니다. Tauri는 창 꾸미기/투명성/권한 경로를 제공한다.','저장과 UI 대안의 실제 설명 범위'),
('ev-ui','sf-ui-testing.json','2026-10-30T03:48:55+00:00','Microsoft winui-ui-testing(카탈로그0.7.1 pinned SHA 5a064a...)은 WPF 포함 Windows UIA 배치테스트, 결과 JSON과 PNG 시각검토를 제공한다. local 대상도 가능하여 Sandbox 부재만으로 도구가 불가능한 것은 아니다. UIA PASS는 배치/겹침 판단을 대신하지 않는다.','실제 스킬 본문과 자동화/시각검토 역할. 도구 실행 성공 아님'),
('ev-local','sf-local-observation.json','2026-10-30T03:48:18+00:00','설치된 computer-use 절차는 반환된 창 선택/관측·행동·재관측이며 Win키·보안설정·터미널UI 자동화를 금지한다. PATH에서 dotnet/winapp이 확인되지 않고 기본 SDK폴더는2.1.526이다.','로컬 절차와 제한적인 환경 확인. 다른 설치 위치까지 검사한 것은 아님')]
evidence=[]
for ident,name,at,claim,scope in items:
 evidence.append({'id':ident,'type':'EvidenceRecord','claim':claim,'source':str(ROOT/'work'/name),'observation_kind':'measured' if ident=='ev-local' else 'external_claim','observed_at':at,'environment':{'target':'권고용 공식자료/로컬관측','source_file':name},'verification_status':'VERIFIED','scope':scope})
er=[pr(x['id']) for x in evidence]
t=HostIO.edit(c['selected_tasks'][0]['current'])
t['question']='폴더 중심 Windows 로컬 앱의 화면·편집·감지·복원·검증을 맡길 기존 및 외부 수단과 실제 경계는 무엇인가?'
t['context'].update(purpose='C2/C3 선택에 필요한 도구 발견과 조건 판별',scope='실제 앱 구현이 아닌 개발방법 권고; WPF/WinUI/Tauri/Avalonia 및 백업/검증 역할',environment={'os':'Windows','data':'로컬 개인 업무'},conditions=['실제제품시험 미요청','설치하지 않음'])
abilities=[
('discovery.goal_link','Windows특수창, 실제 텍스트원본, 탐색기변경, 복원가능성의 수단을 구별한다.','ev-plan','계획의 D01/PVT05/L08/B08에서 도출한 역할을 검색 질문과 공식 기능에 연결했다.'),
('discovery.actual_search','기존목록밖의 기능중심 실제 검색과 발견 후 본문조사를 제시한다.','ev-search','Avalonia/Kopia/MS개발스킬을 실제로 발견하고 원문 경로 오류도 핀과 메타데이터로 교정했다.'),
('discovery.observed_scope','창꾸미기와 Shell연동, 파일알림과 상태대조, UIA와 시각검토, 스냅샷과 앱일관성을 구별한다.','ev-file','기능 문서가 실제바탕화면/백업속도를 입증하지 않으며 감지누락과 별도복원의 조건을 설명할 수 있다.'),
('discovery.selection_effect','새 수단의 보완/대체 역할과 선택에 미치는 조건을 판단한다.','ev-ui','UI테스트 스킬은 WinUI 전용이 아니며 WPF회귀검증에도 사용가능. Kopia는 자동GUI대안, restic은 앱제어CLI 후보로 비교에 전달한다.'),
('discovery.termination','권고에 필요한 제공기능·연결·한계가 확보된 범위와 향후 실제구현 시험을 분리한다.','ev-window','문서기반 우선경로와 실패시 재평가 조건을 구성할 만큼 확인했다. 성능/실환경레이어는 계획된 인수시험이며 현재 추천을 실제성공으로 쓰지 않는다.')]
t['answer_requirements']=[{'id':i,'ability':a,'discriminator':'문서 제공·현장 관측·개발 지침·실제 실행의 차이','sufficient_when':r,'evidence_requirement':'현재 범위의 공식 위치와 실제 관측'} for i,a,e,r in abilities]
t['assessment']={'relevance':'RELEVANT','materiality':'MATERIAL','role':'SELECTION','rationale':'명시된 스킬추천과 Windows 로컬 기능의 선택 근거','evidence_refs':er,'unknowns':[]}
t['answer']={'status':'SUPPORTED','claims':[{'id':x['id'],'text':x['claim'],'evidence_refs':[pr(x['id'])],'source_locator':x['source']+'; 원 URL/읽은 위치는 저장된 tool response에 보존','conditions':[x['scope']],'inference':False} for x in evidence],'counterevidence_refs':[],'gaps':[],'scope':'현재 권고의 기능·조건 조사. 실제 앱 작동·성능은 아님.'}
t['understanding']={'status':'SUFFICIENT','k_level':3,'abilities':[{'id':i,'status':'MET','evidence_refs':[{'id':'ev-plan','revision':1}] if e=='ev-plan' else [pr(e)],'rationale':r} for i,a,e,r in abilities]}
comp={'id':'work-organizer-advice:tool-composition','revision':1}
discoveries=[]
for ident,content,locator,rationale in [
 ('new-ui-automation','Microsoft UIA 배치검증은 WPF도 지원하며 화면시각검토가 별도로 필요하다.','ev-ui','기존 computer-use 즉시관측과 반복 회귀시험의 역할을 분리해 조합에서 선택한다.'),
 ('native-options','Avalonia도 Win32 통합을 제공하며 WinUI 없이도 AppWindow를 WPF에 적용할 수 있다.','ev-window','기존 WinUI우선 제안을 도구 보유보다 요구별 기능/조건으로 재비교한다.'),
 ('backup-options','KopiaUI는 자체스케줄/보존/복원 가능. restic은 JSON실패코드와 로컬복원을 제공하지만 암호와 덮어쓰기 보호가 필요하다.','ev-backup','수동복사를 기본으로 삼지 않고 기존엔진+앱제어와 별도GUI를 비교한다.'),
 ('consistency','감지 알림은 대조를 대신하지 않으며 파일교체만으로 폴더/DB 전체가 동시에 저장된다고 말할 수 없다.','ev-file','고유식별·저널/재대조·편집충돌·DB스냅샷과 복원시험을 설계/검증에 반영한다.')]:
 discoveries.append({'id':ident,'content':content,'source_locator':locator,'observation_kind':'inference','novelty':'이번 실제 탐색에서 이전 단순 권고보다 구체화한 역할/조건','parent_connection':'C2/C3 조합선택 및 보존/검증 책임','understanding_gap':'통합 조합의 선택과 실제 소비가 남음','assessment':'MATERIAL','disposition':'UPDATE','target_refs':[comp],'rationale':rationale,'reopen_condition':'새 요구·도구 조건·문서/실환경 반박이 선택을 바꿀 때'})
t['expansion']={'status':'ACCOUNTED','discoveries':discoveries,'scope_rationale':'새 역할·조건은 현재 열린 통합조합 질문에서 실제로 비교할 의무로 연결. 개별 제공기능은 문서상 판별했으며 세계 전체 탐색이나 실행 성능은 주장하지 않는다.','evidence_refs':er}
t['work_state']='IDLE';t['disposition']='CLOSED';t['reopen_conditions']=['플랫폼/배포/복원 요구 변경 또는 공식기능·중요 반박 변경']
t['closure_assessment']={'verdict':'ANSWERED','rationale':'기능중심 실제탐색·중요후보 절차/입출력/범위 판별을 마쳤다. 선택 비교는 필수이지만 이 발견 답의 선행이 아닌 별도 소비 작업으로 남긴다.','evidence_refs':er,'basis':'pending','scope':'권고에 필요한 문서상 수단·조건 발견','remaining_uncertainties':['앱 통합 성공·바탕화면 레이어·속도는 아직 시험하지 않았고 실제개발 인수조건으로 전달한다.']}
t['extensions']={'requirements_change':{'rationale':'현재계획의 구별조건으로 자동등록 능력을 구체화','evidence_refs':[{'id':'ev-plan','revision':1}]}}
link={'id':'link-discovery-composition','type':'DependencyLink','sources':[pr(t['id'])],'target_ref':comp,'relation':'COMPARES_USING','group_id':'selection','evidence_refs':er,'extensions':{'scope':'실제발견의 역할·조건을 통합비교에 소비','rationale':'현재 탐색 결과를 사용해 후보선택을 재판정해야 한다.'}}
changes=[*evidence,t,link]
r=io.build_result(op,changes,result_ids=[x['id'] for x in changes],preserved_refs=[],evidence_refs=er,unresolved_items=['현재 발견의 통합조합 소비','목표해석 지지/누락 대조'],observed_effects=['기존후보 밖 Avalonia/Kopia/MS도구 발견 및 공식절차 조사','파일대조·복원·UI검증 경계를 확인'],next_decision_inputs=['현재소비자 전파/참조 확인 후 통합비교와 해석대조'],outcome='SUCCEEDED',learning_results=[{'task_ref':HostIO.reference(c['selected_tasks'][0]['current']),'result_ref':pr(t['id']),'answer_refs':er,'understanding_change':'특수창/자료정체성/복원/자동검증의 제공기능과 남는 책임을 구별','discovery_ids':[d['id'] for d in discoveries],'upward_effects':'통합조합 질문에 역할있는 의존을 추가해 결과 소비를 요구','observations':'저장된 실제 검색·공식 문서/스킬 본문·로컬관측','prediction_difference':'Kopia의 자동GUI와 WPF에도 쓸 수 있는 UI테스트를 확인해 기존간단추천의 비교폭을 확대'}],propagation_resolutions=[],path_assessments=[],expected_revision=c['revision'])
for item in r['proposed_state_changes']:
 if item['id']==t['id']:item['closure_assessment']['basis']=io.call('learning-basis',payload=item)['basis']
save('sf-result2.json',r)
p=io.preflight(op,r);save('sf-preflight2.json',p);print(json.dumps({k:v for k,v in p.items() if k!='prepared_result'},ensure_ascii=False))
