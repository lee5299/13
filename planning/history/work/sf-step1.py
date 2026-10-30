from synframe_session import *
io=host(); op=json.loads((ROOT/'work/synframe-operation.json').read_text(encoding='utf-8'))
op=io.preserve_files(op,[(ROOT/'outputs/업무정리_요구사항_최종확인본.md','사용자 계획의 전체 요구사항 출력; 이번 intent 뒤 전문 대조'),(ROOT/'outputs/업무정리_개발진행권고안.md','이전 비관리 권고안; 현재 hash 일치만 확인, 결론은 재평가 대상')])
save('synframe-operation.json',op)
print(json.dumps(op,ensure_ascii=False))
