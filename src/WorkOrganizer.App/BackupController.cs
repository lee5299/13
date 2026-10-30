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
    public void ExportKey()
    {
        var d = new Microsoft.Win32.SaveFileDialog { Title = "백업 복구정보를 별도로 저장", FileName = "업무정리_백업복구정보.txt", Filter = "복구정보|*.txt" };
        if (d.ShowDialog() != true) return;
        if (Disk.IsInside(d.FileName, store.Root) || (!string.IsNullOrWhiteSpace(store.Settings.BackupLocation) && Disk.IsInside(d.FileName, Engine.Repository))) throw new IOException("업무 루트나 백업저장소 내부가 아닌 별도 위치에 보관해주세요.");
        string password = Password(true);
        Disk.Write(d.FileName, "업무정리 백업 복구정보\n외부에 공개하지 마세요. 이 암호를 잃으면 백업을 복원할 수 없습니다.\n저장소: " + Engine.Repository + "\n암호: " + password + "\n");
        Disk.Write(ExportMarker, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)))); MessageBox.Show("복구정보를 저장했습니다. 백업과 함께 잃지 않도록 별도로 보관해주세요.");
    }
    public void ImportKey()
    {
        string? path = UI.PickFile("기존 업무정리 복구정보 선택", null, "복구정보|*.txt"); if (path is null) return;
        string line = File.ReadAllLines(path).FirstOrDefault(x => x.StartsWith("암호: ", StringComparison.Ordinal)) ?? throw new IOException("업무정리 복구정보의 암호 항목을 찾지 못했습니다.");
        string value = line[4..].Trim(); if (value.Length < 16) throw new IOException("복구정보를 확인해주세요.");
        if (File.Exists(KeyPath) && !UI.Confirm("이 루트에서 사용할 백업 암호를 가져온 복구정보로 바꿀까요? 기존 저장소를 지우지 않습니다. 저장소 위치도 확인해주세요.")) return;
        if (File.Exists(KeyPath)) File.Copy(KeyPath, KeyPath + ".previous", true);
        File.WriteAllBytes(KeyPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser)); Disk.Write(ExportMarker, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))));
    }
    public async Task<BackupResult> BackupNow()
    {
        if (Engine.Running) throw new IOException("이미 백업/복원이 실행 중입니다.");
        if (!File.Exists(ExportMarker)) throw new IOException("먼저 복구정보를 별도로 저장해주세요.");
        cancel?.Dispose(); cancel = new(); lastAttempt = DateTimeOffset.Now; return await Engine.Backup(Password(), cancel.Token);
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
    public void Cancel() => cancel?.Cancel();
    public void Dispose() { cancel?.Cancel(); cancel?.Dispose(); }
}
public sealed class RestoreWindow : Window
{
    public RestoreWindow(MainWindow main)
    {
        Title = "백업 복구시점 / 별도 위치 복원"; Width = 820; Height = 650; Background = UI.Canvas; FontFamily = main.FontFamily;
        var panel = new DockPanel { Margin = new Thickness(24) }; var top = new StackPanel(); top.Children.Add(UI.Text("원본을 바꾸지 않고 먼저 복원합니다", 22, true)); top.Children.Add(UI.Text("복구시점 선택 → 업무 루트와 다른 빈 폴더로 복원 → 파일 확인. 복원한 자료를 확인한 뒤 관리 루트를 전환할 수 있습니다.")); var status = UI.Text(""); top.Children.Add(status); var list = new ListBox { DisplayMemberPath = "Time" };
        top.Children.Add(UI.Button("복구시점 목록 읽기", () => { _ = UI.GuardAsync(async () => { list.ItemsSource = await main.Backups!.Points(); }); })); DockPanel.SetDock(top, Dock.Top); panel.Children.Add(top);
        var buttons = new StackPanel(); string? extracted = null;
        buttons.Children.Add(UI.Button("선택 시점을 빈 폴더로 복원", () => { _ = UI.GuardAsync(async () => { if (list.SelectedItem is not BackupPoint point) throw new IOException("복구시점을 선택해주세요."); string? target = UI.PickFolder("별도 빈 폴더 선택"); if (target is null) return; status.Text = "복원 중 · 원본은 바뀌지 않습니다."; await main.Backups!.Restore(point.Id, target); extracted = target; status.Text = "복원 및 파일 검사 완료 · " + target; UI.Open(target); }); }, true));
        buttons.Children.Add(UI.Button("확인한 복원본을 관리 루트로 열기", () =>
        {
            string? root = UI.PickFolder("복원된 자료 중 .업무정리와 주기 폴더가 들어 있는 루트를 선택", extracted); if (root is null) return;
            if (!File.Exists(Path.Combine(root, ".업무정리", "설정.json"))) throw new IOException("선택한 위치에서 업무정리 설정을 찾을 수 없습니다.");
            if (!UI.Confirm("현재 원본은 그대로 두고 선택한 복원본을 관리 대상으로 열까요? 다른 PC에서는 복구정보를 다시 가져와야 할 수 있습니다.")) return;
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
