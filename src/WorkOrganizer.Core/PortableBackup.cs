using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace WorkOrganizer.Core;

public sealed record PortableEntry(string Path, long Size, string Hash);
public sealed record PortableResult(string File, int FileCount, long Bytes, int SkippedLegacy, int SkippedPrivate);

/// <summary>
/// 사용자가 '백업파일 만들기'를 명시 실행할 때만 만드는 private 제외 독립 백업파일(BACKUP_CLOUD_OUTPUT).
/// 예약·앱 시작·로컬 자동백업에서는 호출하지 않는다(BACKUP_CLOUD_NO_AUTO). 업로드·동기화 기능은 없다(BACKUP_CLOUD_NO_UPLOAD).
/// 형식: 'WOBK' 머리말 + PBKDF2 소금 + 1MiB 단위 AES-256-GCM 조각(순번·마지막 표시를 인증)으로 감싼 ZIP. 기존 restic 저장소 없이 복원된다(BACKUP_CLOUD_PORTABLE).
/// private 원본·이전본(.previous)·임시본·검색색인·백업키·잠금·미분류 단일기록 평문은 넣지 않는다(BACKUP_CLOUD_CLEAN).
/// </summary>
public static class PortableBackup
{
    public const string Extension = ".wobak";
    public const string ManifestName = "업무정리_백업정보.json";
    private static readonly byte[] Magic = "WOBK"u8.ToArray();
    private const byte Version = 1;
    private const int ChunkSize = 1 << 20;
    public const int DefaultIterations = 600_000;

