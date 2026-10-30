from synframe_session import *
io=host();s=io.schemas('ReviewRecord','SolutionPackage');save('sf-final-schemas.json',s)
rv=json.loads((ROOT/'work/synframe-review-context.json').read_text(encoding='utf-8'))
print(json.dumps({'schemas':s,'information_needs':rv['information_needs'],'information_need_refs':rv['information_need_refs'],'independent_followups':rv['independent_followups'],'snapshot':rv['snapshot']},ensure_ascii=False))
