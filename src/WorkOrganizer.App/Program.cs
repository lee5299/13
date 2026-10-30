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
        if (args.Contains("--demo")) root = Demo.Create();
        int i = Array.IndexOf(args, "--root"); if (i >= 0 && i + 1 < args.Length) root = args[i + 1];
        if (root is null) { try { root = Disk.ReadJson<ClientConfig>(ConfigPath)?.Root; } catch { } }
        app.Run(new MainWindow(root));
    }
    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "프로그램설정.json");
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
        Grid.SetRow(body, 2); outer.Children.Add(body); Grid.SetRow(status, 3); outer.Children.Add(status); Content = outer;
        search.TextChanged += (_, _) => RefreshRows(); from.SelectedDateChanged += (_, _) => RefreshRows(); to.SelectedDateChanged += (_, _) => RefreshRows();
        active.MouseDoubleClick += (_, _) => OpenSelected(active); review.MouseDoubleClick += (_, _) => OpenSelected(review);
        active.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) OpenSelected(active); }; review.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) OpenSelected(review); };
        forecast.MouseDoubleClick += (_, _) => { if (forecast.SelectedItem is ForecastRow f) NewWork(f.Item); };
        timer.Tick += async (_, _) => { await Refresh(); if (Backups is not null) await Backups.TryScheduled(); }; timer.Start();
        Closed += (_, _) => { timer.Stop(); desktop.Dispose(); Backups?.Dispose(); };
        Closing += (_, e) => { foreach (var w in details.Values.ToList()) { w.Close(); if (w.IsVisible) { e.Cancel = true; return; } } if (Backups?.Engine.Running == true && !UI.Confirm("백업/복원이 실행 중입니다. 중단하고 종료할까요? 이전 복구본은 유지됩니다.")) e.Cancel = true; };
        Loaded += async (_, _) => { if (!string.IsNullOrWhiteSpace(root)) SetRoot(root); await Refresh(); };
    }
    private static Border Section(string heading, UIElement child) { var dock = new DockPanel(); var label = UI.Text(heading, 15, true); DockPanel.SetDock(label, Dock.Top); dock.Children.Add(label); dock.Children.Add(child); return UI.Card(dock); }
    public void RequireStore(Action action) { if (Store is null) { SetRootFromDialog(); if (Store is null) return; } action(); }
    private void SetRootFromDialog() { var path = UI.PickFolder("업무를 관리할 루트폴더 선택"); if (path is not null) SetRoot(path); }
    public void SetRoot(string root)
    {
        if (Store is not null && !Store.Root.Equals(System.IO.Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) && details.Count > 0) throw new IOException("열어둔 작업 상세 창을 먼저 닫아주세요. 미저장 내용은 각 창에서 보호합니다.");
        if (Backups?.Engine.Running == true) throw new IOException("백업/복원이 끝난 뒤 루트를 전환해주세요.");
        var next = new WorkStore(root); Backups?.Dispose(); Store = next; Backups = new BackupController(next);
        Disk.WriteJson(Program.ConfigPath, new Program.ClientConfig { Root = root }); rootLabel.Text = root; _ = Refresh();
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
            string signature = DateTime.Today.ToString("yyyy-MM-dd") + System.Text.Json.JsonSerializer.Serialize(currentStore.Settings, Disk.Json) + string.Join("|", jobs.Select(x => System.Text.Json.JsonSerializer.Serialize(x, Disk.Json) + x.Folder + (x.Missing ? "" : Disk.Hash(Path.Combine(x.Folder, WorkText.FileName)))));
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
            string record = x.Missing ? "" : WorkText.Load(x.Folder);
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
        using var db = new SqliteConnection("Data Source=" + Path.Combine(store.Control, "검색.sqlite")); db.Open();
        using var transaction = db.BeginTransaction();
        using var cmd = db.CreateCommand(); cmd.Transaction = transaction;
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS work(id TEXT PRIMARY KEY,name TEXT,folder TEXT,request TEXT,requested TEXT,completed TEXT,state TEXT); DELETE FROM work;"; cmd.ExecuteNonQuery();
        foreach (var x in items)
        {
            cmd.Parameters.Clear(); cmd.CommandText = "INSERT INTO work VALUES($id,$name,$folder,$request,$requested,$completed,$state)";
            cmd.Parameters.AddWithValue("$id", x.Id); cmd.Parameters.AddWithValue("$name", x.Name); cmd.Parameters.AddWithValue("$folder", x.Folder); cmd.Parameters.AddWithValue("$request", x.Missing ? "" : x.Request); cmd.Parameters.AddWithValue("$requested", x.RequestedOn.ToString("yyyy-MM-dd")); cmd.Parameters.AddWithValue("$completed", x.CompletedAt?.ToString("O") ?? ""); cmd.Parameters.AddWithValue("$state", x.State.ToString()); cmd.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
