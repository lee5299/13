import json,re
from pathlib import Path
p=Path(r'C:\Users\Administrator\Documents\Codex\2026-10-29\new-chat-3')
s=json.loads(next((p/'.specframe').glob('*.json')).read_text(encoding='utf-8'))
print(list(s))
print({k:type(v).__name__ for k,v in s.items()})
text=(p/'outputs/업무정리_요구사항_최종확인본.md').read_text(encoding='utf-8')
norm=lambda v:re.sub(r'\s+','',v.replace('**',''))
missing=[k for k,v in s['sections'].items() if norm(v) not in norm(text)]
print(json.dumps({'section_count':len(s['sections']),'not_identical_in_read_export':missing,'review':{k:v for k,v in s.items() if k not in ['sections']}},ensure_ascii=False))
