from synframe_session import *
io=host();c=json.loads((ROOT/'work/synframe-context.json').read_text(encoding='utf-8'));op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'));pr=HostIO.patch_ref;ref=HostIO.reference;gr=ref(c['goal'])
contents=json.loads((ROOT/'work/sf-package-contents.json').read_text(encoding='utf-8'))
at=json.loads((ROOT/'work/sf-comparison.json').read_text(encoding='utf-8'))['assessed_at']
evs=[]
for ident,file,claim in [('ev-comparison','sf-comparison.json','현재 목적에서 WPF/SQLite/restic과 개발·검증스킬 역할을 비교해 우선경로, 더간단한 구성의 손실, 조건부 UI배치검사와 실제대안·남은 제품시험을 판단했다.'),('ev-support','sf-support.json','원문 F1~F4와 현재기능을 대조한 지지 평가: 전체계획 권고·실제스킬역할·관리절차·로컬제품을 보존하고 설치/구현승인으로 확대하지 않는다.'),('ev-omission','sf-omission.json','별도 누락 검토: 화면성공에도 원본/복원/예상부담/전체범위를 놓칠 조건을 대조하고 고유ID·재대조·편집보존·별도복원·보수계산·전체인수 관문으로 처리했다.')]:
 evs.append({'id':ident,'type':'EvidenceRecord','claim':claim,'source':str(ROOT/'work'/file),'observation_kind':'inference','observed_at':at,'environment':{'event':'현재호스트 의미평가·산출물 작성','grounds':['ev-plan@1','보존된 공식관측']},'verification_status':'VERIFIED','scope':'근거에 따른 현재 방법권고 판단. 실제제품성능·작동성공 또는 독립검토 증거가 아님.'})
