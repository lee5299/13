from synframe_session import *
io=host(); op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'))
c=io.work_context(op);save('sf-context4.json',c)
p=json.loads((ROOT/'work/sf-projection4.json').read_text(encoding='utf-8'))
print(json.dumps({'context_keys':list(c),'revision':c['revision'],'digest':c['state_digest'],'goal':c['goal'],'pending_propagation_count':c['pending_propagation_count'],'operation':c.get('operation'),'projection':{k:v for k,v in p.items() if k not in ['resolved_changes','invalidated_consumers']}},ensure_ascii=False))
