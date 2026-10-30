import json, os, sys
from pathlib import Path
os.environ['PYTHONUTF8']='1'
sys.path.insert(0,r'C:\Users\Administrator\.codex\skills\synframe\scripts')
from synframe_host import HostIO
ROOT=Path(r'C:\Users\Administrator\Documents\Codex\2026-10-29\new-chat-3')
STATE=ROOT/'.synframe'
RUNFILE=ROOT/'work/synframe-run.json'
def save(name,value):
    p=ROOT/'work'/name
    p.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
    return p
def host():
    return HostIO(STATE,json.loads(RUNFILE.read_text(encoding='utf-8'))['id'])
if __name__=='__main__':
    command=sys.argv[1]
    if command=='start':
        goal={
          'id':'work-organizer-advice','type':'GoalSpec',
          'objective':'계획한 Windows 로컬 업무 정리 프로그램의 요구사항을 검토하고, 개발 진행 방법과 적절한 스킬·기술 조합을 근거와 검증 순서까지 제안한다. SynFrame 계약에 따라 조사·비교·검토·채택·결과를 보존한다.',
          'outcome_type':'specification',
          'source_request':'우리가 계획한 문서를 보고 어떻게 진행하는게 좋을지 알려주세요.\n우리가 계획하고 적절한 스킬을 추천해주십시오.\nSynFrame 지침을따라 처리해주세요.\n우리가 만들 프로그램은 로컬에서 돌아가는 프로그램입니다.',
          'success_criteria':[
             {'id':'C1','description':'최신 계획 전체와 대화의 확정·제안·미검증을 보존하고, 사용 목적을 놓칠 수 있는 차이와 구현 전 필요한 결정을 구별한다.'},
             {'id':'C2','description':'실제 기능·입출력·환경·실패/복구 조건에 근거해 기존 및 외부 스킬·기술을 비교하고 한 가지 우선 진행 조합과 조건·대안을 제시한다.'},
             {'id':'C3','description':'폴더·텍스트·탐색기 변경·업무부담·반복·백업을 빠뜨리지 않는 개발 순서와 관찰 가능한 통과 조건을 제공한다.'},
             {'id':'C4','description':'근거·한계·산출물과 SynFrame 현재 검토/채택/완료 상태가 일치하고, 로컬 파일로 다시 확인할 수 있는 결과를 제공한다.'}],
          'explicit_constraints':[
             {'id':'local','description':'완성 제품은 Windows 개인용 로컬 프로그램이며 업무자료는 PC 내부에 관리한다. 조사에 실제 업무파일을 외부 전송하지 않는다.','predicate':None},
             {'id':'authority','description':'현재 요청은 문서 검토와 진행 방법·스킬 추천이다. 요구사항 최종 승인은 대기 중이며 이 요청을 프로그램 전체 구현·설치·배포 승인으로 바꾸지 않는다.','predicate':None},
             {'id':'preserve','description':'전체 계획 범위, 진행률/실사용일수 입력 제외, 오늘 포함 계산, 요청별 폴더와 원본 보존, 디자인 기준을 유지한다.','predicate':None}],
          'inferred_requirements':['추천 전에 로컬 자료의 원본/색인·정체성·외부 변경 대조·저장 실패·백업 일관성 원리를 판별해야 한다.','바탕화면 특수 동작은 문서상의 창 꾸미기와 실제 동작 검증을 구별해야 한다.'],
          'proposed_means':['설치된 스킬과 공식 기술 문서 조사','기존 권고안의 관측과 결론을 구별하여 재사용하고, 부족한 기술 판단을 추가 조사'],
          'extensions':{'goal_alignment':{'facets':[
             {'id':'F1','source_quote':'우리가 계획한 문서를 보고 어떻게 진행하는게 좋을지 알려주세요.','interpretation':'전체 계획에 대한 실행 가능한 개발 방법 권고. 실제 구현 요청과 구분.','criterion_ids':['C1','C3']},
             {'id':'F2','source_quote':'우리가 계획하고 적절한 스킬을 추천해주십시오.','interpretation':'보유 및 외부 스킬의 실제 역할·연결·조건을 조사하고 선택.','criterion_ids':['C2']},
             {'id':'F3','source_quote':'SynFrame 지침을따라 처리해주세요.','interpretation':'운영 계약 전체 로딩 후 실제 관리 루프와 현재 Review·Gate·채택·종료 보존.','criterion_ids':['C4']},
             {'id':'F4','source_quote':'우리가 만들 프로그램은 로컬에서 돌아가는 프로그램입니다.','interpretation':'Windows 로컬 제품의 폴더·편집·백업·바탕화면 조건 유지. 웹사이트 게시로 대체하지 않음.','criterion_ids':['C1','C2','C3']}],
             'preservation_rationale':'문서 읽기/추천/절차 준수/로컬 제품을 별도 의미 단위로 보존한다. 계획에 명시된 전체 기능은 범위로 유지하되 이번 완료 기준은 그 구현 완료가 아닌 개발 방법 권고의 충분성과 근거다.'}}}
        io=HostIO(STATE)
        run=io.start(goal)
        save('synframe-run.json',run)
        print(json.dumps(run,ensure_ascii=False))
    elif command=='call':
        io=host()
        result=io.call(sys.argv[2],**json.loads(sys.argv[3] if len(sys.argv)>3 else '{}'))
        save('synframe-last.json',result)
        print(json.dumps(result,ensure_ascii=False))
    elif command=='begin':
        data=json.loads(Path(sys.argv[2]).read_text(encoding='utf-8'))
        result=host().begin(**data)
        save('synframe-operation.json',result)
        print(json.dumps(result,ensure_ascii=False))
    elif command=='context':
        op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8')) if len(sys.argv)>2 else None
        result=host().work_context(op)
        save('synframe-context.json',result)
        print(json.dumps(result,ensure_ascii=False))
    elif command=='preserve':
        op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'))
        files=json.loads(Path(sys.argv[2]).read_text(encoding='utf-8'))
        result=host().preserve_files(op,files)
        save('synframe-operation.json',result)
        print(json.dumps(result,ensure_ascii=False))
