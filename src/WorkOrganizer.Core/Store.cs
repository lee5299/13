using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkOrganizer.Core;

public static class Disk
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static string Hash(string path) => File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "";
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
    public static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { byte[] bytes = new UTF8Encoding(false).GetBytes(text); stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, path + ".previous", true); else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static void WriteJson<T>(string path, T value) => Write(path, JsonSerializer.Serialize(value, Json));
    public static T? ReadJson<T>(string path) => File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : default;
    public static bool IsInside(string path, string parent) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(path).StartsWith(Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static string SafeName(string value, int max = 90)
    {
        var bad = Path.GetInvalidFileNameChars();
        var name = new string(value.Select(c => bad.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().TrimEnd('.', ' ');
        if (name.Length > max) name = name[..max].TrimEnd();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("이름을 입력해주세요.");
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "LPT1", "LPT2" }.Contains(name.ToUpperInvariant())) name = "_" + name;
        return name;
    }
}

public sealed class WorkStore
{
    private readonly object sync = new();
    public const string MetaName = ".업무.json";
    public string Root { get; }
    public string Control => Path.Combine(Root, ".업무정리");
    public AppSettings Settings { get; private set; }
    public List<string> Issues { get; } = [];
    public List<string> NewFolders { get; } = [];
    private List<WorkItem> known;
    public WorkStore(string root)
    {
        Root = Path.GetFullPath(root);
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("루트폴더에 접근할 수 없습니다. 재연결해주세요.");
        Directory.CreateDirectory(Control);
        Settings = Disk.ReadJson<AppSettings>(Path.Combine(Control, "설정.json")) ?? new();
        known = (Disk.ReadJson<List<StoredWork>>(Path.Combine(Control, "업무목록.json")) ?? []).Select(x => { x.Item.Folder = x.Folder; return x.Item; }).ToList();
        SaveSettings(Settings);
        EnsureCycle(DateOnly.FromDateTime(DateTime.Today));
    }
    public void SaveSettings(AppSettings settings) { lock (sync) { SaveSettingsCore(settings); } }
    private void SaveSettingsCore(AppSettings settings)
    {
        ValidateSettings(settings);
        SaveValidatedSettings(settings);
    }
    public void ValidateSettings(AppSettings settings)
    {
        if (settings.CycleMonth is < 1 or > 12 || settings.CycleDay is < 1 or > 31) throw new ArgumentException("주기 시작 월/일을 확인해주세요.");
        if (settings.BackupMinutes < 1 || settings.KeepDaily < 1 || settings.KeepMonthly < 1) throw new ArgumentException("백업 간격과 보존 개수는 1 이상이어야 합니다.");
        if (!string.IsNullOrWhiteSpace(settings.BackupLocation) && !Path.IsPathFullyQualified(settings.BackupLocation)) throw new ArgumentException("백업 위치는 드라이브부터 시작하는 전체 경로로 지정해주세요.");
        if (!string.IsNullOrWhiteSpace(settings.BackupLocation) && (Disk.IsInside(settings.BackupLocation, Root) || Disk.IsInside(Root, settings.BackupLocation))) throw new ArgumentException("백업 위치와 업무 루트는 서로 포함되지 않는 별도 폴더로 지정해주세요.");
    }
    private void SaveValidatedSettings(AppSettings settings)
    {
        if (known.Count > 0 && (Settings.CycleMonth != settings.CycleMonth || Settings.CycleDay != settings.CycleDay))
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (settings.CycleRules.Count == 0) settings.CycleRules.Add(new() { EffectiveFrom = DateOnly.MinValue, Month = Settings.CycleMonth, Day = Settings.CycleDay });
            settings.CycleRules.RemoveAll(r => r.EffectiveFrom > today);
            var next = new DateOnly(today.Year, settings.CycleMonth, Math.Min(settings.CycleDay, DateTime.DaysInMonth(today.Year, settings.CycleMonth)));
            if (next <= today) next = new(today.Year + 1, settings.CycleMonth, Math.Min(settings.CycleDay, DateTime.DaysInMonth(today.Year + 1, settings.CycleMonth)));
            settings.CycleRules.Add(new() { EffectiveFrom = next, Month = settings.CycleMonth, Day = settings.CycleDay });
        }
        Disk.WriteJson(Path.Combine(Control, "설정.json"), settings); Settings = settings;
    }
    public string CycleFolder(DateOnly start) => Path.Combine(Root, $"{start:yyyy-MM-dd}주기");
    public void EnsureCycle(DateOnly date)
    {
        string c = CycleFolder(Planning.Cycle(date, Settings));
        Directory.CreateDirectory(Path.Combine(c, "작업")); Directory.CreateDirectory(Path.Combine(c, "완료"));
    }
    public List<WorkItem> Scan() { lock (sync) { return ScanCore(); } }
    private List<WorkItem> ScanCore()
    {
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("업무 루트 연결이 끊겼습니다. 자료는 삭제되지 않았습니다.");
        EnsureCycle(DateOnly.FromDateTime(DateTime.Today)); Issues.Clear(); NewFolders.Clear();
        foreach (string journal in Directory.EnumerateFiles(Control, "*의도-*.json")) Issues.Add("중단된 작업 확인 필요: " + Path.GetFileName(journal));
        var found = new List<WorkItem>();
        foreach (string cycle in Directory.EnumerateDirectories(Root, "*주기"))
        foreach (string areaName in new[] { "작업", "완료" })
        {
            string area = Path.Combine(cycle, areaName); if (!Directory.Exists(area)) continue;
            foreach (string folder in Directory.EnumerateDirectories(area))
            {
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) { Issues.Add($"연결 폴더는 자동 탐색하지 않습니다: {folder}"); continue; }
                string meta = Path.Combine(folder, MetaName);
                if (!File.Exists(meta)) { NewFolders.Add(folder); continue; }
                try
                {
                    var x = Disk.ReadJson<WorkItem>(meta) ?? throw new IOException("빈 업무정보"); x.Folder = folder;
                    if (x.SchemaVersion != 1 || string.IsNullOrWhiteSpace(x.Id) || x.Effort <= 0) throw new IOException("업무정보 형식/예상량 오류");
                    string expectedArea = x.State == WorkState.Completed ? "완료" : "작업";
                    if (expectedArea != areaName)
                    {
                        if (areaName == "완료") { x.State = WorkState.Completed; x.CompletedAt = null; x.CompletionDateUnknown = true; }
                        else { x.State = WorkState.Active; x.CompletedAt = null; x.CompletionDateUnknown = false; }
                        Disk.WriteJson(meta, x);
                    }
                    x.MetadataHash = Disk.Hash(meta); found.Add(x);
                }
                catch (Exception ex) { Issues.Add($"업무정보 읽기/저장 확인 필요 · {Path.GetFileName(folder)}: {ex.Message}"); }
            }
        }
        var duplicateIds = found.GroupBy(x => x.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (string id in duplicateIds) Issues.Add("고유 ID가 복제된 폴더를 발견했습니다. 합치지 않습니다: " + string.Join(" / ", found.Where(x => x.Id == id).Select(x => x.Folder)));
        found.RemoveAll(x => duplicateIds.Contains(x.Id));
        foreach (var previous in known.Where(x => found.All(f => f.Id != x.Id)))
        { previous.Missing = true; found.Add(previous); Issues.Add($"위치 확인 필요: {previous.Name} · 자동 삭제하지 않음"); }
        known = found; SaveManifest();
        return found;
    }
    public WorkItem Create(string name, string original, string understood, DateOnly requestDate, decimal effort, int priority, DateOnly? due, WorkType? type = null, string? occurrence = null, string? existingFolder = null, string? requestKey = null) { lock (sync) return CreateCore(name, original, understood, requestDate, effort, priority, due, type, occurrence, existingFolder, requestKey); }
    private WorkItem CreateCore(string name, string original, string understood, DateOnly requestDate, decimal effort, int priority, DateOnly? due, WorkType? type = null, string? occurrence = null, string? existingFolder = null, string? requestKey = null)
    {
        if (effort <= 0 || effort > 100000) throw new ArgumentException("예상 소요일수는 0보다 큰 숫자로 입력해주세요.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("작업명을 입력해주세요.");
        requestKey ??= Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(requestKey, "N", out _)) throw new ArgumentException("요청 식별정보가 올바르지 않습니다.");
        var currentItems = Scan();
        var already = currentItems.SingleOrDefault(x => x.Id == requestKey);
        if (already is not null)
        {
            if (already.Missing || already.Name != name.Trim() || already.Effort != effort || already.RequestedOn != requestDate || already.DueOn != due || already.TypeId != type?.Id || already.Request != original.Trim()) throw new IOException("이 요청의 앞선 저장 결과가 있습니다. 중복 생성하지 않습니다. 폴더에서 앞선 결과와 입력을 확인해주세요.");
            return already;
        }
        if (occurrence is not null && Scan().Any(x => x.Occurrence == occurrence && x.State != WorkState.Cancelled)) throw new InvalidOperationException("이미 해당 회차의 업무가 있습니다.");
        var now = DateOnly.FromDateTime(DateTime.Today); EnsureCycle(now); var cycle = Planning.Cycle(now, Settings);
        string parent = Path.Combine(CycleFolder(cycle), "작업");
        string desired = $"{requestDate:yyMMdd}_{Disk.SafeName(name, 45)}_{Disk.SafeName(string.IsNullOrWhiteSpace(original) ? "요청내용" : original, 65)}";
        string target = Path.Combine(parent, desired); int n = 2;
        while (Directory.Exists(target)) target = Path.Combine(parent, desired + $"_{n++:00}");
        if (existingFolder is not null)
        {
            target = Path.GetFullPath(existingFolder);
            if (!Directory.Exists(target) || !Path.GetDirectoryName(target)!.Equals(parent, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("현재 주기의 작업 영역 안에 있는 새 폴더를 선택해주세요.");
            if (File.Exists(Path.Combine(target, MetaName))) throw new ArgumentException("이미 등록된 업무폴더입니다.");
        }
        string journalPath = Path.Combine(Control, "생성의도-" + requestKey + ".json");
        string inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { name, original, understood, requestDate, effort, priority, due, type, occurrence, existingFolder }, Disk.Json))));
        var resume = Disk.ReadJson<CreateIntent>(journalPath);
        if (resume is not null)
        {
            if (resume.InputHash != inputHash || !Path.GetDirectoryName(resume.Target)!.Equals(parent, StringComparison.OrdinalIgnoreCase)) throw new IOException("앞선 부분 생성의 입력/위치가 다릅니다. 기존 폴더를 보존하며 재확인이 필요합니다.");
            target = resume.Target;
        }
        var templates = Settings.CommonTemplates.Concat(type?.Templates ?? []).Where(x => x.Selected).Select(x => new TemplateFile { Selected = true, Path = Path.IsPathFullyQualified(x.Path) ? x.Path : Path.Combine(Root, x.Path) }).ToList();
        if (templates.Any(x => !File.Exists(x.Path))) throw new FileNotFoundException("선택한 양식이 없습니다. 설정에서 확인해주세요.");
        var names = templates.Select(x => Path.GetFileName(x.Path)).ToList();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count || names.Any(x => x.Equals(MetaName, StringComparison.OrdinalIgnoreCase))) throw new IOException("선택 양식의 이름이 충돌합니다. 설정에서 구분해주세요.");
        if (resume is null && existingFolder is not null && templates.Any(x => File.Exists(Path.Combine(target, Path.GetFileName(x.Path))))) throw new IOException("기존 파일과 양식 이름이 충돌합니다. 덮어쓰지 않습니다.");
        Directory.CreateDirectory(target);
        var item = new WorkItem { Id = requestKey, Name = name.Trim(), RequestedOn = requestDate, Effort = effort, Priority = priority, DueOn = due, CycleStart = cycle, Folder = target, TypeId = type?.Id, Occurrence = occurrence, Submissions = (type?.SubmissionNames ?? []).Select(x => new Submission { Name = x }).ToList() };
        if (resume?.RegisteredAt is DateTimeOffset registered) item.RegisteredAt = registered;
        try
        {
            string record = WorkText.Build(original, understood);
            string recordPath = Path.Combine(target, WorkText.FileName);
            if (resume is not null) record = resume.Record;
            else
            {
                string? templateRecord = templates.FirstOrDefault(x => Path.GetFileName(x.Path).Equals(WorkText.FileName, StringComparison.OrdinalIgnoreCase))?.Path;
                if (templateRecord is not null) record += "\n============ 선택한 기록 양식\n" + File.ReadAllText(templateRecord);
                else if (File.Exists(recordPath)) record += "\n============ 기존 기록 보존\n" + File.ReadAllText(recordPath);
                Disk.WriteJson(journalPath, new CreateIntent { Target = target, InputHash = inputHash, Record = record, RegisteredAt = item.RegisteredAt });
            }
            foreach (var t in templates)
            {
                string output = Path.Combine(target, Path.GetFileName(t.Path));
                if (File.Exists(output)) { if (resume is null || (Disk.Hash(output) != Disk.Hash(t.Path) && !(output.Equals(recordPath, StringComparison.OrdinalIgnoreCase) && File.ReadAllText(output) == record))) throw new IOException("부분 생성 파일과 양식의 내용이 다릅니다. 덮어쓰지 않습니다: " + output); }
                else File.Copy(t.Path, output, false);
            }
            if (resume is not null && File.Exists(recordPath) && File.ReadAllText(recordPath) != record && !templates.Any(t => Path.GetFileName(t.Path) == WorkText.FileName && Disk.Hash(t.Path) == Disk.Hash(recordPath))) throw new IOException("부분 생성 뒤 기록이 수정됐습니다. 내용을 보존하고 확인해주세요.");
            Disk.Write(recordPath, record);
            Directory.CreateDirectory(Path.Combine(target, "작업캡처")); Directory.CreateDirectory(Path.Combine(target, "결과물"));
            Save(item); File.Delete(journalPath);
            return item;
        }
        catch (Exception ex) { throw new IOException($"생성이 일부 완료되었습니다. 기존 자료는 보존합니다. 폴더를 확인해주세요: {target}\n{ex.Message}", ex); }
    }
    public void Save(WorkItem item) { lock (sync) { SaveCore(item); } }
    private void SaveCore(WorkItem item)
    {
        Validate(item); string path = Path.Combine(item.Folder, MetaName);
        if (File.Exists(path) && Disk.Hash(path) != item.MetadataHash) throw new IOException("업무정보가 외부에서 변경되었습니다. 현재 파일을 덮어쓰지 않습니다. 창을 다시 열어 확인해주세요.");
        Disk.WriteJson(path, item); item.MetadataHash = Disk.Hash(path);
        int index = known.FindIndex(x => x.Id == item.Id);
        if (index >= 0) known[index] = item; else known.Add(item);
        SaveManifest();
    }
    public void Validate(WorkItem item)
    {
        if (item.Missing || !Directory.Exists(item.Folder)) throw new IOException("업무폴더를 찾을 수 없습니다. 위치를 다시 연결해주세요.");
        if (!Disk.IsInside(item.Folder, Root)) throw new IOException("현재 루트 밖의 업무는 수정할 수 없습니다.");
        if (item.Effort <= 0) throw new ArgumentException("예상량은 0보다 커야 합니다.");
        if (string.IsNullOrWhiteSpace(item.Name)) throw new ArgumentException("작업명을 입력해주세요.");
        if (item.Priority is < 0 or > 2) throw new ArgumentException("중요도를 확인해주세요.");
    }
    public void ChangeState(WorkItem item, WorkState state) { lock (sync) { ChangeStateCore(item, state); } }
    private void ChangeStateCore(WorkItem item, WorkState state)
    {
        Validate(item); if (item.State == state) return;
        if (Disk.Hash(Path.Combine(item.Folder, MetaName)) != item.MetadataHash) throw new IOException("업무정보가 외부에서 변경되어 상태를 바꾸지 않았습니다. 창을 다시 열어 확인해주세요.");
        bool moving = item.State == WorkState.Completed || state == WorkState.Completed;
        var updated = Disk.Clone(item); updated.Folder = item.Folder; updated.MetadataHash = item.MetadataHash; updated.State = state;
        if (state == WorkState.Completed) { updated.CompletedAt = DateTimeOffset.Now; updated.CompletionDateUnknown = false; }
        else { updated.CompletedAt = null; updated.CompletionDateUnknown = false; }
        if (moving)
        {
            string parent = Path.Combine(CycleFolder(item.CycleStart), state == WorkState.Completed ? "완료" : "작업"); Directory.CreateDirectory(parent);
            string target = Path.Combine(parent, Path.GetFileName(item.Folder));
            if (Directory.Exists(target)) throw new IOException("이동할 위치에 같은 이름의 폴더가 있습니다. 원본을 보존하며 이름 확인이 필요합니다.");
            string journal = Path.Combine(Control, "이동의도-" + item.Id + ".json");
            Disk.WriteJson(journal, new { item.Id, from = item.Folder, target, desiredState = state });
            Directory.Move(item.Folder, target); updated.Folder = target;
            Save(updated); File.Delete(journal);
        }
        else Save(updated);
        item.Folder = updated.Folder; item.MetadataHash = updated.MetadataHash; item.State = updated.State; item.CompletedAt = updated.CompletedAt; item.CompletionDateUnknown = updated.CompletionDateUnknown;
    }
    public string SaveText(WorkItem item, string text, string expectedHash) { lock (sync) { return SaveTextCore(item, text, expectedHash); } }
    private string SaveTextCore(WorkItem item, string text, string expectedHash)
    {
        Validate(item); string path = Path.Combine(item.Folder, WorkText.FileName);
        if (Disk.Hash(path) != expectedHash)
        {
            string conflict = Path.Combine(item.Folder, $"업무기록_앱수정본_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.txt");
            Disk.Write(conflict, text);
            throw new IOException("외부 프로그램에서 기록이 변경되었습니다. 외부 원본과 앱 수정본을 모두 보존했습니다. 다시 읽기 전에 폴더에서 내용을 확인해주세요.\n" + conflict);
        }
        Disk.Write(path, text); return Disk.Hash(path);
    }
    public string CopyInto(WorkItem item, string source, string subfolder) { lock (sync) { return CopyIntoCore(item, source, subfolder); } }
    private string CopyIntoCore(WorkItem item, string source, string subfolder)
    {
        Validate(item); if (!File.Exists(source)) throw new FileNotFoundException("파일을 찾을 수 없습니다.", source);
        string area = Path.Combine(item.Folder, subfolder); Directory.CreateDirectory(area);
        string target = Path.Combine(area, Path.GetFileName(source));
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) return target;
        if (File.Exists(target)) throw new IOException("같은 이름의 파일이 있습니다. 다른 이름으로 저장한 뒤 다시 선택해주세요. 덮어쓰지 않습니다.");
        File.Copy(source, target, false); return target;
    }
    public void ConnectFile(WorkItem item, Submission slot, string path) { lock (sync) { ConnectFileCore(item, slot, path); } }
    private void ConnectFileCore(WorkItem item, Submission slot, string path)
    {
        Validate(item); if (!File.Exists(path) || !Disk.IsInside(path, item.Folder)) throw new ArgumentException("현재 작업폴더 안의 파일을 선택해주세요. 지난 파일은 복사 버튼으로 가져올 수 있습니다.");
        string relative = Path.GetRelativePath(item.Folder, path);
        if (!slot.Files.Contains(relative, StringComparer.OrdinalIgnoreCase)) slot.Files.Add(relative);
        Save(item);
    }
    public string Resolve(WorkItem item, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(item.Folder, relative));
        if (!Disk.IsInside(path, item.Folder)) throw new IOException("업무폴더 밖을 가리키는 연결입니다.");
        return path;
    }
    public void Rename(WorkItem item, string folderName) { lock (sync) { RenameCore(item, folderName); } }
    private void RenameCore(WorkItem item, string folderName)
    {
        if (Disk.Hash(Path.Combine(item.Folder, MetaName)) != item.MetadataHash) throw new IOException("업무정보가 외부에서 바뀌어 폴더를 개명하지 않았습니다. 먼저 창을 다시 열어주세요.");
        Validate(item); string target = Path.Combine(Path.GetDirectoryName(item.Folder)!, Disk.SafeName(folderName, 140));
        if (target.Equals(item.Folder, StringComparison.OrdinalIgnoreCase)) return;
        if (Directory.Exists(target)) throw new IOException("동명 폴더가 있습니다. 합치거나 덮어쓰지 않습니다.");
        string journal = Path.Combine(Control, "개명의도-" + item.Id + ".json"); Disk.WriteJson(journal, new { item.Id, from = item.Folder, target });
        Directory.Move(item.Folder, target); item.Folder = target; Save(item); File.Delete(journal);
    }
    public void Reconnect(WorkItem item, string folder) { lock (sync) { ReconnectCore(item, folder); } }
    private void ReconnectCore(WorkItem item, string folder)
    {
        folder = Path.GetFullPath(folder);
        if (!Directory.Exists(folder) || !Disk.IsInside(folder, CycleFolder(item.CycleStart))) throw new IOException("원래 주기의 작업/완료 영역에 있는 폴더를 선택해주세요.");
        string? area = Path.GetFileName(Path.GetDirectoryName(folder));
        if (area is not ("작업" or "완료")) throw new IOException("작업/완료 바로 아래의 요청폴더를 선택해주세요.");
        var current = Disk.ReadJson<WorkItem>(Path.Combine(folder, MetaName));
        if (current is not null && current.Id != item.Id) throw new IOException("다른 업무의 고유 ID입니다. 덮어쓰거나 합치지 않습니다.");
        if (current is not null && current.Name != item.Name) throw new IOException("선택한 폴더의 업무정보가 바뀌었습니다. 현재 내용을 확인한 뒤 다시 연결해주세요.");
        item.Folder = folder; item.Missing = false; item.MetadataHash = Disk.Hash(Path.Combine(folder, MetaName)); Save(item);
    }
    public void RestoreFile(string restoredFile, string target)
    {
        lock (sync)
        {
            target = Path.GetFullPath(target);
            if (!File.Exists(restoredFile) || !Disk.IsInside(target, Root) || Disk.IsInside(target, Control) || Path.GetFileName(target) == MetaName) throw new IOException("복원할 일반 파일과 현재 업무폴더 안의 저장 위치를 선택해주세요. 관리정보는 전체 복원본의 루트 전환으로 복구할 수 있습니다.");
            if (Path.GetFullPath(restoredFile).Equals(target, StringComparison.OrdinalIgnoreCase)) throw new IOException("복원본과 현재 파일은 서로 다른 위치여야 합니다.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target))
            {
                string before = Path.Combine(Control, "복원전보존", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6], Path.GetRelativePath(Root, target)); Directory.CreateDirectory(Path.GetDirectoryName(before)!); File.Copy(target, before, false);
            }
            string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.Copy(restoredFile, temp, false); if (File.Exists(target)) File.Replace(temp, target, target + ".previous", true); else File.Move(temp, target); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    private void SaveManifest() => Disk.WriteJson(Path.Combine(Control, "업무목록.json"), known.OrderBy(x => x.Id).Select(x => new StoredWork { Item = x, Folder = x.Folder }).ToList());
    private sealed class StoredWork { public WorkItem Item { get; set; } = new(); public string Folder { get; set; } = ""; }
    private sealed class CreateIntent { public string Target { get; set; } = ""; public string InputHash { get; set; } = ""; public string Record { get; set; } = ""; public DateTimeOffset? RegisteredAt { get; set; } }
}
