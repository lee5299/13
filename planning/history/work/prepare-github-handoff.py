from pathlib import Path
import shutil,json,hashlib,sqlite3
root=Path(r'C:\Users\Administrator\Documents\Codex\2026-10-29\new-chat-3')
repo=root/'outputs/13-repository'
source=Path(r'D:\13\업무정리')
for base in ['src','tests/WorkOrganizer.Checks']:
    for p in (source/base).rglob('*'):
        if p.is_file() and not any(x in ('bin','obj','검사자료') for x in p.relative_to(source).parts):
            dest=repo/p.relative_to(source); dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
shutil.copy2(source/'build.ps1',repo/'build.ps1')
tool=repo/'도구';tool.mkdir(exist_ok=True)
for name in ['restic-LICENSE.txt','SHA256SUMS']:shutil.copy2(source/'도구'/name,tool/name)
design=repo/'design';design.mkdir(exist_ok=True)
for name in ['업무정리_목업.html','바탕화면 목록 + 작업 상세.png','완료 작업 창.png']:shutil.copy2(Path(r'D:\13\업무 정리 목업-png')/name,design/name)
shutil.copy2(Path(r'D:\13\업무정리_목업_작업기록확대.html'),design/'업무정리_목업_작업기록확대.html')
planning=repo/'planning';planning.mkdir(exist_ok=True)
state=json.loads((root/'.specframe/01a1862d-c7a4-7592-b76a-6cf47c8930d1.json').read_text(encoding='utf-8'))
assert state['approval']=='approved'
body='# 확정 요구사항 · 개발 기준\n\n원본 SpecFrame 상태의 승인값: approved. 사용자 명시 개발 시작 지시에 따른 현재 전체 요구이며 실제 제품 인수 완료를 뜻하지 않는다. 아래 본문은 상태의 25개 절을 그대로 내보낸 것이다.\n\n'
body+='\n\n'.join('## '+k+'\n\n'+v for k,v in state['sections'].items())+'\n'
(planning/'확정요구사항.md').write_text(body,encoding='utf-8')
history=planning/'history';history.mkdir(exist_ok=True)
shutil.copy2(root/'.specframe/01a1862d-c7a4-7592-b76a-6cf47c8930d1.json',history/'specframe-state.json')
db=sqlite3.connect('file:'+str(root/'.synframe/state.sqlite3').replace('\\','/')+'?mode=ro',uri=True)
archive=sqlite3.connect(history/'synframe-state.sqlite3');db.backup(archive);archive.close();db.close()
mapping=[]
for p in (root/'work').iterdir():
    if p.is_file():
        dest=history/'work'/p.name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
        mapping.append({'original':str(p),'copy':str(dest.relative_to(repo)),'sha256':hashlib.sha256(dest.read_bytes()).hexdigest()})
for name in ['업무정리_SynFrame_개발계획.html','업무정리_SynFrame_개발계획.json','업무정리_개발진행권고안.md']:
    p=root/'outputs'/name
    if p.is_file():
        dest=history/'reports'/name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
test=source/'tests/검사자료/20261030-160636-bc8e4a/검사결과/결과.json'
result=json.loads(test.read_text(encoding='utf-8'));assert result['passed']==40
shutil.copy2(test,repo/'docs/검사결과.json')
(history/'경로대응.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'repo':str(repo),'requirement_sections':len(state['sections']),'test_passed':result['passed'],'files':sum(1 for p in repo.rglob('*') if p.is_file() and '.git' not in p.parts)},ensure_ascii=False))
