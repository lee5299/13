using System.Text.Json.Serialization;

namespace WorkOrganizer.Core;

public enum WorkState { Review, Active, Waiting, Completed, Cancelled }
public enum RepeatMode { None, Monthly, YearlyApproximate }
public enum AiProvider { Claude, Codex }
public sealed class Submission
{
    public string Name { get; set; } = "";
    public List<string> Files { get; set; } = [];
}
public sealed class WorkItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "";
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.Now;
    public DateOnly RequestedOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? DueOn { get; set; }
    public decimal Effort { get; set; } = 1;
    public int Priority { get; set; } = 1;
    public WorkState State { get; set; } = WorkState.Review;
    public DateTimeOffset? CompletedAt { get; set; }
    public bool CompletionDateUnknown { get; set; }
    public DateOnly CycleStart { get; set; }
    public string? TypeId { get; set; }
    public string? Occurrence { get; set; }
    public List<Submission> Submissions { get; set; } = [];
    [JsonIgnore] public string Folder { get; set; } = "";
    [JsonIgnore] public bool Missing { get; set; }
    [JsonIgnore] public string MetadataHash { get; set; } = "";
    [JsonIgnore] public string FolderName => Path.GetFileName(Folder);
    [JsonIgnore] public string Request => WorkText.ReadSection(WorkText.LoadForDisplay(Folder), "요청 원문");
    [JsonIgnore] public bool Unclassified => !Missing && WorkText.IsUnclassified(Folder);
    [JsonIgnore] public string StateLabel => State switch { WorkState.Review => "검토중", WorkState.Active => "진행", WorkState.Waiting => "대기", WorkState.Completed => "완료", _ => "취소" };
}
public sealed class TemplateFile
{
    public string Path { get; set; } = "";
    public bool Selected { get; set; }
}
public sealed class WorkType
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public RepeatMode Repeat { get; set; }
    public int Month { get; set; } = 3;
    public int Day { get; set; } = 15;
    public List<TemplateFile> Templates { get; set; } = [];
    public List<string> SubmissionNames { get; set; } = [];
    public List<string> PastFiles { get; set; } = [];
    public List<string> SkippedOccurrences { get; set; } = [];
    public Dictionary<string, DateOnly> OccurrenceDates { get; set; } = [];
}
public sealed class AppSettings
{
    public List<CycleRule> CycleRules { get; set; } = [];
    public int CycleMonth { get; set; } = 1;
    public int CycleDay { get; set; } = 1;
    public bool ExcludeHolidays { get; set; }
    public List<DateOnly> Holidays { get; set; } = [];
    public List<TemplateFile> CommonTemplates { get; set; } = [];
    public List<WorkType> Types { get; set; } = [];
    public string BackupLocation { get; set; } = "";
    public int BackupMinutes { get; set; } = 60;
    public int KeepDaily { get; set; } = 30;
    public int KeepMonthly { get; set; } = 12;
    public bool BackupRecords { get; set; } = true;
    public bool BackupCaptures { get; set; } = true;
    public bool BackupResults { get; set; } = true;
    public string ResticPath { get; set; } = "";
    public DateTimeOffset? LastBackupAt { get; set; }
    public string LastBackupStatus { get; set; } = "미설정";
    // 주 로컬 백업은 기본 private 포함(BACKUP_LOCAL_INCLUDE). 수동 백업파일 범위와 별개(BACKUP_OPTION_INDEPENDENT).
    public bool BackupPrivate { get; set; } = true;
    public string LastPortableFolder { get; set; } = "";
    // AI 연결: 공식 프로그램을 앱이 뒤에서 호출. 과금 API로 자동 전환하지 않음(AI_NO_PAID_FALLBACK).
    public AiProvider AiProvider { get; set; } = AiProvider.Claude;
    public int AiTimeoutSeconds { get; set; } = 180;
    public string AiExecutablePath { get; set; } = "";
    // 다듬기에 쓸 모델. 비우면 각 프로그램의 기본 모델. Claude는 haiku/sonnet/opus 별칭 또는 전체 모델 이름.
    public string AiModel { get; set; } = "haiku";
    // 전화번호 사전검사의 사용자 예외(AI_EXTENSION_CUSTOM). 숫자만 비교, "re:" 접두는 정규식.
    public List<string> PhoneExceptions { get; set; } = [];
}
public sealed class CycleRule { public DateOnly EffectiveFrom { get; set; } public int Month { get; set; } public int Day { get; set; } }
public sealed record Forecast(string Name, DateOnly From, DateOnly To, string Description, string? TypeId, string? Occurrence, bool HasWork);

