using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WorkOrganizer.Core;

namespace WorkOrganizer.App;

public sealed class NewWorkWindow : Window
{
    private readonly MainWindow main;
    private readonly WorkStore store;
    public WorkItem? Created { get; private set; }
    private readonly string requestKey = Guid.NewGuid().ToString("N");
    public NewWorkWindow(MainWindow main, Forecast? forecast = null)
    {
        this.main = main; store = main.Store!; Title = "새 요청"; Width = 720; Height = 850; MinWidth = 600; MinHeight = 600; Background = UI.Canvas; FontFamily = main.FontFamily; WindowStartupLocation = WindowStartupLocation.CenterOwner; Owner = main;
        var body = new StackPanel { Margin = new Thickness(24) }; body.Children.Add(UI.Text("새 요청을 기록합니다", 24, true)); body.Children.Add(UI.Text("접수할 때 폴더를 만들고 검토중으로 보관합니다. 수락 전에는 확정 부담에서 제외합니다."));
        var name = new TextBox { Text = forecast?.Name ?? "" }; Field(body, "작업명", name);
        var candidates = new ListBox { MaxHeight = 100, DisplayMemberPath = "Name" }; body.Children.Add(candidates);
        var types = new ComboBox { ItemsSource = new[] { new TypeChoice(null) }.Concat(store.Settings.Types.Select(x => new TypeChoice(x))).ToList(), DisplayMemberPath = "Label", SelectedIndex = 0, Margin = new Thickness(0, 4, 0, 8) }; Field(body, "업무 유형 · 일반 업무도 가능", types);
        var linked = new CheckBox { Content = "이 요청을 선택한 반복업무의 이번 회차로 연결", IsChecked = forecast?.TypeId is not null, Margin = new Thickness(0, 3, 0, 8) }; body.Children.Add(linked);
        if (forecast?.TypeId is string fid) types.SelectedItem = types.Items.Cast<TypeChoice>().FirstOrDefault(x => x.Type?.Id == fid);
        var original = Multi(90); Field(body, "받은 요청 원문", original); var understood = Multi(65); Field(body, "본인이 이해한 요청", understood);
        var requested = new DatePicker { SelectedDate = DateTime.Today, Width = 135 }; var due = new DatePicker { Width = 135 }; var effort = new TextBox { Text = "1", Width = 75 }; var priority = Priority(1);
        body.Children.Add(UI.Row(Labeled("요청일", requested), Labeled("마감 · 나중에 입력 가능", due), Labeled("예상 소요일수", effort), Labeled("중요도", priority)));
        var templateSummary = UI.Text(""); body.Children.Add(templateSummary);
        string? existing = null; var location = UI.Text("현재 주기의 작업 영역에 새 폴더를 만듭니다."); body.Children.Add(UI.Row(UI.Button("탐색기에서 만든 폴더 선택", () => { existing = UI.PickFolder("현재 주기 작업 영역의 신규 폴더 선택", Path.Combine(store.CycleFolder(Planning.Cycle(DateOnly.FromDateTime(DateTime.Today), store.Settings)), "작업")); location.Text = existing ?? "새 폴더 생성"; }), UI.Button("새 폴더로", () => { existing = null; location.Text = "새 폴더 생성"; }))); body.Children.Add(location);
        var preview = UI.Text(""); body.Children.Add(preview);
        void Update()
        {
            var type = (types.SelectedItem as TypeChoice)?.Type;
            templateSummary.Text = "복사할 체크 양식: " + string.Join(", ", store.Settings.CommonTemplates.Concat(type?.Templates ?? []).Where(x => x.Selected).Select(x => Path.GetFileName(x.Path))) + "\n제출항목: " + string.Join(", ", type?.SubmissionNames ?? []);
            linked.IsEnabled = type?.Repeat != RepeatMode.None && type is not null;
            if (!linked.IsEnabled) linked.IsChecked = false;
            preview.Text = $"폴더 이름: {(requested.SelectedDate ?? DateTime.Today):yyMMdd}_{name.Text}_{original.Text.ReplaceLineEndings(" ")}\n같은 이름은 덮어쓰지 않고 구분자를 붙입니다.";
        }
        name.TextChanged += (_, _) =>
        {
            string q = name.Text; candidates.ItemsSource = store.Settings.Types.Select(x => new Candidate(x.Name, x.Id)).Concat(main.Items.Select(x => new Candidate(x.Name, x.TypeId))).DistinctBy(x => x.Name).Where(x => Planning.Similarity(q, x.Name) >= .45).OrderByDescending(x => Planning.Similarity(q, x.Name)).Take(8).ToList(); Update();
        };
        candidates.SelectionChanged += (_, _) => { if (candidates.SelectedItem is Candidate candidate) { name.Text = candidate.Name; types.SelectedItem = types.Items.Cast<TypeChoice>().FirstOrDefault(x => x.Type?.Id == candidate.TypeId) ?? types.Items[0]; } };
        types.SelectionChanged += (_, _) => Update(); original.TextChanged += (_, _) => Update(); requested.SelectedDateChanged += (_, _) => Update(); Update();
        body.Children.Add(UI.Row(UI.Button("취소", () => DialogResult = false), UI.Button("요청 폴더 만들기", () =>
        {
            if (!decimal.TryParse(effort.Text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal days) || days <= 0) throw new ArgumentException("예상 소요일수는 1 또는 0.5처럼 입력해주세요.");
            if (requested.SelectedDate is null) throw new ArgumentException("요청일을 확인해주세요.");
            var type = (types.SelectedItem as TypeChoice)?.Type; string? occurrence = null;
            if (linked.IsChecked == true && type is not null)
            {
                var date = DateOnly.FromDateTime(requested.SelectedDate.Value);
                occurrence = forecast?.Occurrence ?? $"{type.Id}:{date.Year}-{(type.Repeat == RepeatMode.Monthly ? date.Month : type.Month):00}";
            }
            Created = store.Create(name.Text, original.Text, understood.Text, DateOnly.FromDateTime(requested.SelectedDate.Value), days, priority.SelectedIndex, due.SelectedDate is DateTime d ? DateOnly.FromDateTime(d) : null, type, occurrence, existing, requestKey);
            DialogResult = true;
        }, true)));
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body };
    }
    public static TextBox Multi(double height) => new() { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = height };
    public static void Field(Panel panel, string label, UIElement input) { panel.Children.Add(UI.Text(label, 13, true)); panel.Children.Add(input); }
    public static StackPanel Labeled(string label, UIElement input) { var p = new StackPanel { Margin = new Thickness(0, 0, 15, 0) }; Field(p, label, input); return p; }
    public static ComboBox Priority(int selected) => new() { ItemsSource = new[] { "낮음", "보통", "높음" }, SelectedIndex = selected, Width = 95, Margin = new Thickness(0, 4, 0, 6) };
    private sealed record TypeChoice(WorkType? Type) { public string Label => Type?.Name ?? "일반 업무 · 유형 없음"; }
    private sealed record Candidate(string Name, string? TypeId);
}

