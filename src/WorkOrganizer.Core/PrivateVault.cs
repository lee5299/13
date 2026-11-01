using System.Security.Cryptography;
using System.Text;

namespace WorkOrganizer.Core;

/// <summary>
/// private 상세의 보관소. 디스크에는 AES-256-GCM 암호문만 둔다.
/// 실제 암호화 키(자료 키)는 무작위 32바이트이며, 사용자가 입력하는 업무정리 암호에서 PBKDF2로 만든 키로 감싸
/// `.업무정리\private보관소.json`에 둔다. 암호를 바꾸면 자료 키의 포장만 바꾸므로 기존 private 파일은 그대로 새 암호로 열린다.
/// 사용자 암호와 자료 키는 평문으로 디스크/백업/AI 전송문에 저장하지 않는다(BACKUP_SECRET_SEPARATE).
/// 같은 Windows 사용자로 실행되는 AI 프로세스가 파일을 읽어도 암호문만 얻는다(AI_PRIVATE_DENY의 차단 수단).
/// 앱 메모리 자체에 대한 같은 사용자 프로세스의 접근은 이 수단의 범위 밖이며, 그래서 셸이 있는 AI 작업 중에는 Lock()으로 키를 지운다.
/// </summary>
public sealed class PrivateVault : IDisposable
{
    public const string FileName = "private보관소.json";
    public const int DefaultIterations = 600_000;
    public const int MinimumPassphraseLength = 10;
    private static readonly byte[] Magic = "WOP1"u8.ToArray();
    private const string CheckText = "WorkOrganizer private vault check v1";
    private byte[]? key;
    public string Control { get; }
    public bool IsLocked => key is null;
    private PrivateVault(string control, byte[] key) { Control = control; this.key = key; }

    public static string PathFor(string control) => Path.Combine(control, FileName);
    public static bool Exists(string control) => File.Exists(PathFor(control));
    public static void CheckNewPassphrase(string passphrase)
    {
        if (string.IsNullOrEmpty(passphrase) || passphrase.Length < MinimumPassphraseLength) throw new ArgumentException($"업무정리 암호는 {MinimumPassphraseLength}자 이상으로 정해주세요.");
    }

    public static PrivateVault Create(string control, string passphrase, int iterations = DefaultIterations)
    {
        if (Exists(control)) throw new IOException("이 루트에는 이미 private 보관소가 있습니다. 기존 암호로 잠금을 해제해주세요.");
        CheckNewPassphrase(passphrase);
        if (iterations < 1000) throw new ArgumentException("반복 횟수가 너무 작습니다.");
        var vault = new PrivateVault(control, RandomNumberGenerator.GetBytes(32));
        vault.WriteInfo(passphrase, iterations);
        return vault;
    }

    public static PrivateVault Unlock(string control, string passphrase)
    {
        var info = Disk.ReadJson<VaultInfo>(PathFor(control)) ?? throw new IOException("private 보관소 정보를 찾을 수 없습니다.");
        if (info.Iterations < 1000 || info.Version is not (1 or 2)) throw new IOException("private 보관소 형식을 확인할 수 없습니다.");
        byte[] wrapKey = Derive(passphrase ?? "", Convert.FromBase64String(info.Salt), info.Iterations);
        byte[] dataKey;
        try
        {
            if (info.Version == 1)
            {
                // 1판: 암호에서 만든 키로 바로 암호화했다. 그 키를 자료 키로 삼아 2판으로 올린다(파일은 다시 암호화하지 않음).
                var probe = new PrivateVault(control, wrapKey);
                if (Encoding.UTF8.GetString(probe.DecryptBytes(Convert.FromBase64String(info.Check), "check")) != CheckText) throw new CryptographicException();
                dataKey = (byte[])wrapKey.Clone(); probe.Lock();
                var upgraded = new PrivateVault(control, dataKey); upgraded.WriteInfo(passphrase!, info.Iterations);
                return upgraded;
            }
            var wrapper = new PrivateVault(control, wrapKey);
            dataKey = wrapper.DecryptBytes(Convert.FromBase64String(info.WrappedKey), "data-key");
            wrapper.Lock();
            if (dataKey.Length != 32) throw new CryptographicException();
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(wrapKey);
            throw new UnauthorizedAccessException("업무정리 암호가 맞지 않습니다. private 내용은 바뀌지 않았습니다.");
        }
        return new PrivateVault(control, dataKey);
    }