public static class WorkText
{
    // 공개기록: AI가 읽을 수 있는 영역(인터넷 게시 아님). 요청 목적·요구·맥락만 적는다(DOC_PUBLIC).
    public const string FileName = "공개기록.txt";
    // 이전 버전의 단일 기록. 공개/private 미분류이므로 AI 전송 대상에 넣지 않는다(MIGRATION_UNCLASSIFIED).
    public const string LegacyFileName = "업무기록.txt";
    // private: 명령어·구조명·민감 상세. 디스크에는 암호문만 둔다(DOC_PRIVATE_SAVE, AI_PRIVATE_DENY).
    public const string PrivateFolder = "private";
    public const string PrivateFileName = "상세기록.private";
    public const string LegacyArchiveName = "기존단일기록.private";
    public static readonly string[] Sections = ["요청 원문", "본인이 이해한 요청", "누가", "언제", "무엇을", "어떻게(목적)", "왜(요청이유)", "주의사항", "개선할 점", "작업 순서 · 실행 명령어", "오류 · 조치방법"];
    public static readonly string[] PublicSections = ["요청 원문", "본인이 이해한 요청", "누가", "언제", "무엇을", "어떻게(목적)", "왜(요청이유)", "주의사항", "개선할 점"];
    public static readonly string[] PrivateSections = ["작업 순서 · 실행 명령어", "오류 · 조치방법", "구조 명칭 · 민감 상세"];
    public static string Build(string original, string understood) => string.Join("\n\n", PublicSections.Select(s => $"============ {s}\n{(s == "요청 원문" ? original : s == "본인이 이해한 요청" ? understood : "")}")) + "\n";
    public static string BuildPrivate() => string.Join("\n\n", PrivateSections.Select(s => $"============ {s}\n")) + "\n";
    /// <summary>공개기록만 읽는다. 이전 단일기록으로 대체하지 않는다.</summary>
    public static string Load(string folder) => File.Exists(Path.Combine(folder, FileName)) ? File.ReadAllText(Path.Combine(folder, FileName)) : "";
    /// <summary>앱 화면 표시·로컬 검색용. 공개기록이 없으면 이전 단일기록을 읽는다. AI 전송에는 쓰지 않는다.</summary>
    public static string LoadForDisplay(string folder)
    {
        if (File.Exists(Path.Combine(folder, FileName))) return File.ReadAllText(Path.Combine(folder, FileName));
        return File.Exists(Path.Combine(folder, LegacyFileName)) ? File.ReadAllText(Path.Combine(folder, LegacyFileName)) : "";
    }
    /// <summary>폴더 바로 아래의 이전 단일기록 평문(업무기록*.txt, .previous 포함) 목록.</summary>
    public static List<string> LegacyFiles(string folder) => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder, "업무기록*").Where(f => { string n = Path.GetFileName(f); return n.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".txt.previous", StringComparison.OrdinalIgnoreCase); }).ToList()
        : [];
    public static bool IsUnclassified(string folder) => File.Exists(Path.Combine(folder, LegacyFileName)) && !File.Exists(Path.Combine(folder, FileName));
    /// <summary>받은 요청 원문 절(R06, 보존 대상)과 나머지 정리 내용을 나눈다. 다시 이으면 원래 텍스트와 같다.</summary>
    public static (string Original, string Remainder) SplitOriginal(string text)
    {
        text = text.Replace("\r\n", "\n");
        const string marker = "============ 요청 원문\n";
        int begin = text.IndexOf(marker, StringComparison.Ordinal);
        if (begin < 0) return ("", text);
        int end = text.IndexOf("\n============ ", begin + marker.Length, StringComparison.Ordinal);
        if (end < 0) return (text, "");
        return (text[..(end + 1)], text[(end + 1)..]);
    }
    public static string ReadSection(string text, string section)
    {
        string marker = "============ " + section + "\n";
        text = text.Replace("\r\n", "\n");
        int begin = text.IndexOf(marker, StringComparison.Ordinal);
        if (begin < 0) return section == "요청 원문" ? text.Split('\n').FirstOrDefault() ?? "" : "";
        begin += marker.Length;
        int end = text.IndexOf("\n============ ", begin, StringComparison.Ordinal);
        return text[begin..(end < 0 ? text.Length : end)].Trim();
    }
}
