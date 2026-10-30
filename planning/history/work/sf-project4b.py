from synframe_session import *
io=host();ch=json.loads((ROOT/'work/sf-changes4.json').read_text(encoding='utf-8'))
for x in ch:
 if x['type']=='SolutionCandidate':
  x['extensions'].pop('functional_proof_refs',None);x['extensions'].pop('selection_validity',None)
save('sf-changes4b.json',ch);p=io.patch_refs(ch);save('sf-projection4b.json',p)
print(json.dumps({'observed_revision':p['observed_revision'],'refs':p['refs'],'reference_conflicts':p['reference_conflicts'],'invalidated_consumers':p['invalidated_consumers'],'pending':[x for x in p['propagation_obligations'] if x['status']=='PENDING']},ensure_ascii=False))
