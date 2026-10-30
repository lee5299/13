from synframe_session import *
io=host();c=json.loads((ROOT/'work/sf-context4.json').read_text(encoding='utf-8'));op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'));ch=json.loads((ROOT/'work/sf-changes4b.json').read_text(encoding='utf-8'));p=json.loads((ROOT/'work/sf-projection4b.json').read_text(encoding='utf-8'));pr=HostIO.patch_ref
reasons={
'asset-host':'실제호스트의 명세대조·공식관측·조합판단을 수행한 ev-comparison/support/omission과 각 원자료를 출처로 연결했다. 현재 조언기능4개와 미래앱기능을 구별한 역할은 유지한다.',
'asset-specframe':'명세의 확정/제안/미검증·승인대기 역할을 ev-plan 원문에 연결했다. 이번 절차로 사용자승인을 바꾸지 않는다.',
'asset-wpf':'창꾸미기 문서와 파일교체 관측을 출처로 연결한다. 아이콘아래층 성공을 주장하지 않는 첫기술검증 역할과 knowledge_only 상태는 유지한다.',
'asset-sqlite':'실제읽은 BackupDatabase 문서의 API범위를 출처로 연결했다. 조회색인/일관된DB사본 계획과 실제통합시험의 구별을 유지한다.',
'asset-restic':'실제읽은0.19.1 스크립팅/로컬암호/별도복원·정리 조건을 연결했다. 앱이 맡을 키복구·스케줄/실패·복원시험을 없애지 않는다.',
'asset-computeruse':'실제읽은지침·금지와 도구조건을 출처로 연결했다. 초기창관측 역할 및 Win키제한·파일/계산검증과의 보완을 유지한다.'}
res=[]
for x in p['propagation_obligations']:
 if x['status']!='PENDING':continue
 t=x['target_ref']['id']
 if t in reasons:
  reason=reasons[t];ev=next(a['provenance'] for a in ch if a['id']==t)
 else:
  reason='현재조합2의 기본/조건부/대안·전체5단계를 실제담은 후보를 대조하고 자산2와 실제근거를 연결했다. 제품성공을 주장하지 않는다.' if x['source_ref']['id'].endswith('tool-composition') else '해석2의 별도지지/누락·목적실패조건과 후보의 ID/대조/복원/보수계산·전체검증을 대조했다. 원문범위를 유지하고 출처·자산참조만 현재판단으로 수정했다.'
  ev=[{'id':i,'revision':1} for i in ['ev-comparison','ev-support','ev-omission']]
 res.append({'id':x['id'],'assessment':'REVISE','evidence_refs':ev,'rationale':reason,'result_refs':[pr(t)],'remaining_work':[]})
r=io.build_result(op,ch,result_ids=[x['id'] for x in ch],preserved_refs=[],evidence_refs=[{'id':i,'revision':1} for i in ['ev-comparison','ev-support','ev-omission','ev-plan']],unresolved_items=['現在候補の意味/構造ReviewとGate/採択/終了'],observed_effects=['6資産を実際根拠に接続し、availabilityと文書/実行の範囲は維持','現在候補を比較・解釈に再対照し8件の反映を実際修正版に結合','閉じた学習を再開せず、過去判断は履歴として保持'],next_decision_inputs=['適用後の現在候補とReview構造・質問を確認'],outcome='SUCCEEDED',learning_results=[],propagation_resolutions=res,path_assessments=[],expected_revision=c['revision'])
save('sf-result4.json',r);v=io.preflight(op,r);save('sf-preflight4.json',v);print(json.dumps({k:v for k,v in v.items() if k!='prepared_result'},ensure_ascii=False))
