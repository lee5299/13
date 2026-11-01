using System;
using System.IO;
using WorkOrganizer.Core;

namespace WorkOrganizer.App;

/// <summary>실제 업무자료와 분리된 합성 시험자료. 업무정리 암호도 시험용 고정값이며 실제 자료에 쓰지 않는다.</summary>
public static class Demo
{
    public const string Passphrase = "시험자료-private-0000";
    public static string Create()
    {
        string root = Path.Combine(Program.ConfigFolder, "시험자료"); Directory.CreateDirectory(root); var store = new WorkStore(root);
        if (store.Scan().Count > 0) return root;
        using var vault = PrivateVault.Exists(store.Control) ? PrivateVault.Unlock(store.Control, Passphrase) : PrivateVault.Create(store.Control, Passphrase);
        var type = new WorkType { Name = "월간 현황자료", Repeat = RepeatMode.Monthly, Day = 15, SubmissionNames = ["C_1 결과물", "C_2 결과물"] }; var s = Disk.Clone(store.Settings); s.Types.Add(type); store.SaveSettings(s);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var a = store.Create("서버점검보고", "정기 점검 결과와 주의사항을 정리해주세요.", "점검 결과를 정리해 보고서로 준비합니다. 담당 연락처 010-1234-5678 확인.", today, 3, 2, today.AddDays(5), privateText: "시험용 명령: systemctl status 합성서비스", vault: vault); store.ChangeState(a, WorkState.Active);
        var b = store.Create(type.Name, "월간 현황 자료 2종을 준비해주세요.", "지난 양식을 복사해 이번 자료를 작성합니다.", today, .5m, 1, today, type, vault: vault); store.ChangeState(b, WorkState.Active);
        var c = store.Create("장비목록갱신", "장비 목록과 담당자를 확인해주세요.", "새 요청 수락 여부를 검토합니다.", today, 2, 1, null, vault: vault);
        string text = store.LoadPrivate(a, vault); for (int i = 1; i <= 65; i++) text += $"\n{i:00}. 시험 기록 — 합성 작업 순서와 명령어를 기록합니다.";
        store.SavePrivate(a, vault, text, WorkStore.PrivateHash(a));
        return root;
    }
}