public sealed class DetailWindow : Window
{
    private readonly MainWindow main;
    private readonly WorkStore store;
    private readonly WorkItem item;
    private readonly TextBox record = new() { AcceptsReturn = true, AcceptsTab = true, FontFamily = new FontFamily("Consolas, Malgun Gothic"), FontSize = 15, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock note = UI.Text("");
    private readonly TextBlock visible = UI.Text("");
    private readonly StackPanel side = new();
    private string basis = "", saved = "";
    private bool dirty;
    private bool metadataDirty;
    private Action? saveMetadata;
    private readonly System.Windows.Threading.DispatcherTimer watcher = new() { Interval = TimeSpan.FromSeconds(3) };
    public DetailWindow(MainWindow main, WorkItem item)
    {
        this.main = main; store = main.Store!; this.item = item;
        Title = item.Name + " — 작업 상세"; Width = 1280; Height = 900; MinWidth = 950; MinHeight = 650; Background = UI.Canvas; FontFamily = main.FontFamily;
        var grid = new Grid { Margin = new Thickness(20) }; grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var heading = new DockPanel(); var buttons = UI.Row(UI.Button("폴더 열기", () => UI.Open(item.Folder)), UI.Button("기록 외부 열기", () => { SaveAll(); UI.Open(Path.Combine(item.Folder, WorkText.FileName)); }), UI.Button("최대화 / 복원", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized), UI.Button("저장", SaveAll, true)); DockPanel.SetDock(buttons, Dock.Right); heading.Children.Add(buttons); heading.Children.Add(UI.Text(item.Name, 23, true)); grid.Children.Add(heading);
        Grid.SetRow(note, 1); grid.Children.Add(note);
        var columns = new Grid(); columns.ColumnDefinitions.Add(new() { Width = new GridLength(7, GridUnitType.Star) }); columns.ColumnDefinitions.Add(new() { Width = new GridLength(18) }); columns.ColumnDefinitions.Add(new() { Width = new GridLength(3, GridUnitType.Star), MinWidth = 285 });
        var font = new ComboBox { ItemsSource = new[] { 14, 15, 16, 18, 20 }, SelectedItem = 15, Width = 55, Margin = new Thickness(8, 3, 8, 3), ToolTip = "기록 글자 크기" }; font.SelectionChanged += (_, _) => { if (font.SelectedItem is int size) record.FontSize = size; };
        var editor = new DockPanel(); var title = UI.Row(UI.Text("작업 기록", 15, true), visible, font, UI.Button("외부 변경 다시 읽기", () => { if (dirty && !UI.Confirm("미저장 앱 내용을 버리고 파일을 다시 읽을까요? 필요하면 먼저 저장해 양쪽 내용을 보존하세요.")) return; LoadRecord(); })); DockPanel.SetDock(title, Dock.Top); editor.Children.Add(title); editor.Children.Add(record); columns.Children.Add(editor);
        var scroll = new ScrollViewer { Content = side, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(scroll, 2); columns.Children.Add(scroll); Grid.SetRow(columns, 2); grid.Children.Add(columns); Content = grid;
        record.TextChanged += (_, _) => { dirty = record.Text != saved; note.Text = dirty ? "미저장 변경 있음 · Ctrl+S 저장" : "저장됨"; UpdateLines(); };
        record.SizeChanged += (_, _) => UpdateLines(); record.LayoutUpdated += (_, _) => UpdateLines();
        KeyDown += (_, e) => { if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { UI.Guard(SaveAll); e.Handled = true; } };
        Closing += (_, e) => { if (!dirty && !metadataDirty) return; var answer = MessageBox.Show("기록과 업무정보를 저장하고 닫을까요?", "미저장 내용", MessageBoxButton.YesNoCancel); if (answer == MessageBoxResult.Cancel) e.Cancel = true; else if (answer == MessageBoxResult.Yes) { try { SaveAll(); } catch (Exception ex) { MessageBox.Show(ex.Message); e.Cancel = true; } } };
        watcher.Tick += (_, _) => UI.Guard(() =>
        {
            string path = Path.Combine(item.Folder, WorkText.FileName); if (Disk.Hash(path) == basis) return;
            if (dirty) note.Text = "외부 변경도 있습니다 · 저장하면 양쪽을 보존하며 충돌을 알립니다."; else LoadRecord();
        }); watcher.Start(); Closed += (_, _) => watcher.Stop();
        LoadRecord(); BuildSide();
    }
    private void UpdateLines() { int a = record.GetFirstVisibleLineIndex(), b = record.GetLastVisibleLineIndex(); visible.Text = a >= 0 && b >= a ? $" · 화면 {b - a + 1}줄" : ""; }
    private void LoadRecord() { saved = WorkText.Load(item.Folder); basis = Disk.Hash(Path.Combine(item.Folder, WorkText.FileName)); record.Text = saved; dirty = false; note.Text = "저장됨 · " + item.Folder; }
    private void SaveRecord() { basis = store.SaveText(item, record.Text, basis); saved = record.Text; dirty = false; note.Text = "저장됨"; _ = main.Refresh(); }
    private void SaveAll() { if (metadataDirty) saveMetadata?.Invoke(); if (dirty) SaveRecord(); }
    private void BuildSide()
    {
        if (metadataDirty) saveMetadata?.Invoke();
        side.Children.Clear(); side.Children.Add(UI.Text(item.StateLabel + (item.CycleStart != Planning.Cycle(DateOnly.FromDateTime(DateTime.Today), store.Settings) ? " · 이전 주기" : ""), 16, true));
        side.Children.Add(UI.Button("폴더 이름 직접 변경", () => { var w = UI.Window("폴더 이름 변경", 650, 400); var p = new StackPanel { Margin = new Thickness(24) }; var edit = new TextBox { Text = item.FolderName }; p.Children.Add(UI.Text("요청일/내용 정정으로 폴더명은 자동 변경되지 않습니다. 아래 이름으로 명시 변경할 수 있습니다.")); p.Children.Add(edit); p.Children.Add(UI.Button("이 이름으로 변경", () => { if (dirty) SaveRecord(); store.Rename(item, edit.Text); basis = Disk.Hash(Path.Combine(item.Folder, WorkText.FileName)); w.Close(); _ = main.Refresh(); })); w.Content = p; w.Owner = this; w.ShowDialog(); }));
        if (item.Missing) side.Children.Add(UI.Button("원래 주기 폴더 재연결", () => { var p = UI.PickFolder("같은 요청의 폴더를 명시 선택", store.CycleFolder(item.CycleStart)); if (p is not null && UI.Confirm("선택한 폴더를 이 요청에 다시 연결할까요? 다른 업무 ID는 덮어쓰지 않습니다.")) { store.Reconnect(item, p); LoadRecord(); BuildSide(); _ = main.Refresh(); } }));
        var name = new TextBox { Text = item.Name }; NewWorkWindow.Field(side, "작업명 · 폴더는 자동 개명하지 않음", name);
        var date = new DatePicker { SelectedDate = item.RequestedOn.ToDateTime(TimeOnly.MinValue) }; NewWorkWindow.Field(side, "요청일", date);
        var due = new DatePicker { SelectedDate = item.DueOn?.ToDateTime(TimeOnly.MinValue) }; NewWorkWindow.Field(side, "마감 · 선택", due);
        var effort = new TextBox { Text = item.Effort.ToString(CultureInfo.InvariantCulture) }; NewWorkWindow.Field(side, "예상 소요일수", effort); var priority = NewWorkWindow.Priority(item.Priority); NewWorkWindow.Field(side, "중요도", priority);
        string metaBasis = Disk.Hash(Path.Combine(item.Folder, WorkStore.MetaName));
        saveMetadata = () =>
        {
            if (Disk.Hash(Path.Combine(item.Folder, WorkStore.MetaName)) != metaBasis) throw new IOException("업무정보가 외부에서 바뀌었습니다. 창을 다시 열어 확인해주세요. 입력한 내용은 이 창에 남아 있습니다.");
            if (date.SelectedDate is null || !decimal.TryParse(effort.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal days) || days <= 0) throw new ArgumentException("요청일과 예상 소요일수를 확인해주세요.");
            var next = Disk.Clone(item); next.Folder = item.Folder; next.MetadataHash = item.MetadataHash; next.Name = name.Text.Trim(); next.RequestedOn = DateOnly.FromDateTime(date.SelectedDate.Value); next.DueOn = due.SelectedDate is DateTime d ? DateOnly.FromDateTime(d) : null; next.Effort = days; next.Priority = priority.SelectedIndex;
            store.Save(next); item.MetadataHash = next.MetadataHash; item.Name = next.Name; item.RequestedOn = next.RequestedOn; item.DueOn = next.DueOn; item.Effort = next.Effort; item.Priority = next.Priority; metaBasis = Disk.Hash(Path.Combine(item.Folder, WorkStore.MetaName)); metadataDirty = false; note.Text = dirty ? "미저장 기록 있음" : "저장됨"; _ = main.Refresh();
        };
        void MetadataChanged() { metadataDirty = true; note.Text = "미저장 업무정보 있음 · Ctrl+S 저장"; }
        name.TextChanged += (_, _) => MetadataChanged(); effort.TextChanged += (_, _) => MetadataChanged(); date.SelectedDateChanged += (_, _) => MetadataChanged(); due.SelectedDateChanged += (_, _) => MetadataChanged(); priority.SelectionChanged += (_, _) => MetadataChanged();
        side.Children.Add(UI.Button("업무정보 저장", () => saveMetadata()));
        if (item.CompletionDateUnknown) side.Children.Add(UI.Text("완료일 확인 필요 · 탐색기 이동의 실제 시각은 알 수 없습니다."));
        if (item.State == WorkState.Completed)
        {
            var completedDate = new DatePicker { SelectedDate = item.CompletedAt?.LocalDateTime.Date }; NewWorkWindow.Field(side, "완료일 정정", completedDate);
            side.Children.Add(UI.Button("완료일 저장", () => { SaveAll(); if (completedDate.SelectedDate is not DateTime d) throw new ArgumentException("완료일을 선택해주세요."); item.CompletedAt = new DateTimeOffset(d); item.CompletionDateUnknown = false; store.Save(item); _ = main.Refresh(); BuildSide(); }));
        }
        if (item.State == WorkState.Review)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            side.Children.Add(UI.Text("이 요청을 수락한다면\n이번 주 " + Planning.Calculate(main.Items, today, Planning.WeekEnd(today), store.Settings, item).Message + "\n이번 달 " + Planning.Calculate(main.Items, today, Planning.MonthEnd(today), store.Settings, item).Message));
            side.Children.Add(UI.Button("수락 · 진행으로", () => State(WorkState.Active), true));
        }
        side.Children.Add(UI.Row(UI.Button("진행", () => State(WorkState.Active)), UI.Button("대기", () => State(WorkState.Waiting)), UI.Button("완료", () => State(WorkState.Completed), true)));
        side.Children.Add(UI.Button("취소 상태로 보관", () => { if (UI.Confirm("파일을 지우지 않고 취소 상태로 보관할까요?")) State(WorkState.Cancelled); }));
        side.Children.Add(UI.Text("요청사항", 16, true)); var info = new StackPanel(); foreach (var section in WorkText.Sections.Take(9)) { string text = WorkText.ReadSection(record.Text, section); if (text.Length > 0) { info.Children.Add(UI.Text(section, 12, true)); info.Children.Add(UI.Text(text)); } } side.Children.Add(UI.Card(info));
        side.Children.Add(UI.Text("제출항목", 16, true)); side.Children.Add(UI.Text("파일 연결은 외부 제출 완료가 아닙니다. 실제 파일 이름은 자동 변경하지 않습니다.", 11));
        foreach (var slot in item.Submissions)
        {
            var content = new StackPanel(); content.Children.Add(UI.Text(slot.Name, 14, true));
            content.Children.Add(UI.Row(UI.Button("이름 복사", () => Clipboard.SetText(slot.Name)), UI.Button("현재 파일 지정", () => { SaveAll(); string? p = UI.PickFile("현재 작업폴더 안의 제출파일 지정", item.Folder); if (p is not null) { store.ConnectFile(item, slot, p); BuildSide(); } })));
            foreach (string file in slot.Files.ToList()) content.Children.Add(UI.Row(UI.Button(Path.GetFileName(file), () => UI.Open(store.Resolve(item, file))), UI.Button("해제", () => { SaveAll(); slot.Files.Remove(file); store.Save(item); BuildSide(); })));
            side.Children.Add(UI.Card(content));
        }
        side.Children.Add(UI.Row(UI.Button("지난 제출물 보기 / 복사", () => new PastFilesWindow(main, item).ShowDialog()), UI.Button("결과물 폴더", () => UI.Open(Path.Combine(item.Folder, "결과물")))));
        side.Children.Add(UI.Text("작업 중 캡처", 16, true)); side.Children.Add(UI.Text("선택 자료 · 제출항목 아님 · 클릭해서 미리보기", 11));
        var captures = new WrapPanel { AllowDrop = true, Focusable = true, MinHeight = 75, Background = new SolidColorBrush(Color.FromRgb(236, 239, 236)) };
        string captureFolder = Path.Combine(item.Folder, "작업캡처"); if (Directory.Exists(captureFolder)) foreach (var file in Directory.EnumerateFiles(captureFolder).Where(IsImage)) captures.Children.Add(UI.Button(Path.GetFileName(file), () => Preview(file)));
        captures.Drop += (_, e) => UI.Guard(() => { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) foreach (var p in paths.Where(IsImage)) store.CopyInto(item, p, "작업캡처"); BuildSide(); }); side.Children.Add(captures);
        void PasteImage()
        {
            if (!Clipboard.ContainsImage()) throw new ArgumentException("클립보드에 이미지가 없습니다. Snipaste에서 캡처를 복사해주세요.");
            Directory.CreateDirectory(captureFolder); string target = Path.Combine(captureFolder, $"캡처_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..5]}.png");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Clipboard.GetImage())); using (var stream = new FileStream(target, FileMode.CreateNew)) encoder.Save(stream); BuildSide();
        }
        captures.MouseDown += (_, _) => captures.Focus();
        captures.KeyDown += (_, e) => { if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control) { UI.Guard(PasteImage); e.Handled = true; } };
        side.Children.Add(UI.Row(UI.Button("이미지 붙여넣기", PasteImage), UI.Button("이미지 파일 추가", () => { var p = UI.PickFile("캡처 이미지 선택", item.Folder, "이미지|*.png;*.jpg;*.jpeg;*.bmp"); if (p is not null) { store.CopyInto(item, p, "작업캡처"); BuildSide(); } })));
    }
    private void State(WorkState state) { SaveAll(); store.ChangeState(item, state); basis = Disk.Hash(Path.Combine(item.Folder, WorkText.FileName)); BuildSide(); _ = main.Refresh(); }
    private static bool IsImage(string path) => new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(Path.GetExtension(path).ToLowerInvariant());
    private static void Preview(string path)
    {
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
        var window = UI.Window("캡처 미리보기 — " + Path.GetFileName(path), 1000, 760); window.Content = new ScrollViewer { Content = new Image { Source = bitmap, Stretch = Stretch.Uniform }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }; window.Show();
    }
}