er=[pr('ev-comparison'),{'id':'ev-plan','revision':1}]+[{'id':x,'revision':1} for x in ['ev-window','ev-file','ev-store','ev-backup','ev-bpolicy','ev-ui','ev-tools','ev-local']]
comp=HostIO.edit(next(t['current'] for t in c['selected_tasks'] if t['requested_ref']['id'].endswith(':tool-composition')))
interp=HostIO.edit(next(t['current'] for t in c['selected_tasks'] if t['requested_ref']['id'].endswith(':goal-interpretation')))
comp['question']='현재 계획을 보존하는 개발·검증·복원 조합과 실제 개발 순서는 무엇인가?'
comp_r={
'composition.roles':'개발관리/명세/Windows구현/검색색인/백업엔진/즉시화면검증/조건부배치검증의 수행주체와 산출물을 분리했다.',
'composition.dispositions':'WinUI/Tauri/Avalonia/Kopia를 유효대안으로 유지하며 미시험을 기능부재로 바꾸지 않았다. WPF경로에 WinUI개발스킬을 필수 추가하지 않는다.',
'composition.combinations':'폴더+메모장 단독은 검색·부담·반복연결을 못채우고, 화면만으로 원본/복원목적을 못채운다. 호스트구현+기존엔진/기본API와 즉시검증을 기본으로 혼합한다.',
'composition.connections':'정본/ID→색인, 변경알림→대조, 일관된백업입력→restic JSON/종료코드→별도복원, 실행창→관측과 파일/계산검증의 주체·실패·검증을 명시했다.',
'composition.comparison':'투명창이 명시된 Windows전용 경로를 먼저 시험하는 WPF안을 선택. 실제속도/작업량우월을 주장하지 않으며 restic의 문서상 입출력과 암호·부분실패 조건을 보존했다.',
'composition.remaining_work':'아이콘아래층/배율/복원·속도/툴체인은 실제제품검증으로 계획하고 승인대기·단계별 실패분기를 포함했다. 현재조언에 필요한 미판별 기능은 남기지 않았다.'}
comp['context'].update(purpose='확인한 역할·조건에서 우선개발경로와 검증관문 선택',scope='방법권고, 현재제품 구현/성능이 아님')
comp['assessment']={'relevance':'RELEVANT','materiality':'MATERIAL','role':'SELECTION','rationale':'F1/F2/F4 선택과 전체진행방법을 실제조합으로 반환해야 한다.','evidence_refs':er,'unknowns':[]}
comp['answer_requirements']=[dict(a,ability=comp_r[a['id']],discriminator='역할·기여·연결·권한·실패/복원의 실제조건 대조',sufficient_when='현재범위의 판단이 다음개발행동과 통과조건으로 연결',evidence_requirement='계획+공식자료+현재조합대조') for a in comp['answer_requirements']]
comp['answer']={'status':'SUPPORTED','claims':[{'id':'selected-route','text':contents['RecommendedSolution']['우선안'],'evidence_refs':er,'source_locator':'sf-comparison.json:contents 전체와 현재역할비교','conditions':[contents['RecommendedSolution']['채택범위'],contents['RecommendedSolution']['추가조건']],'inference':True},{'id':'roadmap','text':'전체범위를 5단계에 연결하고 기술검증/복원 확인 후 실자료를 사용한다. 선택대안과 실행단계를 구분한다.','evidence_refs':er,'source_locator':'sf-comparison.json:ImplementationPlan·DecisionRationale','conditions':['실제 제품 구현은 승인후'],'inference':True}],'counterevidence_refs':[],'gaps':[],'scope':'검증 관문이 포함된 개발방법 권고'}
comp['understanding']={'status':'SUFFICIENT','k_level':3,'abilities':[{'id':i,'status':'MET','evidence_refs':er,'rationale':v} for i,v in comp_r.items()]}
comp['expansion']={'status':'ACCOUNTED','discoveries':[],'scope_rationale':'발견2의 역할/조건을 기본안·대안·조건부추가·작업/검증에 실제소비했다. 새 미판별개념은 드러나지 않았고 실제제품구현시험을 조사미완료로 혼동하지 않는다.','evidence_refs':er}
comp['dependency_refs']=[{'source_ref':{'id':'work-organizer-advice:tool-discovery','revision':2},'role':'COMPARES_USING','required':True,'scope':'현재 발견의 기능/한계로 최종조합 비교','rationale':'새 후보와 실제조건을 현재 선택에 소비했다.'}]
comp['work_state']='IDLE';comp['disposition']='CLOSED';comp['reopen_conditions']=['요구·실제플랫폼시험·도구 기능/배포·복원 조건의 중요한 반박']
comp['closure_assessment']={'verdict':'ANSWERED','rationale':'현재발견을 실제단독/대안/보완·역할/I/O·설정/복구·작업순서에 반영했다. 문서기반권고의 충분성과 미요청제품시험을 구분했다.','evidence_refs':er,'scope':'현재 개발방법 선택','remaining_uncertainties':['제품시험의 통과는 아직 주장하지 않음. 초기관문과 실패시 재평가로 처리.'],'basis':'pending'}
comp['extensions']={'requirements_change':{'rationale':'운영질문의 능력을 현재계획에서 실제판별한 역할/조건으로 구체화','evidence_refs':er}}
ir=[pr('ev-support'),pr('ev-omission'),{'id':'ev-plan','revision':1}]
interp_r={
'interpretation.source_preservation':'원문권고와 전체계획을 유지하고 기술제안·승인대기를 분리했다.',
'interpretation.result_properties':'반복 사용되는 폴더기록/탐색기접근/과거제출물/새업무판단·복원 목적에 사용할 조언임을 구별했다.',
'interpretation.material_views':'화면완성만으로 끝내면 실패하는 파일정합성·복원·보수계산·범위 관점을 별도누락 경로에서 대조했다.',
'interpretation.differences':'고유ID·대조·충돌·복원과 전체검증 관문을 개발계획에 반영했다. 외부구조를 제품신규요구로 확정하지 않았다.',
'interpretation.uncertainty':'문서능력/현재판단과 실제제품시험·사용자승인/선호를 분리했다.',
'interpretation.consumption':'기존 alignment/delivery조건과 지지/누락 결과를 대조해 유지하고 현재후보/산출물에 실제소비했다.',
'interpretation.termination':'원문에 연결된 중요미처리차이가 없고 초기기술검증/신규요구·반박 때 재평가한다. 숨은의도를 유일하게 알아냈다는 주장은 하지 않는다.'}
interp['answer_requirements']=[dict(a,ability=interp_r[a['id']],discriminator='원문목적/사용조건과 현재해석의 지지 및 양방향누락·목적실패를 분리대조',sufficient_when='의미차이를 실제권고·검증에 처리하고 권한/불확실성을 보존',evidence_requirement='원문/전체계획과 별도의 실제지지·누락평가') for a in interp['answer_requirements']]
interp['assessment']={'relevance':'RELEVANT','materiality':'MATERIAL','role':'SCOPING','rationale':'현재기술이 성공해도 원문목적을 놓칠수 있는지 별도로 평가해야 한다.','evidence_refs':ir,'unknowns':[]}
interp['answer']={'status':'SUPPORTED','claims':[{'id':'interpretation','text':'현재목표는 전체계획을 보존하는 근거있는 개발방법과 스킬권고다. 원문지지와 목적실패조건을 별도로 대조해 현재범위를 유지한다. 실제구현승인과 기능실행성공을 추정하지 않는다.','evidence_refs':ir,'source_locator':'sf-support.json 및 sf-omission.json의 서로 다른 대조','conditions':['현재 원문/계획; 실제제품구현은 별도'],'inference':True}],'counterevidence_refs':[],'gaps':[],'scope':'현재해석의 지지 및 중요관점누락 처리'}
interp['understanding']={'status':'SUFFICIENT','k_level':3,'abilities':[{'id':i,'status':'MET','evidence_refs':ir,'rationale':v} for i,v in interp_r.items()]}
interp['expansion']={'status':'ACCOUNTED','discoveries':[],'scope_rationale':'목적실패조건은 이미 도출한 기능과 현재아키텍처/개발검증에 소비했다. 기존기준 밖 새로운미판별차이는 확인하지 못했고 포괄적인 가능성만으로 새질문을 만들지 않는다.','evidence_refs':ir}
interp['work_state']='IDLE';interp['disposition']='CLOSED';interp['reopen_conditions']=['실제원문/요구변경 또는 현재해석을 깨는 관측된 목적실패']
cons=[{'id':'cap-alignment','revision':1},{'id':'cap-delivery','revision':1}]
common={'target':'현재원문과해석','scope':'F1~F4 및 계획 전체','source_locator':'outputs/업무정리_요구사항_최종확인본.md; 별도대조파일','rationale':'원문은 권고를 요청하고 로컬 전체범위를 제공한다. 설치/구현·숨은선호를 추가하지 않는다.','facet_ids':['F1','F2','F3','F4']}
review={'current_interpretation':'전체계획을 보존하는 개발방법/스킬 권고','assumptions':'조언 요청을 실제제품 구현으로 확대하지 않는다. 기술경로는 호스트제안이며 사용자 확정은 아니다.','result_properties':'반복사용 로컬폴더의 기록통일·업무수용판단·과거제출물재사용·원본복구에 쓰는 실행가능한 개발방법','interpretations':[{'id':'advice-scope','facet_ids':['F1','F2','F3','F4'],'result_scope':'전체제품을 대상으로 한 개발방법 권고','basis':'원문·맥락·현재명세','assumptions':'기술결정은 제안이며 제품구현·설치의 승인으로 확대하지 않음','difference':'화면제작만이 아닌 폴더정합성/복원/보수판단·전체검증을 포함','decision_impact':'초기기술/복원 관문과 전체개발순서를 유지','uncertainties':'실제제품검증과 전체명세승인은 남음','consumer_refs':cons,'disposition':'CURRENT','rationale':'원문목적을 충족하는 방법권고로 두고 원본자료를 보호한다.','reopen_condition':'요구변경이나 중요반박'}],
'support':dict(common,evidence_refs=[pr('ev-support'),{'id':'ev-plan','revision':1}]),
'omission':dict(common,evidence_refs=[pr('ev-omission'),{'id':'ev-plan','revision':1}],source_to_result='전체범위·원본·업무부담·재사용·복원을 개발관문에 보존',result_to_source='기술선택은 요구확정과 분리하며 임의설치/진척추적·클라우드전송 없음',counterfactual_scope='화면성공/백업존재가 원본보존·복원·판단유용성을 보장하지 않는 네 목적실패조건을 별도검토',observed_structures='Kopia내장자동GUI·WPF에도 가능한UIA배치의 차이를 비교·조건부계획에 반영',remaining_uncertainty_assessment='실제제품시험은 후속관문이며 현재해석의 중요미처리차이는 관측되지 않음',difference_ids=[]),
'handling':'CURRENT','treatment':'현재해석 유지. 별도지지/누락대조 결과를 기능·후보·5단계 계획·검증에 소비','termination_scope':'현재권고범위의 해석평가를 종료하며 실제제품성공/승인/숨은선호를 확정하지 않는다.',
'effects':[{'target_ref':x,'assessment':'MAINTAIN','evidence_refs':ir,'rationale':'현재cap조건의 원문보존/검증범위가 대조결과를 다루고 산출물에 실제소비됨'} for x in cons]}
review['support'].update(target='원문의 조언·스킬추천·SynFrame처리·로컬제품과 현재해석의 지지',source_locator='work/sf-support.json: findings F1~F4; 원문과 계획전체',rationale='원문과 명세가 전체계획의 개발방법 권고를 지지하며 제품구현/설치승인을 추가하지 않는다.')
review['omission'].update(target='현재추천이 성공해도 기록통일·업무수용판단·원본복구를 놓칠 조건',source_locator='work/sf-omission.json: source_to_result/result_to_source/counterfactual_scope/observed_structures',rationale='화면성공·백업존재·숫자표시·단계분리만으로 원문목적이 충족되지 않는 조건을 별도로 대조했다. 고유ID/재대조/과거원본복사·키포함별도복원·보수예상·전체인수로 처리하고 기술제안의 과잉확정을 제거했다.')
interp['extensions']={'interpretation_review':review,'requirements_change':{'rationale':'실제원문대조에서 각 능력의 판별범위를 구체화','evidence_refs':ir}}
interp['closure_assessment']={'verdict':'ANSWERED','rationale':'지지와 별도양방향누락·목적실패를 대조하고 기능/현재방법에 반영했다. 중요한미처리차이 없이 현재범위 종료.','evidence_refs':ir,'scope':'현재해석 처리','remaining_uncertainties':['제품인수·최종명세승인을 현재판단으로 대체하지 않음.'],'basis':'pending'}
links=[]
for task,targets,refs in [(interp,['cap-alignment','cap-delivery'],ir),(comp,['cap-selection','cap-roadmap'],er)]:
 for target in targets:links.append({'id':'consume-'+target,'type':'DependencyLink','sources':[pr(task['id'])],'target_ref':{'id':target,'revision':1},'relation':'AFFECTS','group_id':'advice-scope','evidence_refs':refs,'extensions':{'scope':'현재방법의 조건/선택/검증 소비','rationale':'실제학습·대조를 기존기능판단에 소비'}})