    /// <summary>암호 변경. 기존 암호로 확인한 뒤 자료 키를 새 암호로 다시 감싼다. private 파일은 다시 쓰지 않는다.</summary>
    public static void ChangePassphrase(string control, string oldPassphrase, string newPassphrase)
    {
        CheckNewPassphrase(newPassphrase);
        if (oldPassphrase == newPassphrase) throw new ArgumentException("새 암호가 기존 암호와 같습니다.");
        using var vault = Unlock(control, oldPassphrase);
        var info = Disk.ReadJson<VaultInfo>(PathFor(control))!;
        vault.WriteInfo(newPassphrase, info.Iterations);
    }

    private void WriteInfo(string passphrase, int iterations)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] wrapKey = Derive(passphrase, salt, iterations);
        var wrapper = new PrivateVault(Control, wrapKey);
        var info = new VaultInfo
        {
            Version = 2, Salt = Convert.ToBase64String(salt), Iterations = iterations,
            WrappedKey = Convert.ToBase64String(wrapper.EncryptBytes((byte[])Key.Clone(), "data-key")),
            Check = Convert.ToBase64String(EncryptBytes(Encoding.UTF8.GetBytes(CheckText), "check")),
        };
        wrapper.Lock();
        Disk.WriteJson(PathFor(Control), info);
        // 이전 포장(.previous)에는 옛 암호로 감싼 키가 남으므로 지운다. 옛 암호로 다시 열 수 없게 한다.
        try { File.Delete(PathFor(Control) + ".previous"); } catch (IOException) { }
    }

    private static byte[] Derive(string passphrase, byte[] salt, int iterations) => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, iterations, HashAlgorithmName.SHA256, 32);

    public void Lock()
    {
        if (key is not null) CryptographicOperations.ZeroMemory(key);
        key = null;
    }
    public void Dispose() => Lock();

    private byte[] Key => key ?? throw new PrivateLockedException();

    public byte[] Encrypt(string text, string itemId) => EncryptBytes(Encoding.UTF8.GetBytes(text), "item:" + itemId);
    public string Decrypt(byte[] data, string itemId)
    {
        try { return Encoding.UTF8.GetString(DecryptBytes(data, "item:" + itemId)); }
        catch (CryptographicException) { throw new IOException("private 문서를 해독할 수 없습니다. 다른 업무의 문서이거나 손상되었습니다. 파일은 그대로 둡니다."); }
    }

    private byte[] EncryptBytes(byte[] plain, string context)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12), tag = new byte[16], cipher = new byte[plain.Length];
        using (var aes = new AesGcm(Key, 16)) aes.Encrypt(nonce, plain, cipher, tag, Aad(context));
        CryptographicOperations.ZeroMemory(plain);
        return [.. Magic, .. nonce, .. tag, .. cipher];
    }
    private byte[] DecryptBytes(byte[] data, string context)
    {
        if (!IsEncrypted(data)) throw new CryptographicException("형식 불일치");
        byte[] nonce = data[4..16], tag = data[16..32], cipher = data[32..], plain = new byte[cipher.Length];
        using var aes = new AesGcm(Key, 16); aes.Decrypt(nonce, cipher, tag, plain, Aad(context));
        return plain;
    }
    private static byte[] Aad(string context) => Encoding.UTF8.GetBytes("WorkOrganizer/private/" + context);
    public static bool IsEncrypted(byte[] data) => data.Length >= 32 && data.AsSpan(0, 4).SequenceEqual(Magic);
    public static bool IsEncryptedFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < 32) return false;
            Span<byte> head = stackalloc byte[4]; stream.ReadExactly(head);
            return head.SequenceEqual(Magic);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    private sealed class VaultInfo { public int Version { get; set; } public string Salt { get; set; } = ""; public int Iterations { get; set; } public string Check { get; set; } = ""; public string WrappedKey { get; set; } = ""; }
}