public sealed class PastFilesWindow : Window
{
    public PastFilesWindow(MainWindow main, WorkItem current)
    {
        var store = main.Store!; Title = "지난 제출물 — 원본 보기 / 이번 작업으로 복사"; Width = 850; Height = 600; Background = UI.Canvas; FontFamily = main.FontFamily;
        var panel = new DockPanel { Margin = new Thickness(20) }; var help = UI.Text("같은 유형의 지난 파일만 표시합니다. 복사하면 이번 작업폴더 안에서 수정할 수 있습니다."); DockPanel.SetDock(help, Dock.Top); panel.Children.Add(help);
        var files = main.Items.Where(x => x.Id != current.Id && current.TypeId is not null && x.TypeId == current.TypeId && x.State == WorkState.Completed && !x.Missing).OrderByDescending(x => x.RegisteredAt).SelectMany(x => x.Submissions.SelectMany(s => s.Files.Select(f => new PastFile($"{x.RequestedOn:yyyy.MM.dd} · {s.Name} · {Path.GetFileName(f)}", store.Resolve(x, f))))).ToList();
        var type = store.Settings.Types.FirstOrDefault(x => x.Id == current.TypeId);
        if (type is not null) files.AddRange(type.PastFiles.Select(f => new PastFile("직접 지정한 지난 파일 · " + Path.GetFileName(f), Path.IsPathFullyQualified(f) ? f : Path.Combine(store.Root, f))));
        var list = new ListBox { ItemsSource = files, DisplayMemberPath = "Label" }; var controls = UI.Row(UI.Button("선택한 원본 열기", () => { if (list.SelectedItem is PastFile p) UI.Open(p.Path); }), UI.Button("이번 작업 결과물로 복사", () => { if (list.SelectedItem is PastFile p) { string result = store.CopyInto(current, p.Path, "결과물"); UI.Open(Path.GetDirectoryName(result)!); MessageBox.Show("복사했습니다. 제출항목에서 현재 파일을 지정할 수 있습니다."); } })); DockPanel.SetDock(controls, Dock.Bottom); panel.Children.Add(controls); panel.Children.Add(list); Content = panel;
    }
    private sealed record PastFile(string Label, string Path);
}