assets=[]
for ident,identity,role,evidence,provided in [
('asset-host','현재호스트 + SynFrame v3.2','실제계획대조·공식관측·조합·검토/결과보존',er+ir,['cap-alignment','cap-selection','cap-roadmap','cap-delivery']),
('asset-specframe','SpecFrame 명세 기준','확정/제안/미검증·승인 경계 보존',[{'id':'ev-plan','revision':1}],[]),
('asset-wpf','C# + WPF/.NET 기본API','제안된 첫 구현경로; 현재 앱실행 제공을 주장하지 않음',[{'id':'ev-window','revision':1},{'id':'ev-write','revision':1}],[]),
('asset-sqlite','Microsoft.Data.Sqlite','제안된 로컬조회색인과 지원DB백업',[{'id':'ev-store','revision':1}],[]),
('asset-restic','restic 로컬모드 문서0.19.1','제안된 백업엔진; 앱스케줄/실패표시/키복구/별도복원검증이 남는책임',[{'id':'ev-backup','revision':1},{'id':'ev-bpolicy','revision':1}],[]),
('asset-computeruse','computer-use @oai/sky','후속앱창검증; 현재는 지침·제약 확인. 금지동작 우회없음',[{'id':'ev-local','revision':1}],[])]:
 assets.append({'id':ident,'type':'KnowledgeAsset','kind':'method' if ident=='asset-host' else 'skill' if ident in ['asset-specframe','asset-computeruse'] else 'technology','identity':identity,'version':'문서/설치범위는 근거에 명시; 제품패키지버전은 구현전 고정','provided_capability_refs':[{'id':x,'revision':1} for x in provided],'applicability':role,'understanding':[{'context':'현재개발방법 권고의 역할/연결/실패조건 판단','level':3,'questions':[pr(comp['id'])],'evidence_refs':evidence,'stop_condition':'문서기반역할·조건판별은 확보. 실제제품 시험과 구별.'}],'inputs':[],'outputs':[],'availability':'available' if ident in ['asset-host','asset-specframe','asset-computeruse'] else 'knowledge_only','extensions':{'planned_product_asset':ident in ['asset-wpf','asset-sqlite','asset-restic'],'judgment_dependencies':[{'source_ref':pr(comp['id']),'role':'REUSES','required':True,'scope':'현재역할/조건평가','rationale':'선택/연결의 canonical판단을 재사용'}]}})
