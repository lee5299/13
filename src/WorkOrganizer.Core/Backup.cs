using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace WorkOrganizer.Core;

public sealed record BackupResult(bool Success, bool Partial, string Message, string? SnapshotId, TimeSpan Elapsed);
public sealed record BackupPoint(string Id, string Time, string Description);
public sealed class BackupEngine
{
    private readonly WorkStore store;
    private readonly SemaphoreSlim gate = new(1, 1);
    public bool Running { get; private set; }
    public BackupEngine(WorkStore store) => this.store = store;
    public string Repository => Path.Combine(store.Settings.BackupLocation, "업무정리_백업저장소");
    public static string NewPassword() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(store.Settings.BackupLocation)) throw new IOException("백업 위치를 설정해주세요.");
        if (Disk.IsInside(store.Settings.BackupLocation, store.Root) || Disk.IsInside(store.Root, store.Settings.BackupLocation)) throw new IOException("별도 백업 위치를 지정해주세요.");
        if (!Directory.Exists(store.Settings.BackupLocation)) throw new IOException("백업 장치/폴더가 연결되지 않았습니다. 기존 복구본은 보존합니다.");
        if (!File.Exists(store.Settings.ResticPath)) throw new FileNotFoundException("백업 엔진 위치를 설정해주세요.");
    }
    private async Task<(int Exit, string Output, string Error)> Run(string password, IEnumerable<string> args, CancellationToken cancel)
    {
        var start = new ProcessStartInfo(store.Settings.ResticPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["RESTIC_PASSWORD"] = password;
        start.ArgumentList.Add("--repo"); start.ArgumentList.Add(Repository);
        foreach (var a in args) start.ArgumentList.Add(a);
        using var p = Process.Start(start) ?? throw new IOException("백업 엔진을 실행할 수 없습니다.");
        using var registration = cancel.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } });
        var output = p.StandardOutput.ReadToEndAsync(cancel); var error = p.StandardError.ReadToEndAsync(cancel);
        await p.WaitForExitAsync(cancel);
        return (p.ExitCode, await output, await error);
    }
    private IEnumerable<string> Sources()
    {
        foreach (var f in Directory.EnumerateFiles(store.Root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }))
        {
            string rel = Path.GetRelativePath(store.Root, f);
            if (rel.StartsWith(".업무정리" + Path.DirectorySeparatorChar + "복원전보존", StringComparison.OrdinalIgnoreCase) || rel.Contains("백업입력상태.json", StringComparison.OrdinalIgnoreCase) || rel.EndsWith("설정.json.previous", StringComparison.OrdinalIgnoreCase) || rel.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || rel.Contains("검색.sqlite", StringComparison.OrdinalIgnoreCase)) continue;
            if (!store.Settings.BackupCaptures && rel.Split(Path.DirectorySeparatorChar).Contains("작업캡처")) continue;
            if (!store.Settings.BackupResults && rel.Split(Path.DirectorySeparatorChar).Contains("결과물")) continue;
            if (!store.Settings.BackupRecords && (rel.EndsWith(WorkText.FileName) || rel.EndsWith(WorkText.FileName + ".previous"))) continue;
            yield return f;
        }
    }
    public async Task<BackupResult> Backup(string password, CancellationToken cancel = default)
    {
        if (!await gate.WaitAsync(0, cancel)) return new(false, false, "이미 백업/복원 작업 중입니다.", null, TimeSpan.Zero);
        Running = true; var timer = Stopwatch.StartNew(); string? stage = null;
        try
        {
            Validate();
            if (!File.Exists(Path.Combine(Repository, "config")))
            {
                if (Directory.Exists(Repository) && Directory.EnumerateFileSystemEntries(Repository).Any()) throw new IOException("저장소가 비어 있지 않거나 손상됐습니다. 새로 덮어쓰지 않습니다.");
                var init = await Run(password, ["init"], cancel);
                if (init.Exit != 0) throw new IOException("저장소 준비 실패: " + init.Error);
            }
            string stagingParent = Path.Combine(Path.GetTempPath(), "WorkOrganizerBackup", Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(store.Root)))[..16]);
            Directory.CreateDirectory(stagingParent); stage = Path.Combine(stagingParent, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await Task.Run(() =>
            {
                foreach (string source in Sources())
                {
                    cancel.ThrowIfCancellationRequested(); string rel = Path.GetRelativePath(store.Root, source); string target = Path.Combine(stage, rel); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    input.CopyTo(output); output.Flush(true); output.Close();
                    hashes[rel] = Disk.Hash(target);
                }
                var now = Sources().Select(x => Path.GetRelativePath(store.Root, x)).Order(StringComparer.OrdinalIgnoreCase).ToList();
                if (!now.SequenceEqual(hashes.Keys.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)) throw new IOException("백업 준비 중 파일 구성이 변경되었습니다. 저장 후 다시 시도해주세요.");
                foreach (var (rel, hash) in hashes) if (Disk.Hash(Path.Combine(store.Root, rel)) != hash) throw new IOException("백업 준비 중 파일이 변경되었습니다. 저장을 마친 뒤 다시 시도해주세요.");
            }, cancel);
            var signatureParts = new SortedDictionary<string, string>(hashes, StringComparer.OrdinalIgnoreCase);
            string settingsRelative = Path.Combine(".업무정리", "설정.json");
            if (signatureParts.ContainsKey(settingsRelative))
            {
                var stable = Disk.ReadJson<AppSettings>(Path.Combine(stage, settingsRelative))!; stable.LastBackupAt = null; stable.LastBackupStatus = "";
                signatureParts[settingsRelative] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(stable, Disk.Json))));
            }
            string signature = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(signatureParts))));
            string inputStatePath = Path.Combine(store.Control, "백업입력상태.json");
            var previousInput = Disk.ReadJson<BackupInputState>(inputStatePath);
            if (previousInput?.Signature == signature && previousInput.Repository == Repository)
            {
                var old = await Run(password, ["snapshots", previousInput.SnapshotId, "--json"], cancel);
                if (old.Exit == 0 && JsonDocument.Parse(old.Output).RootElement.GetArrayLength() > 0) return new(true, false, "변경 없음 · 이전 복구시점을 유지합니다.", previousInput.SnapshotId, timer.Elapsed);
            }
            var result = await Run(password, ["backup", stage, "--tag", "WorkOrganizer", "--json"], cancel);
            string? id = null;
            foreach (string line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            { try { using var j = JsonDocument.Parse(line); if (j.RootElement.TryGetProperty("snapshot_id", out var v)) id = v.GetString(); } catch (JsonException) { } }
            bool partial = result.Exit == 3;
            if (result.Exit != 0 || id is null) return Finish(false, partial, partial ? "일부 파일 백업 실패 · 이전 복구본은 보존했습니다." : $"백업 실패({result.Exit}) · {result.Error}", id, timer.Elapsed);
            Disk.WriteJson(inputStatePath, new BackupInputState { Signature = signature, Repository = Repository, SnapshotId = id });
            var retention = await Run(password, ["forget", "--tag", "WorkOrganizer", "--keep-last", "2", "--keep-daily", store.Settings.KeepDaily.ToString(), "--keep-monthly", store.Settings.KeepMonthly.ToString(), "--group-by", "tags", "--prune"], cancel);
            return Finish(true, false, retention.Exit == 0 ? "백업 및 이력 정리 완료" : "백업 완료 · 이력 정리는 재확인 필요: " + retention.Error, id, timer.Elapsed);
        }
        catch (OperationCanceledException) { return Finish(false, false, "백업을 중단했습니다. 원본과 이전 복구본을 보존합니다.", null, timer.Elapsed); }
        catch (Exception ex) { return Finish(false, false, ex.Message, null, timer.Elapsed); }
        finally
        {
            if (stage is not null && Disk.IsInside(stage, Path.Combine(Path.GetTempPath(), "WorkOrganizerBackup"))) { try { Directory.Delete(stage, true); } catch { } }
            Running = false; gate.Release();
        }
    }
    private sealed class BackupInputState { public string Signature { get; set; } = ""; public string Repository { get; set; } = ""; public string SnapshotId { get; set; } = ""; }
    private BackupResult Finish(bool success, bool partial, string message, string? id, TimeSpan elapsed)
    {
        var s = Disk.Clone(store.Settings); s.LastBackupStatus = message;
        if (success) s.LastBackupAt = DateTimeOffset.Now;
        store.SaveSettings(s); return new(success, partial, message, id, elapsed);
    }
    public async Task<List<BackupPoint>> Snapshots(string password, CancellationToken cancel = default)
    {
        Validate(); var r = await Run(password, ["snapshots", "--tag", "WorkOrganizer", "--json"], cancel);
        if (r.Exit != 0) throw new IOException("백업 목록을 읽을 수 없습니다. 암호/장치/잠금 상태를 확인해주세요. " + r.Error);
        using var json = JsonDocument.Parse(r.Output);
        return json.RootElement.EnumerateArray().Select(x => new BackupPoint(x.GetProperty("id").GetString()!, x.GetProperty("time").GetString()!, "업무정리 복구시점")).OrderByDescending(x => x.Time).ToList();
    }
    public async Task Restore(string password, string snapshot, string emptyTarget, CancellationToken cancel = default)
    {
        if (!await gate.WaitAsync(0, cancel)) throw new IOException("백업/복원 작업이 이미 실행 중입니다.");
        Running = true;
        try
        {
            Validate();
            if (Disk.IsInside(emptyTarget, store.Root) || Disk.IsInside(store.Root, emptyTarget) || Disk.IsInside(emptyTarget, store.Settings.BackupLocation)) throw new IOException("업무 루트/백업저장소와 다른 빈 위치를 지정해주세요.");
            if (Directory.Exists(emptyTarget) && Directory.EnumerateFileSystemEntries(emptyTarget).Any()) throw new IOException("복원 위치는 빈 폴더여야 합니다. 기존 파일을 덮어쓰지 않습니다.");
            Directory.CreateDirectory(emptyTarget);
            var lookup = await Run(password, ["snapshots", snapshot, "--json"], cancel);
            if (lookup.Exit != 0) throw new IOException("복구시점 정보를 확인하지 못했습니다. " + lookup.Error);
            using var metadata = JsonDocument.Parse(lookup.Output);
            string path = metadata.RootElement[0].GetProperty("paths")[0].GetString() ?? throw new IOException("복원할 업무 경로 정보가 없습니다.");
            path = path.Replace('\\', '/');
            if (path.Length >= 2 && path[1] == ':') path = "/" + path[0] + path[2..];
            // Restore only our staged data tree, never the source system's ancestor directories.
            var r = await Run(password, ["restore", snapshot + ":" + path, "--target", emptyTarget, "--verify", "--json"], cancel);
            if (r.Exit != 0) throw new IOException($"복원이 완전하지 않습니다({r.Exit}). 원본은 바뀌지 않았습니다. " + r.Error);
        }
        finally { Running = false; gate.Release(); }
    }
}
