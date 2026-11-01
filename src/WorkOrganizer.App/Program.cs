using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WorkOrganizer.Core;
using Microsoft.Data.Sqlite;

namespace WorkOrganizer.App;

public static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "Local\\WorkOrganizer.Personal", out bool first);
        if (!first) { MessageBox.Show("업무정리가 이미 실행 중입니다. 작업표시줄에서 열어주세요."); return; }
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) => { MessageBox.Show(e.Exception.Message, "처리 확인", MessageBoxButton.OK, MessageBoxImage.Warning); e.Handled = true; };
        app.Resources.Add(typeof(Button), UI.ButtonStyle());
        app.Resources.Add(typeof(TextBox), UI.TextStyle());
        app.Resources.Add(typeof(DatePicker), new Style(typeof(DatePicker)) { Setters = { new Setter(FrameworkElement.MarginProperty, new Thickness(0, 4, 0, 6)) } });
        string? root = null;
        if (args.Contains("--demo")) { root = Demo.Create(); MessageBox.Show("시험자료로 실행합니다.\n시험자료의 업무정리 암호: " + Demo.Passphrase + "\n실제 업무자료에는 쓰지 마세요.", "업무정리 시험자료"); }
        int i = Array.IndexOf(args, "--root"); if (i >= 0 && i + 1 < args.Length) root = args[i + 1];
        if (root is null) { try { root = (Disk.ReadJson<ClientConfig>(ConfigPath) ?? Disk.ReadJson<ClientConfig>(LegacyConfigPath))?.Root; } catch { } }
        app.Run(new MainWindow(root));
    }
    // 설치형(셋업)에서는 실행 폴더가 아니라 사용자 설정 폴더에 기기별 설정을 둔다. 업무자료는 사용자가 정한 루트에만 저장한다.
    public static string ConfigFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "업무정리");
    public static string ConfigPath => Path.Combine(ConfigFolder, "프로그램설정.json");
    private static string LegacyConfigPath => Path.Combine(AppContext.BaseDirectory, "프로그램설정.json");
    public sealed class ClientConfig { public string Root { get; set; } = ""; }
}
public static class UI
{
    public static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(43, 51, 53));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(111, 123, 127));
    public static readonly Brush Teal = new SolidColorBrush(Color.FromRgb(60, 105, 104));
    public static readonly Brush Canvas = new SolidColorBrush(Color.FromRgb(245, 246, 243));
    public static readonly Brush Line = new SolidColorBrush(Color.FromRgb(222, 225, 221));
    public static Style ButtonStyle() => new(typeof(Button)) { Setters = { new Setter(Control.PaddingProperty, new Thickness(13, 8, 13, 8)), new Setter(FrameworkElement.MarginProperty, new Thickness(3)), new Setter(Control.BackgroundProperty, Brushes.White), new Setter(Control.BorderBrushProperty, Line), new Setter(Control.ForegroundProperty, Ink), new Setter(Control.FontSizeProperty, 13.0), new Setter(Control.CursorProperty, System.Windows.Input.Cursors.Hand) } };
    public static Style TextStyle() => new(typeof(TextBox)) { Setters = { new Setter(Control.PaddingProperty, new Thickness(8)), new Setter(Control.BorderBrushProperty, Line), new Setter(Control.BackgroundProperty, Brushes.White), new Setter(Control.ForegroundProperty, Ink), new Setter(Control.FontSizeProperty, 13.0), new Setter(FrameworkElement.MarginProperty, new Thickness(0, 4, 0, 6)) } };
    public static TextBlock Text(string text, double size = 13, bool bold = false) => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 5) };
    public static Button Button(string label, Action action, bool primary = false)
    {
        var b = new Button { Content = label }; if (primary) { b.Background = Teal; b.Foreground = Brushes.White; }
        b.Click += (_, _) => Guard(action); return b;
    }
    public static void Guard(Action action) { try { action(); } catch (Exception e) { MessageBox.Show(e.Message, "확인이 필요합니다", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    public static async Task GuardAsync(Func<Task> action) { try { await action(); } catch (Exception e) { MessageBox.Show(e.Message, "확인이 필요합니다", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    public static StackPanel Row(params UIElement[] items) { var p = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var x in items) p.Children.Add(x); return p; }
    public static Border Card(UIElement child) => new() { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(15), Margin = new Thickness(0, 8, 0, 5), Child = child };
    public static Window Window(string title, double width = 780, double height = 650) => new() { Title = title, Width = width, Height = height, MinWidth = 520, MinHeight = 400, Background = Canvas, FontFamily = new FontFamily("Malgun Gothic"), Foreground = Ink, WindowStartupLocation = WindowStartupLocation.CenterOwner };
    public static string? PickFolder(string title, string? initial = null)
    {
        using var d = new System.Windows.Forms.FolderBrowserDialog { Description = title, UseDescriptionForTitle = true, SelectedPath = initial ?? "" };
        return d.ShowDialog() == System.Windows.Forms.DialogResult.OK ? d.SelectedPath : null;
    }
    public static string? PickFile(string title, string? initial = null, string filter = "모든 파일|*.*")
    {
        var d = new Microsoft.Win32.OpenFileDialog { Title = title, InitialDirectory = initial ?? "", Filter = filter };
        return d.ShowDialog() == true ? d.FileName : null;
    }
    public static void Open(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("경로를 찾을 수 없습니다.", path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    public static bool Confirm(string text) => MessageBox.Show(text, "확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    public static DataGrid Grid(params (string Header, string Binding, double Width)[] columns)
    {
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false, SelectionMode = DataGridSelectionMode.Single, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = Line, RowHeight = 38, Background = Brushes.White, BorderThickness = new Thickness(0), FontSize = 13, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        foreach (var (header, binding, width) in columns) grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(binding), Width = new DataGridLength(width, width <= 8 ? DataGridLengthUnitType.Star : DataGridLengthUnitType.Pixel), ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis), new Setter(FrameworkElement.MarginProperty, new Thickness(7, 8, 5, 0)) } } });
        grid.LoadingRow += (_, e) => e.Row.ToolTip = e.Row.Item.ToString(); return grid;
    }
}

public sealed class MainWindow : Window
{
    public WorkStore? Store { get; private set; }
    public List<WorkItem> Items { get; private set; } = [];
    public BackupController? Backups { get; private set; }
    /// <summary>잠금 해제된 private 보관소. 키는 이 객체의 메모리에만 있다. null이면 잠김.</summary>
    public PrivateVault? Vault { get; private set; }
    public event Action? PrivateStateChanged;
    private readonly Border externalBanner = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock rootLabel = UI.Text("루트폴더를 선택하면 새 업무부터 관리합니다.");
    private readonly TextBlock status = UI.Text("");
    private readonly TextBlock capacity = UI.Text("");
    private readonly TextBox search = new() { MinWidth = 270, ToolTip = "폴더명 · 요청 원문 · 본인 이해 검색" };
    private readonly DatePicker from = new() { Width = 130 }, to = new() { Width = 130 };
    private readonly DataGrid active = UI.Grid(("작업 폴더", "Folder", 3), ("요청 원문", "Request", 3), ("요청일", "Requested", 95), ("마감", "Due", 95), ("예상량", "Effort", 80));
    private readonly DataGrid review = UI.Grid(("검토중 요청", "Folder", 3), ("요청 내용", "Request", 3), ("예상량", "Effort", 80));
    private readonly DataGrid forecast = UI.Grid(("구분", "Kind", 160), ("업무명", "Name", 2), ("시기 · 내용", "Description", 5));
    private readonly Button completed;
    private readonly System.Windows.Threading.DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly DesktopHost desktop;
    private bool refreshing;
    private string lastSignature = "";
    private readonly Dictionary<string, DetailWindow> details = new();
    public MainWindow(string? root)
    {
        Title = "업무 정리"; Width = 1180; Height = 900; MinWidth = 900; MinHeight = 650; Background = UI.Canvas; FontFamily = new FontFamily("Malgun Gothic"); Foreground = UI.Ink;
        desktop = new DesktopHost(this);
        var outer = new Grid { Margin = new Thickness(24) };
        outer.RowDefinitions.Add(new() { Height = GridLength.Auto }); outer.RowDefinitions.Add(new() { Height = GridLength.Auto }); outer.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); outer.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var top = new DockPanel(); var title = new StackPanel(); title.Children.Add(UI.Text("업무 정리", 27, true)); title.Children.Add(rootLabel); top.Children.Add(title);
        completed = UI.Button("완료 작업", () => RequireStore(() => new CompletedWindow(this).Show()));
        var buttons = UI.Row(UI.Button("1년 일정", () => RequireStore(() => new CalendarWindow(this).Show())), completed, UI.Button("+ 새 요청", () => NewWork(), true), UI.Button("설정", Settings)); DockPanel.SetDock(buttons, Dock.Right); top.Children.Insert(0, buttons); outer.Children.Add(top);
        var filters = UI.Row(search, UI.Text("  요청일 "), from, UI.Text(" ~ "), to, UI.Button("지우기", () => { search.Clear(); from.SelectedDate = null; to.SelectedDate = null; RefreshRows(); })); Grid.SetRow(filters, 1); outer.Children.Add(filters);
        var body = new Grid(); body.RowDefinitions.Add(new() { Height = new GridLength(1.3, GridUnitType.Star) }); body.RowDefinitions.Add(new() { Height = new GridLength(2.4, GridUnitType.Star) }); body.RowDefinitions.Add(new() { Height = new GridLength(1.4, GridUnitType.Star) });
        body.Children.Add(Section("이번 달 할 일 · 예상", forecast)); var work = Section("작업 리스트", active); Grid.SetRow(work, 1); body.Children.Add(work);
        var rp = new DockPanel(); DockPanel.SetDock(capacity, Dock.Top); rp.Children.Add(capacity); rp.Children.Add(review); var reviews = Section("수락할지 검토 중인 요청", rp); Grid.SetRow(reviews, 2); body.Children.Add(reviews);
        Grid.SetRow(body, 2); outer.Children.Add(body);
        var bottom = new StackPanel(); bottom.Children.Add(externalBanner); bottom.Children.Add(status); Grid.SetRow(bottom, 3); outer.Children.Add(bottom); Content = outer;
        search.TextChanged += (_, _) => RefreshRows(); from.SelectedDateChanged += (_, _) => RefreshRows(); to.SelectedDateChanged += (_, _) => RefreshRows();
        active.MouseDoubleClick += (_, _) => OpenSelected(active); review.MouseDoubleClick += (_, _) => OpenSelected(review);
        active.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) OpenSelected(active); }; review.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) OpenSelected(review); };
        forecast.MouseDoubleClick += (_, _) => { if (forecast.SelectedItem is ForecastRow f) NewWork(f.Item); };
        timer.Tick += async (_, _) => { await Refresh(); if (Backups is not null) await Backups.TryScheduled(); }; timer.Start();
        Closed += (_, _) => { timer.Stop(); desktop.Dispose(); Backups?.Dispose(); Vault?.Lock(); Vault = null; };
        Closing += (_, e) => { foreach (var w in details.Values.ToList()) { w.Close(); if (w.IsVisible) { e.Cancel = true; return; } } if (Backups?.Engine.Running == true && !UI.Confirm("백업/복원이 실행 중입니다. 중단하고 종료할까요? 이전 복구본은 유지됩니다.")) e.Cancel = true; };
        Loaded += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(root))
            {
                try { SetRoot(root); }
                catch (DirectoryNotFoundException) { MessageBox.Show("마지막으로 쓰던 루트폴더에 접근할 수 없습니다. 자료가 지워졌다는 뜻은 아닙니다. 외장디스크·네트워크 연결을 확인하거나 루트를 다시 선택해주세요.\n" + root, "루트 재연결"); FirstRun(true); }
            }
            else FirstRun(false);
            await Refresh();
        };
    }
    private static Border Section(string heading, UIElement child) { var dock = new DockPanel(); var label = UI.Text(heading, 15, true); DockPanel.SetDock(label, Dock.Top); dock.Children.Add(label); dock.Children.Add(child); return UI.Card(dock); }
    public void RequireStore(Action action) { if (Store is null) { SetRootFromDialog(); if (Store is null) return; } action(); }
    /// <summary>첫 실행(또는 루트 접근 불가) 안내. 업무자료를 둘 루트폴더를 고른다. 나중에 설정에서 바꿀 수 있다.</summary>
    private void FirstRun(bool reconnect)
    {
        var w = UI.Window(reconnect ? "루트폴더 다시 선택" : "업무정리 시작", 640, 430); w.Owner = this; w.ResizeMode = ResizeMode.NoResize;
        var p = new StackPanel { Margin = new Thickness(26) };
        p.Children.Add(UI.Text(reconnect ? "루트폴더를 다시 선택해주세요" : "업무자료를 둘 루트폴더를 정해주세요", 20, true));
        p.Children.Add(UI.Text("요청마다 이 폴더 아래에 날짜가 붙은 업무폴더가 만들어집니다. 실제 업무자료를 둘 폴더나, 시험해 보려면 비어 있는 새 폴더를 고르세요. 기존 폴더의 자료를 옮기거나 합치지 않습니다."));
        p.Children.Add(UI.Text("나중에 설정 → 기본 / 주기 → ‘다른 루트로 전환’에서 바꿀 수 있습니다.", 12));
        p.Children.Add(UI.Row(UI.Button("나중에", () => w.Close()), UI.Button("루트폴더 선택", () => { var path = UI.PickFolder("업무를 관리할 루트폴더 선택 · ‘새 폴더 만들기’도 가능"); if (path is null) return; SetRoot(path); w.Close(); }, true)));
        w.Content = p; w.ShowDialog();
    }
    private void SetRootFromDialog() { var path = UI.PickFolder("업무를 관리할 루트폴더 선택"); if (path is not null) SetRoot(path); }
    public void SetRoot(string root)
    {
        if (Store is not null && !Store.Root.Equals(System.IO.Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) && details.Count > 0) throw new IOException("열어둔 작업 상세 창을 먼저 닫아주세요. 미저장 내용은 각 창에서 보호합니다.");
        if (Backups?.Engine.Running == true) throw new IOException("백업/복원이 끝난 뒤 루트를 전환해주세요.");
        var next = new WorkStore(root); Backups?.Dispose(); Vault?.Lock(); Vault = null; Store = next; Backups = new BackupController(next); PrivateStateChanged?.Invoke(); UpdateExternalBanner();
        Disk.WriteJson(Program.ConfigPath, new Program.ClientConfig { Root = root }); rootLabel.Text = root; _ = Refresh();
    }
    /// <summary>private 잠금 해제. 보관소가 없으면 암호를 새로 정한다. 외부 AI 작업 잠금 중에는 해제하지 않는다(PRIVATE_LOCK_DURING).</summary>
    public PrivateVault? EnsurePrivateUnlocked(Window owner)
    {
        if (Store is null) throw new InvalidOperationException("루트폴더를 먼저 지정해주세요.");
        if (ExternalAiSession.IsActive(Store)) throw new InvalidOperationException("외부 폴더 AI 작업 잠금 중입니다. 작업을 끝내고 ‘외부 AI 작업 종료 확인’을 누른 뒤 private 잠금을 해제할 수 있습니다.");
        if (Vault is { IsLocked: false }) return Vault;
        bool create = !PrivateVault.Exists(Store.Control);
        var w = new PassphraseWindow(owner, create, create ? null : "private 잠금을 해제합니다.");
        if (w.ShowDialog() != true || w.Passphrase is null) return null;
        Vault = create ? PrivateVault.Create(Store.Control, w.Passphrase) : PrivateVault.Unlock(Store.Control, w.Passphrase);
        PrivateStateChanged?.Invoke(); return Vault;
    }
    /// <summary>
    /// 업무정리 암호를 받아 돌려준다(복구정보·백업파일용). 이 루트의 보관소로 맞는 암호인지 확인한다.
    /// 아직 암호를 정하지 않았으면 새로 정한다. 외부 AI 작업 잠금 중에는 묻지 않는다.
    /// </summary>
    public string? AskUserPassword(Window owner, string purpose)
    {
        if (Store is null) throw new InvalidOperationException("루트폴더를 먼저 지정해주세요.");
        if (ExternalAiSession.IsActive(Store)) throw new InvalidOperationException("외부 폴더 AI 작업 잠금 중에는 암호를 입력받지 않습니다. 작업을 끝낸 뒤 다시 시도해주세요.");
        bool create = !PrivateVault.Exists(Store.Control);
        var w = new PassphraseWindow(owner, create, purpose);
        if (w.ShowDialog() != true || w.Passphrase is null) return null;
        var vault = create ? PrivateVault.Create(Store.Control, w.Passphrase) : PrivateVault.Unlock(Store.Control, w.Passphrase);
        if (Vault is { IsLocked: false }) vault.Lock(); else { Vault = vault; PrivateStateChanged?.Invoke(); }
        return w.Passphrase;
    }
    /// <summary>다른 PC의 복구정보나 예전 백업파일처럼 이 루트와 무관한 암호를 받을 때(확인 없이 입력만).</summary>
    public static string? AskPasswordOnly(Window owner, string purpose)
    {
        var w = new PassphraseWindow(owner, false, purpose);
        return w.ShowDialog() == true ? w.Passphrase : null;
    }
    public void ChangeUserPassword(Window owner)
    {
        if (Store is null) throw new InvalidOperationException("루트폴더를 먼저 지정해주세요.");
        if (ExternalAiSession.IsActive(Store)) throw new InvalidOperationException("외부 폴더 AI 작업 잠금 중에는 암호를 바꾸지 않습니다.");
        if (!PrivateVault.Exists(Store.Control)) throw new InvalidOperationException("아직 업무정리 암호를 정하지 않았습니다. private 잠금 해제나 복구정보 저장에서 먼저 정해주세요.");
        var w = new ChangePasswordWindow(owner, Store.Control);
        if (w.ShowDialog() != true || w.NewPassword is null) return;
        bool diskCopy = false; try { diskCopy = Backups?.WriteRecoveryCopy(w.NewPassword) == true; } catch (Exception ex) { MessageBox.Show("백업 위치의 복구정보를 새 암호로 바꾸지 못했습니다: " + ex.Message); }
        if (UI.Confirm("암호를 바꿨습니다. private 파일은 새 암호로 열립니다." + (diskCopy ? "\n백업 위치(외장디스크)의 복구정보도 새 암호로 바꿨습니다." : "") + "\n\n따로 보관하던 복구정보 파일도 새 암호로 다시 저장할까요? 옛 암호로 잠근 파일은 지워주세요."))
            Backups?.ExportKey(w.NewPassword);
        MessageBox.Show("이미 만든 백업파일(.wobak)은 만들 때의 암호가 그대로 필요합니다. 필요하면 새로 만들어 올리고 예전 파일은 지워주세요.", "암호 변경 완료");
    }
    /// <summary>private 키를 메모리에서 지운다. 미저장 private 편집이 있으면 저장 여부를 먼저 묻는다(PRIVATE_LOCK_START).</summary>
    public bool TryLockPrivate(string reason)
    {
        foreach (var w in details.Values.Where(x => x.PrivateDirty).ToList())
        {
            var answer = MessageBox.Show($"{reason} 전에 private 상세의 미저장 내용을 저장할까요?\n예: 저장 후 잠금 · 아니요/취소: 잠그지 않고 돌아가기", w.Title, MessageBoxButton.YesNo);
            if (answer != MessageBoxResult.Yes) return false;
            try { w.SavePrivateNow(); } catch (Exception ex) { MessageBox.Show("저장하지 못해 잠그지 않았습니다. 입력은 창에 남아 있습니다.\n" + ex.Message); return false; }
        }
        Vault?.Lock(); Vault = null; PrivateStateChanged?.Invoke(); return true;
    }
    /// <summary>외부 폴더 AI 작업 시작(PRIVATE_LOCK_FLOW). 잠금·평문 점검이 성공해야만 PowerShell 창을 연다.</summary>
    public void StartExternalAi(string tool)
    {
        if (Store is null) throw new InvalidOperationException("루트폴더를 먼저 지정해주세요.");
        if (ExternalAiSession.IsActive(Store)) throw new InvalidOperationException("이미 외부 AI 작업 잠금 중입니다.");
        if (!UI.Confirm("외부 폴더 AI 작업을 시작할까요?\n1) private 미저장 내용을 저장하고 private 키를 메모리에서 지웁니다.\n2) 루트 안에 AI가 읽을 수 있는 private 평문이 없는지 확인합니다.\n3) 업무 루트에서 PowerShell 창을 엽니다. 작업 중에는 private을 보거나 편집할 수 없습니다.")) return;
        if (!TryLockPrivate("외부 AI 작업 시작")) return;
        ExternalAiSession.Begin(Store, Vault, tool, null);
        try
        {
            string command = "Set-Location -LiteralPath '" + Store.Root.Replace("'", "''") + "'" + (tool is "claude" or "codex" ? "; " + tool : "");
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = false, WorkingDirectory = Store.Root };
            start.ArgumentList.Add("-NoExit"); start.ArgumentList.Add("-NoLogo"); start.ArgumentList.Add("-Command"); start.ArgumentList.Add(command);
            CliAiConnector.Sanitize(start.Environment);
            var process = Process.Start(start) ?? throw new IOException("PowerShell 창을 열지 못했습니다.");
            ExternalAiSession.Attach(Store, process);
        }
        catch { ExternalAiSession.End(Store, false); throw; }
        UpdateExternalBanner();
    }
    public void EndExternalAi()
    {
        if (Store is null || !ExternalAiSession.IsActive(Store)) { UpdateExternalBanner(); return; }
        var l = ExternalAiSession.Read(Store);
        if (l is not null && ExternalAiSession.IsProcessAlive(l))
        {
            if (!UI.Confirm("외부 AI 작업 창이 아직 실행 중입니다. 창(과 그 안의 AI 프로그램)을 종료하고 잠금을 해제할까요?\n아니요를 누르면 잠금을 유지합니다.")) return;
            ExternalAiSession.End(Store, true);
        }
        else ExternalAiSession.End(Store, false);
        int others = ExternalAiSession.OtherAiProcesses();
        UpdateExternalBanner();
        MessageBox.Show("외부 AI 작업 잠금을 끝냈습니다. private은 필요할 때 다시 잠금 해제하세요." + (others > 0 ? $"\n참고: 다른 Claude/Codex 프로세스 {others}개가 실행 중입니다. private 파일은 암호문이지만, 잠금 해제 중인 앱 메모리에 대한 보호는 검증되지 않았습니다." : ""), "외부 AI 작업 종료");
    }
    public void UpdateExternalBanner()
    {
        if (Store is null || !ExternalAiSession.IsActive(Store)) { externalBanner.Visibility = Visibility.Collapsed; return; }
        var l = ExternalAiSession.Read(Store); var p = new StackPanel();
        p.Children.Add(UI.Text($"외부 폴더 AI 작업 잠금 중 · {l?.Tool} · {l?.StartedAt:MM.dd HH:mm} 시작 · private 열람/편집 잠금", 14, true));
        p.Children.Add(UI.Row(UI.Button("외부 AI 작업 종료 확인", EndExternalAi, true)));
        externalBanner.Child = UI.Card(p); externalBanner.Visibility = Visibility.Visible;
    }
    private void Settings()
    {
        if (Store is null) { SetRootFromDialog(); return; }
        new SettingsWindow(this, desktop).ShowDialog(); _ = Refresh();
    }
    public void NewWork(Forecast? occurrence = null)
    {
        RequireStore(() => { var w = new NewWorkWindow(this, occurrence); if (w.ShowDialog() == true && w.Created is not null) ShowDetail(w.Created); _ = Refresh(); });
    }
    public void ShowDetail(WorkItem item)
    {
        if (details.TryGetValue(item.Id, out var existing)) { existing.Activate(); return; }
        var detail = new DetailWindow(this, item); details.Add(item.Id, detail); detail.Closed += (_, _) => details.Remove(item.Id); detail.Show();
    }
    private void OpenSelected(DataGrid grid) { if (grid.SelectedItem is WorkRow row) ShowDetail(row.Item); }
    public async Task Refresh()
    {
        if (Store is null || refreshing) return; refreshing = true;
        try
        {
            var currentStore = Store; var jobs = await Task.Run(currentStore.Scan); if (!ReferenceEquals(Store, currentStore)) return;
            string signature = DateTime.Today.ToString("yyyy-MM-dd") + System.Text.Json.JsonSerializer.Serialize(currentStore.Settings, Disk.Json) + string.Join("|", jobs.Select(x => System.Text.Json.JsonSerializer.Serialize(x, Disk.Json) + x.Folder + (x.Missing ? "" : Disk.Hash(Path.Combine(x.Folder, WorkText.FileName)) + Disk.Hash(Path.Combine(x.Folder, WorkText.LegacyFileName)))));
            UpdateExternalBanner();
            Items = jobs;
            if (signature != lastSignature)
            {
                lastSignature = signature; RefreshRows();
                await Task.Run(() => SearchIndex.Rebuild(currentStore, jobs));
            }
        }
        catch (Exception e) { status.Text = "확인 필요 · " + e.Message; }
        finally { refreshing = false; }
    }
    private void RefreshRows()
    {
        if (Store is null) { status.Text = "설정에서 루트폴더를 지정해주세요."; return; }
        if (from.SelectedDate > to.SelectedDate) { status.Text = "요청일의 시작일이 종료일보다 늦습니다."; return; }
        var today = DateOnly.FromDateTime(DateTime.Today); string q = search.Text.Trim();
        bool Match(WorkItem x)
        {
            if (from.SelectedDate is DateTime a && x.RequestedOn < DateOnly.FromDateTime(a) || to.SelectedDate is DateTime b && x.RequestedOn > DateOnly.FromDateTime(b)) return false;
            string record = x.Missing ? "" : WorkText.LoadForDisplay(x.Folder); // 로컬 화면 검색. private은 검색하지 않는다.
            string text = x.FolderName + " " + x.Name + " " + WorkText.ReadSection(record, "요청 원문") + " " + WorkText.ReadSection(record, "본인이 이해한 요청");
            return text.Contains(q, StringComparison.OrdinalIgnoreCase);
        }
        active.ItemsSource = Planning.Ordered(Items.Where(x => x.State is WorkState.Active or WorkState.Waiting).Where(Match), today, Store.Settings).Select(x => new WorkRow(x, today, Store.Settings)).ToList();
        review.ItemsSource = Items.Where(x => x.State == WorkState.Review).Where(Match).OrderByDescending(x => x.RegisteredAt).Select(x => new WorkRow(x, today, Store.Settings)).ToList();
        forecast.ItemsSource = Planning.Forecasts(Items, Store.Settings, today.Year, today.Month).Where(x => (x.Name + x.Description).Contains(q, StringComparison.OrdinalIgnoreCase)).Select(x => new ForecastRow(x)).ToList();
        capacity.Text = "이번 주 " + Planning.Calculate(Items, today, Planning.WeekEnd(today), Store.Settings).Message + "\n이번 달 " + Planning.Calculate(Items, today, Planning.MonthEnd(today), Store.Settings).Message;
        var start = Planning.WeekEnd(today).AddDays(-6); int count = Items.Count(x => x.State == WorkState.Completed && x.CompletedAt is not null && DateOnly.FromDateTime(x.CompletedAt.Value.LocalDateTime) >= start && DateOnly.FromDateTime(x.CompletedAt.Value.LocalDateTime) <= today);
        completed.Content = $"완료 작업 · 이번 주 {count}";
        status.Text = $"오늘 {today:yyyy.MM.dd} 포함 · 파일 내용 자동 분석 없음 · {Store.Settings.LastBackupStatus}";
        if (Store.Issues.Count > 0) status.Text += "\n" + string.Join(" / ", Store.Issues.Take(2));
        if (Store.NewFolders.Count > 0) status.Text += $" · 탐색기 신규폴더 {Store.NewFolders.Count}개 (새 요청에서 선택 등록)";
        int unclassified = Items.Count(x => x.Unclassified); if (unclassified > 0) status.Text += $" · 공개/private 미분류 이전 기록 {unclassified}건(AI 전송 안 함)";
    }
}
public sealed record WorkRow(WorkItem Item, DateOnly Today, AppSettings Settings)
{
    public string Folder => (Item.CycleStart != Planning.Cycle(Today, Settings) ? "[이전 주기] " : "") + (Item.Missing ? "[위치 확인] " : "") + Item.FolderName + (Item.State == WorkState.Waiting ? " · 대기" : Planning.Urgency(Item, Today, Settings) <= 2 ? " · 긴급" : "");
    public string Request => Item.Missing ? "폴더 재연결 필요" : Item.Request.ReplaceLineEndings(" ");
    public string Requested => Item.RequestedOn.ToString("yyyy.MM.dd");
    public string Due => Item.DueOn?.ToString("yyyy.MM.dd") ?? "미정";
    public string Effort => Item.Effort.ToString("0.##") + "일";
    public override string ToString() => Folder + "\n" + Request;
}
public sealed record ForecastRow(Forecast Item)
{
    public string Name => Item.Name;
    public string Kind => Item.Description + (Item.HasWork ? " · 요청 등록됨" : "");
    public string Description => $"{Item.From:MM.dd} ~ {Item.To:MM.dd} · 두 번 클릭해 내용/새 요청";
}
public static class SearchIndex
{
    public static void Rebuild(WorkStore store, List<WorkItem> items)
    {
        // 보조 색인은 공개기록의 요청 원문만 담는다. private·이전 단일기록은 넣지 않는다(AI_PRIVATE_DERIVED). 지운 자료가 빈 페이지에 남지 않게 secure_delete.
        using var db = new SqliteConnection("Data Source=" + Path.Combine(store.Control, "검색.sqlite")); db.Open();
        using (var pragma = db.CreateCommand()) { pragma.CommandText = "PRAGMA secure_delete=ON;"; pragma.ExecuteNonQuery(); }
        using var transaction = db.BeginTransaction();
        using var cmd = db.CreateCommand(); cmd.Transaction = transaction;
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS work(id TEXT PRIMARY KEY,name TEXT,folder TEXT,request TEXT,requested TEXT,completed TEXT,state TEXT); DELETE FROM work;"; cmd.ExecuteNonQuery();
        foreach (var x in items)
        {
            cmd.Parameters.Clear(); cmd.CommandText = "INSERT INTO work VALUES($id,$name,$folder,$request,$requested,$completed,$state)";
            cmd.Parameters.AddWithValue("$id", x.Id); cmd.Parameters.AddWithValue("$name", x.Name); cmd.Parameters.AddWithValue("$folder", x.Folder); cmd.Parameters.AddWithValue("$request", x.Missing ? "" : WorkText.ReadSection(WorkText.Load(x.Folder), "요청 원문")); cmd.Parameters.AddWithValue("$requested", x.RequestedOn.ToString("yyyy-MM-dd")); cmd.Parameters.AddWithValue("$completed", x.CompletedAt?.ToString("O") ?? ""); cmd.Parameters.AddWithValue("$state", x.State.ToString()); cmd.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
