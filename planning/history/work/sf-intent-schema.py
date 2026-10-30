from synframe_session import *
io=host();s=io.call('schema',for_command='intent');save('sf-intent-schema.json',s);print(json.dumps(s,ensure_ascii=False))
