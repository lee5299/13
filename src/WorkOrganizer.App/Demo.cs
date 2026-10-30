using System;
using System.IO;
using WorkOrganizer.Core;

namespace WorkOrganizer.App;

public static class Demo
{
    public static string Create()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "시험자료"); Directory.CreateDirectory(root); var store = new WorkStore(root);
        if (store.Scan().Count > 0) return root;
        var type = new WorkType { Name = "월간 현황자료", Repeat = RepeatMode.Monthly, Day = 15, SubmissionNames = ["C_1 결과물", "C_2 결과물"] }; var s = Disk.Clone(store.Settings); s.Types.Add(type); store.SaveSettings(s);
        var today = DateOnly.FromDateTime(DateTime.Today); var a = store.Create("서버점검보고", "정기 점검 결과와 주의사항을 정리해주세요.", "점검 절차와 오류 조치를 기록하고 보고서를 준비합니다.", today, 3, 2, today.AddDays(5)); store.ChangeState(a, WorkState.Active);
        var b = store.Create(type.Name, "월간 현황 자료 2종을 준비해주세요.", "지난 양식을 복사해 이번 자료를 작성합니다.", today, .5m, 1, today, type); store.ChangeState(b, WorkState.Active);
        var c = store.Create("장비목록갱신", "장비 목록과 담당자를 확인해주세요.", "새 요청 수락 여부를 검토합니다.", today, 2, 1, null);
        string text = WorkText.Load(a.Folder); for (int i = 1; i <= 65; i++) text += $"\n{i:00}. 시험 기록 — 자료 확인 및 처리 내용을 기록합니다."; store.SaveText(a, text, Disk.Hash(Path.Combine(a.Folder, WorkText.FileName)));
        return root;
    }
}