public sealed class PrivateLockedException : InvalidOperationException
{
    public PrivateLockedException() : base("private이 잠겨 있습니다. 업무정리 암호로 잠금을 해제해주세요.") { }
}

/// <summary>
/// 백업 복구정보 파일. 로컬 백업 저장소(restic)의 내부 암호(무작위)를 사용자의 업무정리 암호로 감싸 저장한다.
/// 다른 PC에서는 '복구정보 파일 + 업무정리 암호'로 복원한다. 내부 암호는 자동 백업을 위해 이 PC에만 DPAPI로 따로 보관한다.
/// </summary>
public static class RecoveryInfo
{
    private const string Header = "업무정리 백업 복구정보 v2";
    public static void Write(string path, string backupPassword, string userPassword, string repository, int iterations = PrivateVault.DefaultIterations)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16), nonce = RandomNumberGenerator.GetBytes(12), tag = new byte[16];
        byte[] plain = Encoding.UTF8.GetBytes(backupPassword), cipher = new byte[plain.Length];
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(userPassword), salt, iterations, HashAlgorithmName.SHA256, 32);
        try { using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(Header)); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
        Disk.Write(path, string.Join("\n", Header,
            "이 파일만으로는 복원할 수 없습니다. 업무정리 암호가 함께 필요합니다. 업무자료·백업 저장소와 다른 곳에 보관하세요.",
            "저장소: " + repository,
            "반복: " + iterations,
            "소금: " + Convert.ToBase64String(salt),
            "값: " + Convert.ToBase64String([.. nonce, .. tag, .. cipher])) + "\n");
    }
    public static bool IsProtected(string path) => File.ReadLines(path).FirstOrDefault() == Header;
    /// <summary>복구정보에서 백업 내부 암호를 꺼낸다. 이전 판(암호 평문 "암호: ")도 읽는다.</summary>
    public static string Read(string path, string? userPassword)
    {
        var lines = File.ReadAllLines(path);
        if (lines.FirstOrDefault() != Header)
        {
            string line = lines.FirstOrDefault(x => x.StartsWith("암호: ", StringComparison.Ordinal)) ?? throw new IOException("업무정리 복구정보의 암호 항목을 찾지 못했습니다.");
            string value = line[4..].Trim(); if (value.Length < 16) throw new IOException("복구정보를 확인해주세요."); return value;
        }
        string Field(string name) => (lines.FirstOrDefault(x => x.StartsWith(name + ": ", StringComparison.Ordinal)) ?? throw new IOException("복구정보가 손상되었습니다: " + name))[(name.Length + 2)..].Trim();
        int iterations = int.Parse(Field("반복")); byte[] salt = Convert.FromBase64String(Field("소금")), blob = Convert.FromBase64String(Field("값"));
        if (iterations < 1000 || blob.Length < 29) throw new IOException("복구정보가 손상되었습니다.");
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(userPassword ?? ""), salt, iterations, HashAlgorithmName.SHA256, 32);
        byte[] plain = new byte[blob.Length - 28];
        try { using var aes = new AesGcm(key, 16); aes.Decrypt(blob[..12], blob[28..], blob[12..28], plain, Encoding.UTF8.GetBytes(Header)); }
        catch (CryptographicException) { throw new UnauthorizedAccessException("업무정리 암호가 맞지 않거나 복구정보가 손상되었습니다."); }
        finally { CryptographicOperations.ZeroMemory(key); }
        return Encoding.UTF8.GetString(plain);
    }
}