frame={'id':'frame-files-first','type':'Frame','goal_ref':gr,'object':'로컬폴더 업무흐름의 전체개발방법','representation':'정본자료·업무ID/상태·검증관문을 가진 앱 + 조회화면','mechanism':'기존기술 재사용과 실패/복원관문으로 단계별연결','intervention':'초기특수창/저장/복원시제품 뒤 전체범위연결','evidence_refs':er,'assumptions':['화면만완성해서는 기록보존·업무수용판단·복원목적이 보장되지 않음'],'parent_refs':[],'transformation':'initial','structural_difference':{'rationale':'기존목업확장 중심 접근과 달리 파일정체성·외부변경·복원가능성을 선행판별하여 UI와 독립해 검증한다.'},'information_requirements':[pr(comp['id']),pr(interp['id'])]}
candidate={'id':'candidate-local-native','type':'SolutionCandidate','goal_ref':gr,'frame_refs':[pr(frame['id'])],'asset_refs':[pr(a['id']) for a in assets],'capability_refs':[{'id':x,'revision':1} for x in ['cap-alignment','cap-selection','cap-roadmap','cap-delivery']],'connections':[],'gaps':[],'attributes':{'scope':'개발방법 권고를 현재호스트가 제공. 미래제품의 연결은 Architecture/ImplementationPlan에 구체화하고 실현성공을 주장하지 않음.','recommended_solution':contents['RecommendedSolution'],'architecture':contents['Architecture'],'selected_assets':contents['SelectedAssets'],'decision_rationale':contents['DecisionRationale'],'implementation_plan':contents['ImplementationPlan'],'validation_scope':contents['Provenance']['검증범위']},'comparison_rationale':contents['DecisionRationale']['WPF우선이유']+' '+contents['DecisionRationale']['간단구성과비교'],'disposition':'retain','constraint_values':{'local':True,'authority':True,'preserve':True},'extensions':{'judgment_dependencies':[{'source_ref':pr(comp['id']),'role':'COMPARES_USING','required':True,'scope':'현재조합/선택','rationale':'확인한후보역할·대안·조건을 실제소비'},{'source_ref':pr(interp['id']),'role':'REUSES','required':True,'scope':'원문/사용목적','rationale':'지지와누락대조의 현재결론 보존'}]}}
provides=[{'id':'provide-'+x,'type':'DependencyLink','sources':[pr('asset-host')],'target_ref':{'id':x,'revision':1},'relation':'PROVIDES','group_id':'actual-advice','evidence_refs':er+ir} for x in ['cap-alignment','cap-selection','cap-roadmap','cap-delivery']]
changes=[*evs,interp,comp,*links,*assets,frame,candidate,*provides]
projection=io.patch_refs(changes);save('sf-projection3.json',projection)
res=[]
for p in projection['propagation_obligations']:
 if p['status']!='PENDING':continue
 target=p['target_ref']['id']
 if target==comp['id']:assessment='REVISE';out=[pr(comp['id'])];reason='발견2를 실제 조합·조건·대안·작업에 소비하여 현재 조합답을 새버전으로 작성했다.'
 elif target in ['cap-alignment','cap-delivery','cap-selection','cap-roadmap']:assessment='MAINTAIN';out=[{'id':target,'revision':1}];reason='현재 기능조건이 원문/실패/검증범위를 보존함을 실제 대조하고 이번후보·산출물에 적용했다. 기능정의의 의미변경은 필요하지 않다.'
 else:continue
 res.append({'id':p['id'],'assessment':assessment,'evidence_refs':er+ir,'rationale':reason,'result_refs':out,'remaining_work':[]})
