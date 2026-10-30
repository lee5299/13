from synframe_session import *
c=json.loads((ROOT/'work/sf-context5.json').read_text(encoding='utf-8'))
for t in c['requirements_context']['learning_tasks']:
 if t['id'].endswith('tool-discovery'):print(json.dumps(t,ensure_ascii=False))