    /// <summary>루트 기준 상대경로가 백업파일에서 제외되는지. 이유를 돌려준다(null이면 포함).</summary>
    public static string? ExclusionReason(string relative)
    {
        string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = parts[^1];
        if (parts.Any(p => p.Equals(WorkText.PrivateFolder, StringComparison.OrdinalIgnoreCase)) || name.Contains(".private", StringComparison.OrdinalIgnoreCase)) return "private";
        if (name.EndsWith(".previous", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)) return "temporary";
        if (parts[0].Equals(".업무정리", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 2 && (name.Equals("설정.json", StringComparison.OrdinalIgnoreCase) || name.Equals("업무목록.json", StringComparison.OrdinalIgnoreCase))) return null;
            if (parts.Length >= 3 && (parts[1].Equals("기본양식", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("지난제출물", StringComparison.OrdinalIgnoreCase))) return null;
            return "control";
        }
        // 업무폴더 안의 이전 단일기록 평문(미분류)은 민감 상세가 섞였을 수 있어 넣지 않는다.
        if (name.StartsWith("업무기록", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) return "legacy";
        return null;
    }

    public static async Task<PortableResult> Create(WorkStore store, string password, string destination, bool overwrite = false, int iterations = DefaultIterations, CancellationToken cancel = default)
    {
        destination = Path.GetFullPath(destination);
        if (string.IsNullOrEmpty(password) || password.Length < PrivateVault.MinimumPassphraseLength) throw new ArgumentException("업무정리 암호를 확인해주세요.");
        if (Disk.IsInside(destination, store.Root)) throw new IOException("업무 루트 밖의 위치를 지정해주세요. 루트 안에 만들면 다음 백업에 섞입니다.");
        if (!string.IsNullOrWhiteSpace(store.Settings.BackupLocation) && Disk.IsInside(destination, Path.Combine(store.Settings.BackupLocation, "업무정리_백업저장소"))) throw new IOException("로컬 백업 저장소 안이 아닌 별도 위치를 지정해주세요.");
        string folder = Path.GetDirectoryName(destination) ?? throw new IOException("저장 위치를 확인해주세요.");
        if (!Directory.Exists(folder)) throw new IOException("저장 위치(장치/폴더)가 연결되지 않았습니다. 파일을 만들지 않았습니다.");
        if (File.Exists(destination) && !overwrite) throw new IOException("같은 이름의 파일이 있습니다. 다른 이름을 지정해주세요.");
        string zipTemp = Path.Combine(Path.GetTempPath(), "WorkOrganizerPortable", Guid.NewGuid().ToString("N") + ".zip");
        string partial = destination + "." + Guid.NewGuid().ToString("N")[..8] + ".partial";
        Directory.CreateDirectory(Path.GetDirectoryName(zipTemp)!);
        try
        {
            int skippedLegacy = 0, skippedPrivate = 0;
            var entries = new List<PortableEntry>();
            await Task.Run(() =>
            {
                var files = Directory.EnumerateFiles(store.Root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false })
                    .Select(f => Path.GetRelativePath(store.Root, f)).Order(StringComparer.OrdinalIgnoreCase).ToList();
                using (var zip = ZipFile.Open(zipTemp, ZipArchiveMode.Create))
                {
                    foreach (string rel in files)
                    {
                        cancel.ThrowIfCancellationRequested();
                        string? reason = ExclusionReason(rel);
                        if (reason == "legacy") { skippedLegacy++; continue; }
                        if (reason == "private") { skippedPrivate++; continue; }
                        if (reason is not null) continue;
                        string source = Path.Combine(store.Root, rel);
                        var entry = zip.CreateEntry(rel.Replace('\\', '/'), CompressionLevel.Optimal);
                        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        using (var output = entry.Open())
                        {
                            byte[] buffer = new byte[81920]; int read;
                            while ((read = input.Read(buffer)) > 0) { hasher.AppendData(buffer, 0, read); output.Write(buffer, 0, read); }
                        }
                        entries.Add(new PortableEntry(rel.Replace('\\', '/'), input.Length, Convert.ToHexString(hasher.GetHashAndReset())));
                    }
                    // 생성 중 변경 확인: 구성과 내용이 그대로여야 한다.
                    var now = Directory.EnumerateFiles(store.Root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
                        .Select(f => Path.GetRelativePath(store.Root, f)).Where(r => ExclusionReason(r) is null).Select(r => r.Replace('\\', '/')).Order(StringComparer.OrdinalIgnoreCase).ToList();
                    if (!now.SequenceEqual(entries.Select(e => e.Path), StringComparer.OrdinalIgnoreCase)) throw new IOException("백업파일을 만드는 중 파일 구성이 바뀌었습니다. 저장을 마친 뒤 다시 만들어주세요. 불완전한 파일은 남기지 않았습니다.");
                    foreach (var e in entries) if (Disk.Hash(Path.Combine(store.Root, e.Path.Replace('/', Path.DirectorySeparatorChar))) != e.Hash) throw new IOException("백업파일을 만드는 중 파일이 바뀌었습니다: " + e.Path);
                    var manifest = zip.CreateEntry(ManifestName);
                    using var writer = new StreamWriter(manifest.Open(), new UTF8Encoding(false));
                    writer.Write(System.Text.Json.JsonSerializer.Serialize(new { createdAt = DateTimeOffset.Now, scope = "private 제외", note = "private 원본·이전본·검색색인·백업키·미분류 단일기록은 포함하지 않음", files = entries }, Disk.Json));
                }
                using (var plain = new FileStream(zipTemp, FileMode.Open, FileAccess.Read))
                using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { Encrypt(plain, output, password, iterations, cancel); output.Flush(true); }
                // 만든 파일을 다시 열어 전체 조각과 목록을 확인한 뒤에만 완성 이름으로 바꾼다.
                var check = ListEntries(partial, password);
                if (check.Count != entries.Count) throw new IOException("백업파일 확인에 실패했습니다. 불완전한 파일은 남기지 않았습니다.");
                File.Move(partial, destination, overwrite);
            }, cancel);
            return new PortableResult(destination, entries.Count, new FileInfo(destination).Length, skippedLegacy, skippedPrivate);
        }
        finally
        {
            try { if (File.Exists(zipTemp)) File.Delete(zipTemp); } catch { }
            try { if (File.Exists(partial)) File.Delete(partial); } catch { }
        }
    }

    public static List<PortableEntry> ListEntries(string file, string password)
    {
        string temp = Path.Combine(Path.GetTempPath(), "WorkOrganizerPortable", Guid.NewGuid().ToString("N") + ".zip");
        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
        try
        {
            using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write)) Decrypt(input, output, password, default);
            using var zip = ZipFile.OpenRead(temp);
            var manifest = zip.GetEntry(ManifestName) ?? throw new IOException("백업정보가 없는 파일입니다.");
            using var reader = new StreamReader(manifest.Open(), Encoding.UTF8);
            using var json = System.Text.Json.JsonDocument.Parse(reader.ReadToEnd());
            return json.RootElement.GetProperty("files").EnumerateArray().Select(e => new PortableEntry(e.GetProperty("Path").GetString()!, e.GetProperty("Size").GetInt64(), e.GetProperty("Hash").GetString()!)).ToList();
        }
        finally { try { File.Delete(temp); } catch { } }
    }

