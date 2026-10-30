from synframe_session import *
c=json.loads((ROOT/'work/sf-context4.json').read_text(encoding='utf-8'));p=json.loads((ROOT/'work/sf-projection4.json').read_text(encoding='utf-8'))
print(json.dumps({'compatible':c['compatible'],'identity':c['identity'],'unfinished_learning':c['unfinished_learning'],'pending_operations':c['pending_operations'],'requirements':{k:v for k,v in c['requirements_context'].items() if k in ['criteria','capabilities','question_links','consumers']},'invalidated_consumers':p['invalidated_consumers']},ensure_ascii=False))
