using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WorkOrganizer.Core;

namespace WorkOrganizer.App;

public sealed class BackupController : IDisposable
{
    private readonly WorkStore store;
    private CancellationTokenSource? cancel;
    private DateTimeOffset lastAttempt = DateTimeOffset.MinValue;
    public BackupEngine Engine { get; }
    private string KeyPath => Path.Combine(store.Control, ".백업키");
    private string ExportMarker => Path.Combine(store.Control, ".복구정보저장확인");
    public BackupController(WorkStore store) { this.store = store; Engine = new(store); }
    private string Password(bool create = false)
    {
        if (File.Exists(KeyPath)) return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.CurrentUser));
        if (!create) throw new IOException("설정에서 먼저 복구정보를 저장해주세요.");
        string password = BackupEngine.NewPassword(); File.WriteAllBytes(KeyPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser)); return password;
    }
    /// <summary>복구정보 저장. 백업 내부 암호를 업무정리 암호로 잠가 별도 위치에 저장한다(파일만으로는 복원 불가).</summary>
    public void ExportKey(string userPassword)
    {
        var d = new Microsoft.Win32.SaveFileDialog { Title = "백업 복구정보를 별도로 저장 · 업무정리 암호로 잠김", FileName = "업무정리_백업복구정보.txt", Filter = "복구정보|*.txt" };
        if (d.ShowDialog() != true) return;
        if (Disk.IsInside(d.FileName, store.Root) || (!string.IsNullOrWhiteSpace(store.Settings.BackupLocation) && Disk.IsInside(d.FileName, Engine.Repository))) throw new IOException("업무 루트나 백업저장소 내부가 아닌 별도 위치에 보관해주세요.");
        string password = Password(true);
        RecoveryInfo.Write(d.FileName, password, userPassword, Engine.Repository);
        if (RecoveryInfo.Read(d.FileName, userPassword) != password) throw new IOException("복구정보 확인에 실패했습니다. 다시 저장해주세요.");
        Disk.Write(ExportMarker, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))));
        bool copied = WriteRecoveryCopy(userPassword);
        MessageBox.Show("복구정보를 저장했습니다. 업무정리 암호가 있어야 열립니다." + (copied ? "\n백업 위치(외장디스크)에도 같은 복구정보를 저장했습니다. 다른 PC에서는 그 디스크와 업무정리 암호로 복원할 수 있습니다." : "\n백업 위치가 아직 연결되지 않아 디스크에는 저장하지 못했습니다. 백업 위치를 연결한 뒤 다시 저장해주세요."));
    }
    /// <summary>백업 비밀키를 업무정리 암호로 잠가 백업 저장소 옆에 저장한다. 사용자 암호를 알 때(복구정보 저장·암호 변경)만 호출한다.</summary>
    public bool WriteRecoveryCopy(string userPassword)
    {
        if (!File.Exists(KeyPath) || string.IsNullOrWhiteSpace(store.Settings.BackupLocation) || !Directory.Exists(store.Settings.BackupLocation)) return false;
        string password = Password();
        RecoveryInfo.Write(Engine.RecoveryCopyPath, password, userPassword, Engine.Repository);
        if (RecoveryInfo.Read(Engine.RecoveryCopyPath, userPassword) != password) throw new IOException("백업 위치의 복구정보 확인에 실패했습니다.");
        return true;
    }
    public void ImportKey(Func<string?> askPassword)
    {
        string? path = UI.PickFile("복구정보 선택 · 백업 위치(외장디스크)의 업무정리_백업복구정보.txt 또는 따로 보관한 파일", Directory.Exists(store.Settings.BackupLocation) ? store.Settings.BackupLocation : null, "복구정보|*.txt"); if (path is null) return;
        string? user = RecoveryInfo.IsProtected(path) ? askPassword() : null;
        if (RecoveryInfo.IsProtected(path) && user is null) return;
        string value = RecoveryInfo.Read(path, user);
        if (File.Exists(KeyPath) && !UI.Confirm("이 루트에서 사용할 백업 암호를 가져온 복구정보로 바꿀까요? 기존 저장소를 지우지 않습니다. 저장소 위치도 확인해주세요.")) return;
        if (File.Exists(KeyPath)) File.Copy(KeyPath, KeyPath + ".previous", true);
        File.WriteAllBytes(KeyPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser)); Disk.Write(ExportMarker, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))));
    }
    public async Task<BackupResult> BackupNow()
    {
        if (Engine.Running) throw new IOException("이미 백업/복원이 실행 중입니다.");
        if (!File.Exists(ExportMarker)) throw new IOException("먼저 복구정보를 별도로 저장해주세요.");
        cancel?.Dispose(); cancel = new(); lastAttempt = DateTimeOffset.Now; var result = await Engine.Backup(Password(), cancel.Token);
        if (result.Success && !File.Exists(Engine.RecoveryCopyPath)) result = result with { Message = result.Message + " · 백업 위치에 복구정보 없음: 설정에서 ‘복구정보 저장’을 다시 눌러주세요" };
        return result;
    }
    public async Task TryScheduled()
    {
        if (Engine.Running || string.IsNullOrWhiteSpace(store.Settings.BackupLocation) || !File.Exists(ExportMarker)) return;
        var boundary = store.Settings.LastBackupAt is DateTimeOffset previous && previous > lastAttempt ? previous : lastAttempt;
        if (DateTimeOffset.Now - boundary < TimeSpan.FromMinutes(store.Settings.BackupMinutes)) return;
        try { await BackupNow(); } catch (Exception ex) { lastAttempt = DateTimeOffset.Now; var s = Disk.Clone(store.Settings); s.LastBackupStatus = "백업 확인 필요: " + ex.Message; store.SaveSettings(s); }
    }
    public Task<System.Collections.Generic.List<BackupPoint>> Points() => Engine.Snapshots(Password());
    public Task Restore(string id, string target) { if (Engine.Running) throw new IOException("백업/복원 중입니다."); cancel?.Dispose(); cancel = new(); return Engine.Restore(Password(), id, target, cancel.Token); }
    /// <summary>‘백업파일 만들기’ 명시 실행(BACKUP_CLOUD_OUTPUT). 저장창 취소면 파일을 만들지 않고 null.</summary>
    public async Task<PortableResult?> CreatePortable(Func<string?> askPassword)
    {
        if (Engine.Running) throw new IOException("백업/복원이 끝난 뒤 다시 시도해주세요.");
        var d = new Microsoft.Win32.SaveFileDialog { Title = "private 제외 백업파일 저장 위치", FileName = $"업무정리_백업_{DateTime.Now:yyyyMMdd_HHmm}{PortableBackup.Extension}", Filter = "업무정리 백업파일|*" + PortableBackup.Extension, InitialDirectory = Directory.Exists(store.Settings.LastPortableFolder) ? store.Settings.LastPortableFolder : "" };
        if (d.ShowDialog() != true) return null;
        if (!UI.Confirm("포함: 공개기록·업무정보·설정·양식·캡처·결과물\n제외: private 상세·이전본·임시본·검색색인·백업키·미분류 이전 기록\n업무정리 암호로 잠급니다.\n\n이 범위로 백업파일을 만들까요? 업로드는 직접 해주세요.")) return null;
        string? user = askPassword(); if (user is null) return null;
        cancel?.Dispose(); cancel = new();
        var result = await PortableBackup.Create(store, user, d.FileName, overwrite: File.Exists(d.FileName), cancel: cancel.Token);
        var s = Disk.Clone(store.Settings); s.LastPortableFolder = Path.GetDirectoryName(d.FileName) ?? ""; store.SaveSettings(s);
        return result;
    }
    public async Task<int?> RestorePortable(Func<string?> askPassword)
    {
        string? file = UI.PickFile("복원할 업무정리 백업파일 선택", null, "업무정리 백업파일|*" + PortableBackup.Extension); if (file is null) return null;
        string? target = UI.PickFolder("복원할 빈 폴더 선택 · 현재 업무 루트와 다른 위치"); if (target is null) return null;
        if (Disk.IsInside(target, store.Root) || Disk.IsInside(store.Root, target)) throw new IOException("현재 업무 루트와 겹치지 않는 빈 폴더를 선택해주세요.");
        string? user = askPassword(); if (user is null) return null;
        cancel?.Dispose(); cancel = new(); var token = cancel.Token;
        var entries = await Task.Run(() => PortableBackup.Restore(file, user, target, token));
        UI.Open(target); return entries.Count;
    }
    public void Cancel() => cancel?.Cancel();
    public void Dispose() { cancel?.Cancel(); cancel?.Dispose(); }
}
public sealed class RestoreWindow : Window
{
    public RestoreWindow(MainWindow main)
    {
        Title = "백업 복구시점 / 별도 위치 복원"; Width = 820; Height = 650; Background = UI.Canvas; FontFamily = main.FontFamily;
        var panel = new DockPanel { Margin = new Thickness(24) }; var top = new StackPanel(); top.Children.Add(UI.Text("원본을 바꾸지 않고 먼저 복원합니다", 22, true)); top.Children.Add(UI.Text("복구시점 선택 → 업무 루트와 다른 빈 폴더로 복원 → 파일 확인. 복원한 자료를 확인한 뒤 관리 루트를 전환할 수 있습니다.")); var status = UI.Text(""); top.Children.Add(status); var list = new ListBox { DisplayMemberPath = "Label" };
        top.Children.Add(UI.Button("복구시점 목록 읽기", () => { _ = UI.GuardAsync(async () => { list.ItemsSource = (await main.Backups!.Points()).Select(p => new PointRow(p)).ToList(); }); })); DockPanel.SetDock(top, Dock.Top); panel.Children.Add(top);
        var buttons = new StackPanel(); string? extracted = null;
        buttons.Children.Add(UI.Button("선택 시점을 빈 폴더로 복원", () => { _ = UI.GuardAsync(async () => { if (list.SelectedItem is not PointRow row) throw new IOException("복구시점을 선택해주세요."); var point = row.Point; string? target = UI.PickFolder("별도 빈 폴더 선택"); if (target is null) return; status.Text = "복원 중 · 원본은 바뀌지 않습니다."; await main.Backups!.Restore(point.Id, target); extracted = target; status.Text = "복원 및 파일 검사 완료 · " + target; UI.Open(target); }); }, true));
        buttons.Children.Add(UI.Button("확인한 복원본을 관리 루트로 열기", () =>
        {
            string? root = UI.PickFolder("복원된 자료 중 .업무정리와 주기 폴더가 들어 있는 루트를 선택", extracted); if (root is null) return;
            if (!File.Exists(Path.Combine(root, ".업무정리", "설정.json"))) throw new IOException("선택한 위치에서 업무정리 설정을 찾을 수 없습니다.");
            bool hasPrivate = Directory.EnumerateDirectories(root, WorkOrganizer.Core.WorkText.PrivateFolder, SearchOption.AllDirectories).Any();
            if (!UI.Confirm("현재 원본은 그대로 두고 선택한 복원본을 관리 대상으로 열까요? 다른 PC에서는 복구정보를 다시 가져와야 할 수 있습니다." + (hasPrivate ? "\nprivate 상세는 이 루트의 업무정리 암호로 잠금 해제합니다." : "\n이 복원본에는 private 상세가 없습니다(private 제외 백업). 현재 루트의 private은 지우지 않고 원래 위치에 그대로 남습니다."))) return;
            main.SetRoot(root); Close();
        }));
        buttons.Children.Add(UI.Button("복원본의 파일 하나를 현재 업무에 적용", () =>
        {
            string? source = UI.PickFile("확인한 복원 파일 하나 선택", extracted); if (source is null) return;
            var d = new Microsoft.Win32.SaveFileDialog { Title = "현재 업무폴더 안의 적용 위치 선택", InitialDirectory = main.Store!.Root, FileName = Path.GetFileName(source), Filter = "모든 파일|*.*", OverwritePrompt = false };
            if (d.ShowDialog() != true || !UI.Confirm("선택한 위치에 파일을 복원할까요? 현재 파일이 있으면 교체 전 사본을 별도로 보존합니다.")) return;
            main.Store.RestoreFile(source, d.FileName); status.Text = "선택 파일을 복원했습니다. 교체 전 자료도 보존했습니다."; _ = main.Refresh();
        }));
        DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons); panel.Children.Add(list); Content = panel;
    }
}
public sealed record PointRow(BackupPoint Point) { public string Label => Point.Time + " · " + Point.Description; }
