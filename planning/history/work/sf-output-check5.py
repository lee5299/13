from synframe_session import *
import hashlib
from datetime import datetime,timezone
contents=json.loads((ROOT/'work/sf-package-contents.json').read_text(encoding='utf-8'))
report=ROOT/'outputs/업무정리_SynFrame_개발계획.html';jp=ROOT/'outputs/업무정리_SynFrame_개발계획.json'
actual=json.loads(jp.read_text(encoding='utf-8'));html=report.read_text(encoding='utf-8')
assert actual==contents
assert all(k in html for k in contents)
rv=json.loads((ROOT/'work/synframe-review-context.json').read_text(encoding='utf-8'));cand=rv['selection_context']['candidate']
for a,b in [('recommended_solution','RecommendedSolution'),('architecture','Architecture'),('selected_assets','SelectedAssets'),('decision_rationale','DecisionRationale'),('implementation_plan','ImplementationPlan')]:assert cand['attributes'][a]==contents[b]
final='''**다음은 개발설계서 작성과 핵심 동작 시제품 검증입니다.** 우선 개발 경로는 C#·WPF로 권고합니다. 실제 바탕화면 레이어 동작과 읽기 편한 기록 30~50줄은 Windows에서 먼저 검증해야 하며, 전체 요구사항의 최종 승인은 아직 대기 상태입니다.

개발에는 다음 조합을 권고합니다.

| 구성 | 맡길 역할 |
|---|---|
| **SynFrame** | 기술 조사·비교, 선택 근거와 검증 결과 관리 |
| **SpecFrame** | 기존 요구사항과 확정·제안·미검증 구분 유지 |
| **C#·WPF** | 제가 구현할 Windows 창·기록 편집·로컬 파일 기능 |
| **SQLite** | 작업폴더의 관리정보를 바탕으로 목록·날짜 검색용 색인 구성 |
| **restic** | 앱이 실행·관리하는 로컬 이력 백업과 복원 |
| **computer-use** | 실제 실행 창의 조작과 화면 확인 |

WPF의 투명 창 기능만으로 아이콘 아래층 동작이 보장되지는 않습니다. 백업도 restic을 연결하는 데서 끝나지 않고, **암호·복구정보 보관과 별도 위치 복원 시험**까지 확인해야 합니다. 실제 백업 시간은 자료와 장치가 정해진 뒤 측정합니다. [WPF 창 문서](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/), [restic 복원 문서](https://restic.readthedocs.io/en/stable/050_restore.html)

진행 순서는 다음과 같습니다.

1. **개발설계서**: 요구사항마다 저장 원본, 화면, 상태 변경, 오류·복구, 확인 방법을 연결합니다. 기존 목업의 착수 날짜·목록 복사 등 최신 요구와 다른 부분도 정리합니다.
2. **핵심 시제품**: 바탕화면 표시, 큰 기록창, 외부 편집 충돌, 탐색기 완료·복귀, 백업·복원을 시험합니다.
3. **전체 기능 연결**: 요청 한 건의 생성부터 완료까지 연결한 뒤, 주·월 여유, 반복업무·지난 제출물, 기간 검색과 설정을 완성합니다. 오늘 포함 계산과 사용일수 입력 제외를 유지합니다.

반복 화면검사가 필요해지면 **Microsoft winui-ui-testing**을 조건부로 추가할 수 있습니다. WPF도 지원하며 실제 빌드·안정된 화면 식별자와 필요한 실행환경을 갖춘 뒤 적용합니다. WinUI·Tauri·Avalonia와 Kopia는 대안으로 비교했으며, 지금 모두 설치할 필요는 없습니다.

SynFrame의 조사·조합·최종 검토와 채택 절차를 마쳤습니다. 완료 범위는 **개발 방법 권고**이며, 프로그램 구현·성능·복원 성공을 검증한 상태는 아닙니다.

[D:\\13에 저장한 개발 계획·비교 근거 보기](D:/13/업무정리_SynFrame_개발계획.html)
'''
fp=ROOT/'outputs/업무정리_SynFrame_결론.md'
assert not fp.exists()
fp.write_text(final,encoding='utf-8');assert fp.read_text(encoding='utf-8')==final
checks={'assessed_at':datetime.now(timezone.utc).isoformat(),'checks':{'json_readback':'ten authored fields equal','html_content':'all ten keys and charset exist; no visual render performed','candidate_package':'five selected architecture/decision/plan fields exactly equal','source_review':'최종요구사항25절 전체를 현재다시읽고 원문대화/현재권고와 대조. 전체범위 유지·기술제안/승인대기·실제제품미검증을 구분.','final_review':'첫추천과 조건/자산역할/선택과 대안·순서/백업암호복원/개발환경/현재권고범위가 후보와 일치. 최종완료문장은 실제채택/완료전이뒤만 반환한다.'},'files':[{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in [report,jp,fp]],'limits':['형식/동일성은 추천의진실이나 실제제품성능 검증이 아님','프로그램미구현/명세승인대기','사용자PC의실제화면/백업미실행']}
save('sf-artifact-check5.json',checks)
print(json.dumps(checks,ensure_ascii=False))