r=io.build_result(op,changes,result_ids=[x['id'] for x in changes],preserved_refs=[{'id':x,'revision':1} for x in ['cap-alignment','cap-delivery','cap-selection','cap-roadmap']],evidence_refs=er+ir,unresolved_items=['현재후보의 의미/구조 Review·Gate·채택·완료 확인'],observed_effects=['현재발견을 우선조합/조건부추가/대안/전체5단계에 실제소비','원문 지지와 별도목적누락대조로 현재범위 유지','실제제품미검증·승인대기를 보존'],next_decision_inputs=['현재후보 review-context로 버전/연결/능력·근거 대조'],outcome='SUCCEEDED',learning_results=[{'task_ref':ref(t['current']),'result_ref':pr(t['current']['id']),'answer_refs':ir if t['current']['id']==interp['id'] else er,'understanding_change':'지지/누락·목적실패를 구별하고 현재조건을 방법/검증에 반영' if t['current']['id']==interp['id'] else '역할·I/O·개입·복원·대안/조건부추가를 현재조합으로 선택','discovery_ids':[],'upward_effects':'현재기능조건은 실제대조로 유지하고 후보·산출물·검증관문을 새로 구성','observations':'sf-support.json+sf-omission.json' if t['current']['id']==interp['id'] else 'sf-comparison.json 및 두 출력파일','prediction_difference':'외부도구가 UI/backup을 모두보장하는것으로 확대하지 않고 호스트가 남는연결/검증을 맡는다.'} for t in c['selected_tasks']],propagation_resolutions=res,path_assessments=[],expected_revision=c['revision'])
for item in r['proposed_state_changes']:
 if item['type']=='LearningTask':item['closure_assessment']['basis']=io.call('learning-basis',payload=item)['basis']
save('sf-result3.json',r)
p=io.preflight(op,r);save('sf-preflight3.json',p);print(json.dumps({k:v for k,v in p.items() if k!='prepared_result'},ensure_ascii=False))