    /// <summary>빈 별도 폴더로만 복원한다. 현재 업무 루트에 적용하지 않으므로 현재 private를 지우지 않는다(BACKUP_RESTORE_EXCLUDED).</summary>
    public static List<PortableEntry> Restore(string file, string password, string emptyTarget, CancellationToken cancel = default)
    {
        emptyTarget = Path.GetFullPath(emptyTarget);
        if (Directory.Exists(emptyTarget) && Directory.EnumerateFileSystemEntries(emptyTarget).Any()) throw new IOException("복원 위치는 빈 폴더여야 합니다. 기존 파일을 덮어쓰지 않습니다.");
        Directory.CreateDirectory(emptyTarget);
        string temp = Path.Combine(Path.GetTempPath(), "WorkOrganizerPortable", Guid.NewGuid().ToString("N") + ".zip");
        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
        try
        {
            using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write)) Decrypt(input, output, password, cancel);
            var entries = new List<PortableEntry>();
            using var zip = ZipFile.OpenRead(temp);
            foreach (var entry in zip.Entries)
            {
                cancel.ThrowIfCancellationRequested();
                if (entry.FullName.EndsWith('/')) continue;
                string target = Path.GetFullPath(Path.Combine(emptyTarget, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!Disk.IsInside(target, emptyTarget) || target.Equals(emptyTarget, StringComparison.OrdinalIgnoreCase)) throw new IOException("복원 위치 밖을 가리키는 항목이 있어 중단했습니다: " + entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, false);
                if (entry.FullName != ManifestName) entries.Add(new PortableEntry(entry.FullName, entry.Length, Disk.Hash(target)));
            }
            var expected = ListEntries(file, password).ToDictionary(e => e.Path, e => e.Hash, StringComparer.OrdinalIgnoreCase);
            if (expected.Count != entries.Count || entries.Any(e => !expected.TryGetValue(e.Path, out var h) || h != e.Hash)) throw new IOException("복원한 파일이 백업정보와 일치하지 않습니다. 복원 폴더를 사용하지 마세요.");
            return entries;
        }
        finally { try { File.Delete(temp); } catch { } }
    }

    private static byte[] Header(byte[] salt, int iterations)
    {
        var h = new byte[4 + 1 + 16 + 4 + 8];
        Magic.CopyTo(h, 0); h[4] = Version; salt.CopyTo(h, 5); BinaryPrimitives.WriteInt32BigEndian(h.AsSpan(21), iterations);
        RandomNumberGenerator.Fill(h.AsSpan(25, 8)); // 조각 nonce 앞부분
        return h;
    }
    private static byte[] Nonce(byte[] header, uint index) { var n = new byte[12]; header.AsSpan(25, 8).CopyTo(n); BinaryPrimitives.WriteUInt32BigEndian(n.AsSpan(8), index); return n; }
    private static byte[] Aad(byte[] header, uint index, bool final) { var a = new byte[header.Length + 5]; header.CopyTo(a, 0); BinaryPrimitives.WriteUInt32BigEndian(a.AsSpan(header.Length), index); a[^1] = final ? (byte)1 : (byte)0; return a; }
    private static byte[] Key(string password, byte[] salt, int iterations) => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);

    public static void Encrypt(Stream plain, Stream output, string password, int iterations, CancellationToken cancel)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16), header = Header(salt, iterations), key = Key(password, salt, iterations);
        try
        {
            output.Write(header);
            using var aes = new AesGcm(key, 16);
            byte[] current = new byte[ChunkSize], next = new byte[ChunkSize];
            int currentLength = ReadFull(plain, current); uint index = 0;
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                int nextLength = currentLength == ChunkSize ? ReadFull(plain, next) : 0;
                bool final = nextLength == 0;
                byte[] cipher = new byte[currentLength], tag = new byte[16], len = new byte[5];
                aes.Encrypt(Nonce(header, index), current.AsSpan(0, currentLength), cipher, tag, Aad(header, index, final));
                len[0] = final ? (byte)1 : (byte)0; BinaryPrimitives.WriteInt32BigEndian(len.AsSpan(1), currentLength);
                output.Write(len); output.Write(tag); output.Write(cipher);
                if (final) break;
                (current, next) = (next, current); currentLength = nextLength; index++;
            }
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public static void Decrypt(Stream input, Stream output, string password, CancellationToken cancel)
    {
        var header = new byte[33];
        if (ReadFull(input, header) != header.Length || !header.AsSpan(0, 4).SequenceEqual(Magic) || header[4] != Version) throw new IOException("업무정리 백업파일 형식이 아닙니다.");
        int iterations = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(21));
        if (iterations < 1000 || iterations > 10_000_000) throw new IOException("백업파일 머리말이 손상되었습니다.");
        byte[] key = Key(password ?? "", header[5..21], iterations);
        try
        {
            using var aes = new AesGcm(key, 16);
            for (uint index = 0; ; index++)
            {
                cancel.ThrowIfCancellationRequested();
                var len = new byte[5]; var tag = new byte[16];
                if (ReadFull(input, len) != 5 || ReadFull(input, tag) != 16) throw new IOException("백업파일이 중간에 끊겼습니다(마지막 조각 없음). 이 파일로 복원하지 않습니다.");
                bool final = len[0] == 1; int length = BinaryPrimitives.ReadInt32BigEndian(len.AsSpan(1));
                if (length < 0 || length > ChunkSize) throw new IOException("백업파일이 손상되었습니다.");
                byte[] cipher = new byte[length], plain = new byte[length];
                if (ReadFull(input, cipher) != length) throw new IOException("백업파일이 중간에 끊겼습니다. 이 파일로 복원하지 않습니다.");
                try { aes.Decrypt(Nonce(header, index), cipher, tag, plain, Aad(header, index, final)); }
                catch (CryptographicException) { throw new UnauthorizedAccessException("이 백업파일을 만들 때의 업무정리 암호가 아니거나 파일이 손상되었습니다. 아무것도 복원하지 않았습니다. 암호를 바꾼 적이 있다면 만들 당시의 암호를 입력해주세요."); }
                output.Write(plain);
                if (final) { if (input.ReadByte() != -1) throw new IOException("백업파일 끝에 알 수 없는 자료가 있습니다."); break; }
            }
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    private static int ReadFull(Stream s, byte[] buffer) { int total = 0, read; while (total < buffer.Length && (read = s.Read(buffer, total, buffer.Length - total)) > 0) total += read; return total; }
}
