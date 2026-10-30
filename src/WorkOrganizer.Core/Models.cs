using System.Text.Json.Serialization;

namespace WorkOrganizer.Core;

public enum WorkState { Review, Active, Waiting, Completed, Cancelled }
public enum RepeatMode { None, Monthly, YearlyApproximate }
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
    [JsonIgnore] public string Request => WorkText.ReadSection(WorkText.Load(Folder), "요청 원문");
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
}
public sealed class CycleRule { public DateOnly EffectiveFrom { get; set; } public int Month { get; set; } public int Day { get; set; } }
public sealed record Forecast(string Name, DateOnly From, DateOnly To, string Description, string? TypeId, string? Occurrence, bool HasWork);

public static class WorkText
{
    public const string FileName = "업무기록.txt";
    public static readonly string[] Sections = ["요청 원문", "본인이 이해한 요청", "누가", "언제", "무엇을", "어떻게(목적)", "왜(요청이유)", "주의사항", "개선할 점", "작업 순서 · 실행 명령어", "오류 · 조치방법"];
    public static string Build(string original, string understood) => string.Join("\n\n", Sections.Select(s => $"============ {s}\n{(s == "요청 원문" ? original : s == "본인이 이해한 요청" ? understood : "")}")) + "\n";
    public static string Load(string folder) => File.Exists(Path.Combine(folder, FileName)) ? File.ReadAllText(Path.Combine(folder, FileName)) : "";
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
