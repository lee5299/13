using System.Diagnostics;

namespace WorkOrganizer.Core;

public sealed class ExternalAiLock
{
    public DateTimeOffset StartedAt { get; set; }
    public string Tool { get; set; } = "";
    public int ProcessId { get; set; }
    public DateTime? ProcessStartTime { get; set; }
}

/// <summary>
/// 외부 폴더 AI 작업 잠금(PRIVATE_LOCK_FLOW). 시작 전에 private 평문이 루트에 없는지 확인하고(GATE),
/// 작업 중에는 private 키를 메모리에 두지 않으며(DURING), 끝낼 때 관련 프로세스 종료를 확인한다(END).
/// 상태를 파일에 남겨 앱이 비정상 종료돼도 다음 실행에서 이어서 확인한다(RECOVER).
/// </summary>
public static class ExternalAiSession
{
    public const string FileName = "외부AI잠금.json";
    public static string PathFor(WorkStore store) => Path.Combine(store.Control, FileName);
    public static ExternalAiLock? Read(WorkStore store) { try { return Disk.ReadJson<ExternalAiLock>(PathFor(store)); } catch { return new ExternalAiLock { Tool = "확인 불가(잠금 정보 손상)" }; } }
    public static bool IsActive(WorkStore store) => File.Exists(PathFor(store));

    public static void Begin(WorkStore store, PrivateVault? vault, string tool, Process? process)
    {
        if (vault is not null && !vault.IsLocked) throw new InvalidOperationException("private 키가 아직 메모리에 있습니다. 먼저 잠근 뒤 시작해주세요.");
        var blockers = store.ExternalAiBlockers();
        if (blockers.Count > 0) throw new InvalidOperationException("AI가 읽을 수 있는 private 평문이 있어 외부 AI 작업을 시작하지 않습니다.\n" + string.Join("\n", blockers.Take(10)) + (blockers.Count > 10 ? $"\n외 {blockers.Count - 10}건" : ""));
        Disk.WriteJson(PathFor(store), new ExternalAiLock { StartedAt = DateTimeOffset.Now, Tool = tool, ProcessId = process?.Id ?? 0, ProcessStartTime = process is null ? null : SafeStart(process) });
    }
    public static void Attach(WorkStore store, Process process)
    {
        var l = Read(store) ?? throw new InvalidOperationException("외부 AI 잠금이 시작되지 않았습니다.");
        l.ProcessId = process.Id; l.ProcessStartTime = SafeStart(process); Disk.WriteJson(PathFor(store), l);
    }
    /// <summary>잠금에 기록된 프로세스(창)가 아직 실행 중인지. 같은 번호를 다른 프로그램이 다시 쓴 경우는 시작 시각으로 구분한다.</summary>
    public static bool IsProcessAlive(ExternalAiLock l)
    {
        if (l.ProcessId <= 0) return false;
        try
        {
            using var p = Process.GetProcessById(l.ProcessId);
            if (p.HasExited) return false;
            return l.ProcessStartTime is null || SafeStart(p) == l.ProcessStartTime;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
    /// <summary>이름이 claude/codex인 실행 중 프로세스 수. 앱이 시작하지 않은 것도 포함해 안내용으로만 쓴다.</summary>
    public static int OtherAiProcesses() => new[] { "claude", "codex" }.Sum(n => { try { var list = Process.GetProcessesByName(n); foreach (var p in list) p.Dispose(); return list.Length; } catch { return 0; } });
    public static void End(WorkStore store, bool terminate)
    {
        var l = Read(store);
        if (l is not null && IsProcessAlive(l))
        {
            if (!terminate) throw new InvalidOperationException("외부 AI 작업 창이 아직 실행 중입니다. 작업을 끝내고 창을 닫은 뒤 다시 확인하거나, 종료 후 해제를 선택해주세요.");
            using var p = Process.GetProcessById(l.ProcessId); p.Kill(true); p.WaitForExit(10_000);
            if (!p.HasExited) throw new InvalidOperationException("외부 AI 작업 창을 종료하지 못했습니다. 잠금을 유지합니다.");
        }
        File.Delete(PathFor(store));
    }
    private static DateTime? SafeStart(Process p) { try { return p.StartTime.ToUniversalTime(); } catch { return null; } }
}
