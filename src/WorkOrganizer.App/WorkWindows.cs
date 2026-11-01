using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        body.Children.Add(UI.Text("아래 두 칸은 공개 기본내용(AI가 읽을 수 있는 영역)에 저장됩니다. 명령어·구조 명칭·민감한 상세는 만든 뒤 상세창의 private에 적어주세요.", 12));
        var original = Multi(90); Field(body, "받은 요청 원문 · 공개", original); var understood = Multi(65); Field(body, "본인이 이해한 요청 · 공개", understood);
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
            // 업무기록.txt 기록 양식을 골랐으면 private 상세에 넣으므로 잠금 해제가 필요하다. 잠금 해제 중이면 빈 private 문서도 함께 만든다.
            bool recordTemplate = store.Settings.CommonTemplates.Concat(type?.Templates ?? []).Any(x => x.Selected && Path.GetFileName(x.Path).Equals(WorkText.LegacyFileName, StringComparison.OrdinalIgnoreCase));
            var vault = recordTemplate ? main.EnsurePrivateUnlocked(this) : main.Vault;
            if (recordTemplate && vault is null) return;
            Created = store.Create(name.Text, original.Text, understood.Text, DateOnly.FromDateTime(requested.SelectedDate.Value), days, priority.SelectedIndex, due.SelectedDate is DateTime d ? DateOnly.FromDateTime(d) : null, type, occurrence, existing, requestKey, vault: vault);
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
    // 상단 공개 입력부(DOC_PUBLIC): AI가 읽을 수 있는 영역. 인터넷 게시를 뜻하지 않는다.
    private readonly TextBox publicBox = new() { AcceptsReturn = true, AcceptsTab = true, FontFamily = new FontFamily("Malgun Gothic"), FontSize = 15, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 90 };
    // 중간의 넓은 private 입력부(DOC_PRIVATE): 명령어·구조명·민감 상세. 디스크에는 암호문만 저장한다.
    private readonly TextBox privateBox = new() { AcceptsReturn = true, AcceptsTab = true, FontFamily = new FontFamily("Consolas, Malgun Gothic"), FontSize = 15, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 160 };
    private readonly Border privateLocked = new() { Background = new SolidColorBrush(Color.FromRgb(236, 239, 236)), BorderBrush = UI.Line, BorderThickness = new Thickness(1), MinHeight = 160 };
    private readonly TextBlock note = UI.Text("");
    private readonly TextBlock visible = UI.Text("");
    private readonly TextBlock aiStatus = UI.Text("", 12);
    private readonly StackPanel legacyBanner = new();
    private readonly StackPanel side = new();
    private readonly Button sentenceButton, structureButton, cancelAiButton;
    private string publicBasis = "", publicSaved = "", privateBasis = "", privateSaved = "";
    private bool publicDirty, privateDirty, privateLoaded;
    private bool metadataDirty;
    private Action? saveMetadata;
    private System.Threading.CancellationTokenSource? aiCancel;
    private readonly System.Windows.Threading.DispatcherTimer watcher = new() { Interval = TimeSpan.FromSeconds(3) };
    public bool PrivateDirty => privateDirty;
    public bool AnyDirty => publicDirty || privateDirty || metadataDirty;
    public string ItemId => item.Id;
    public DetailWindow(MainWindow main, WorkItem item)
    {
        this.main = main; store = main.Store!; this.item = item;
        Title = item.Name + " — 작업 상세"; Width = 1280; Height = 900; MinWidth = 950; MinHeight = 650; Background = UI.Canvas; FontFamily = main.FontFamily;
        var grid = new Grid { Margin = new Thickness(20) }; grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var heading = new DockPanel(); var buttons = UI.Row(UI.Button("폴더 열기", () => UI.Open(item.Folder)), UI.Button("공개기록 외부 열기", () => { SaveAll(); UI.Open(Path.Combine(item.Folder, WorkText.FileName)); }), UI.Button("최대화 / 복원", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized), UI.Button("저장", SaveAll, true)); DockPanel.SetDock(buttons, Dock.Right); heading.Children.Add(buttons); heading.Children.Add(UI.Text(item.Name, 23, true)); grid.Children.Add(heading);
        Grid.SetRow(note, 1); grid.Children.Add(note);
        Grid.SetRow(legacyBanner, 2); grid.Children.Add(legacyBanner);
        var columns = new Grid(); columns.ColumnDefinitions.Add(new() { Width = new GridLength(7, GridUnitType.Star) }); columns.ColumnDefinitions.Add(new() { Width = new GridLength(18) }); columns.ColumnDefinitions.Add(new() { Width = new GridLength(3, GridUnitType.Star), MinWidth = 285 });
        var font = new ComboBox { ItemsSource = new[] { 14, 15, 16, 18, 20 }, SelectedItem = 15, Width = 55, Margin = new Thickness(8, 3, 8, 3), ToolTip = "기록 글자 크기" }; font.SelectionChanged += (_, _) => { if (font.SelectedItem is int size) { privateBox.FontSize = size; publicBox.FontSize = size; } };
        // 편집 열: 공개(상단) · 크기 조절 막대 · private(중간, 넓게)
        var editor = new Grid(); editor.RowDefinitions.Add(new() { Height = GridLength.Auto }); editor.RowDefinitions.Add(new() { Height = new GridLength(2, GridUnitType.Star), MinHeight = 120 }); editor.RowDefinitions.Add(new() { Height = new GridLength(8) }); editor.RowDefinitions.Add(new() { Height = new GridLength(5, GridUnitType.Star), MinHeight = 220 });
        sentenceButton = UI.Button("문장 다듬기(오타 포함)", () => _ = Polish(AiMode.Sentence)); structureButton = UI.Button("내용+구조 다듬기", () => _ = Polish(AiMode.Structure)); cancelAiButton = UI.Button("요청 중단", () => aiCancel?.Cancel()); cancelAiButton.IsEnabled = false;
        var publicTitle = new DockPanel(); var aiButtons = UI.Row(sentenceButton, structureButton, cancelAiButton); DockPanel.SetDock(aiButtons, Dock.Right); publicTitle.Children.Add(aiButtons);
        var publicLabel = new StackPanel(); publicLabel.Children.Add(UI.Text("공개 기본내용 · 요청 목적·요구·맥락", 15, true)); publicLabel.Children.Add(UI.Text("AI가 읽을 수 있는 영역입니다(인터넷 게시 아님). 명령어·구조 명칭·민감한 상세는 아래 private에 적어주세요.", 11)); publicLabel.Children.Add(aiStatus); publicTitle.Children.Add(publicLabel);
        editor.Children.Add(publicTitle); Grid.SetRow(publicBox, 1); editor.Children.Add(publicBox);
        var splitter = new GridSplitter { Height = 8, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center, Background = UI.Line, ResizeDirection = GridResizeDirection.Rows, ToolTip = "끌어서 공개/private 영역 크기 조절" }; Grid.SetRow(splitter, 2); editor.Children.Add(splitter);
        var privateTitle = UI.Row(UI.Text("private 상세 · 명령어·구조명·민감 상세", 15, true), visible, font, UI.Button("잠금 해제", UnlockPrivate), UI.Button("private 잠그기", () => main.TryLockPrivate("private 잠금")), UI.Button("다른 곳 변경 다시 읽기", () => { if ((publicDirty || privateDirty) && !UI.Confirm("미저장 앱 내용을 버리고 파일을 다시 읽을까요? 필요하면 먼저 저장해 양쪽 내용을 보존하세요.")) return; LoadPublic(); LoadPrivate(); }));
        var privateDock = new DockPanel(); DockPanel.SetDock(privateTitle, Dock.Top); privateDock.Children.Add(privateTitle); var privateCell = new Grid(); privateDock.Children.Add(privateCell); Grid.SetRow(privateDock, 3); editor.Children.Add(privateDock);
        var lockedText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; lockedText.Children.Add(UI.Text("private이 잠겨 있습니다", 16, true)); lockedText.Children.Add(UI.Text("디스크에는 암호문만 있으며 AI 연결에는 전달하지 않습니다. 보려면 업무정리 암호로 잠금을 해제하세요.")); lockedText.Children.Add(UI.Button("잠금 해제", UnlockPrivate, true)); privateLocked.Child = lockedText;
        privateCell.Children.Add(privateBox); privateCell.Children.Add(privateLocked);
        columns.Children.Add(editor);
        var scroll = new ScrollViewer { Content = side, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(scroll, 2); columns.Children.Add(scroll); Grid.SetRow(columns, 3); grid.Children.Add(columns); Content = grid;
        publicBox.TextChanged += (_, _) => { publicDirty = publicBox.Text != publicSaved; UpdateNote(); };
        privateBox.TextChanged += (_, _) => { if (privateLoaded) privateDirty = privateBox.Text != privateSaved; UpdateNote(); UpdateLines(); };
        privateBox.SizeChanged += (_, _) => UpdateLines(); privateBox.LayoutUpdated += (_, _) => UpdateLines();
        KeyDown += (_, e) => { if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { UI.Guard(SaveAll); e.Handled = true; } };
        Closing += (_, e) =>
        {
            if (aiCancel is not null) { if (!UI.Confirm("AI 요청이 진행 중입니다. 중단하고 닫을까요? 문서는 바뀌지 않습니다.")) { e.Cancel = true; return; } aiCancel.Cancel(); }
            if (!AnyDirty) return; var answer = MessageBox.Show("공개 내용·private 상세·업무정보를 저장하고 닫을까요?\n예: 저장 후 닫기 · 아니요: 버리고 닫기 · 취소: 계속 편집", "미저장 내용", MessageBoxButton.YesNoCancel); if (answer == MessageBoxResult.Cancel) e.Cancel = true; else if (answer == MessageBoxResult.Yes) { try { SaveAll(); } catch (Exception ex) { MessageBox.Show(ex.Message); e.Cancel = true; } }
        };
        watcher.Tick += (_, _) => UI.Guard(() =>
        {
            if (item.Missing || !Directory.Exists(item.Folder)) return;
            if (Disk.Hash(Path.Combine(item.Folder, WorkText.FileName)) != publicBasis) { if (publicDirty) note.Text = "공개기록에 외부 변경도 있습니다 · 저장하면 양쪽을 보존하며 충돌을 알립니다."; else LoadPublic(); }
            if (privateLoaded && WorkStore.PrivateHash(item) != privateBasis) { if (privateDirty) note.Text = "private 상세가 다른 곳에서 바뀌었습니다 · 저장하면 양쪽을 암호문으로 보존합니다."; else LoadPrivate(); }
        }); watcher.Start();
        Action changed = () => Dispatcher.Invoke(() => UI.Guard(LoadPrivate));
        main.PrivateStateChanged += changed; Closed += (_, _) => { watcher.Stop(); main.PrivateStateChanged -= changed; aiCancel?.Cancel(); };
        LoadPublic(); LoadPrivate(); BuildLegacyBanner(); BuildSide();
    }
    private void UpdateNote()
    {
        var parts = new List<string>();
        if (publicDirty) parts.Add("공개 미저장"); if (privateDirty) parts.Add("private 미저장"); if (metadataDirty) parts.Add("업무정보 미저장");
        note.Text = parts.Count == 0 ? "저장됨 · " + item.Folder : string.Join(" · ", parts) + " · Ctrl+S 저장";
    }
    private void UpdateLines() { if (!privateBox.IsVisible) { visible.Text = ""; return; } int a = privateBox.GetFirstVisibleLineIndex(), b = privateBox.GetLastVisibleLineIndex(); visible.Text = a >= 0 && b >= a ? $" · 화면 {b - a + 1}줄" : ""; }
    private void LoadPublic() { publicSaved = WorkText.Load(item.Folder); publicBasis = Disk.Hash(Path.Combine(item.Folder, WorkText.FileName)); if (publicSaved.Length == 0 && item.Unclassified) publicSaved = WorkText.Build("", ""); publicBox.Text = publicSaved; publicDirty = false; UpdateNote(); }
    private void LoadPrivate()
    {
        var vault = main.Vault;
        if (vault is null || vault.IsLocked)
        {
            if (privateDirty) return; // 잠그기 전에 저장/취소를 묻는다. 여기서는 미저장 입력을 지우지 않는다.
            privateLoaded = false; privateSaved = ""; privateBox.Text = ""; privateDirty = false; privateBox.Visibility = Visibility.Collapsed; privateLocked.Visibility = Visibility.Visible; UpdateNote(); return;
        }
        if (privateDirty && privateLoaded) return;
        privateSaved = store.LoadPrivate(item, vault).Replace("\r\n", "\n").Replace("\n", Environment.NewLine); privateBasis = WorkStore.PrivateHash(item);
        privateLoaded = false; privateBox.Text = privateSaved; privateLoaded = true; privateDirty = false;
        privateBox.Visibility = Visibility.Visible; privateLocked.Visibility = Visibility.Collapsed; UpdateNote();
    }
    private void UnlockPrivate() { if (main.EnsurePrivateUnlocked(this) is not null) LoadPrivate(); }
    private void SavePublic() { publicBasis = store.SaveText(item, publicBox.Text, publicBasis); publicSaved = publicBox.Text; publicDirty = false; UpdateNote(); BuildLegacyBanner(); _ = main.Refresh(); }
    public void SavePrivateNow()
    {
        if (!privateDirty) return;
        var vault = main.Vault ?? throw new PrivateLockedException();
        privateBasis = store.SavePrivate(item, vault, privateBox.Text, privateBasis); privateSaved = privateBox.Text; privateDirty = false; UpdateNote();
    }
    public void SaveAll() { if (metadataDirty) saveMetadata?.Invoke(); if (publicDirty) SavePublic(); SavePrivateNow(); }
    private void BuildLegacyBanner()
    {
        legacyBanner.Children.Clear();
        var legacy = WorkText.LegacyFiles(item.Folder); if (legacy.Count == 0) return;
        var p = new StackPanel();
        p.Children.Add(UI.Text("이전 단일기록(공개/private 미분류)이 있습니다", 14, true));
        p.Children.Add(UI.Text(File.Exists(Path.Combine(item.Folder, WorkText.FileName))
            ? "공개 내용은 나눠 저장했습니다. 기존 평문 기록이 폴더에 남아 있어 외부 폴더 AI 작업은 시작할 수 없습니다. 확인 후 private 보관본으로 옮겨주세요."
            : "자동으로 공개/AI 전송 대상에 넣지 않습니다. 기존 기록을 보면서 위 공개 영역과 아래 private 영역에 직접 나눠 옮긴 뒤 저장하세요. 저장 전에는 AI 다듬기를 쓸 수 없습니다.", 12));
        p.Children.Add(UI.Row(UI.Button("기존 기록 보기(읽기 전용)", () =>
        {
            var w = UI.Window("기존 단일기록 — 읽기 전용", 900, 750); w.Owner = this; var tabs = new TabControl { Margin = new Thickness(12) };
            foreach (string f in legacy) tabs.Items.Add(new TabItem { Header = Path.GetFileName(f), Content = new TextBox { Text = File.ReadAllText(f), IsReadOnly = true, AcceptsReturn = true, FontFamily = new FontFamily("Consolas, Malgun Gothic"), FontSize = 14, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto } });
            w.Content = tabs; w.Show();
        }), UI.Button("기존 기록을 private 보관본으로 옮기기", () =>
        {
            SaveAll();
            if (!File.Exists(Path.Combine(item.Folder, WorkText.FileName))) throw new InvalidOperationException("먼저 공개 내용을 나눠 저장해주세요.");
            var vault = main.EnsurePrivateUnlocked(this); if (vault is null) return;
            if (!UI.Confirm("기존 단일기록을 private 암호문 보관본으로 옮길까요?\n암호문을 다시 확인한 뒤에만 평문 파일을 지웁니다. 보관본은 private 영역의 ‘기존 기록 보관본 보기’에서 볼 수 있습니다.")) return;
            int n = store.ArchiveLegacy(item, vault); MessageBox.Show($"{n}개 파일을 private 보관본으로 옮겼습니다."); BuildLegacyBanner(); BuildSide(); _ = main.Refresh();
        })));
        legacyBanner.Children.Add(UI.Card(p));
    }
    /// <summary>클릭형 AI 다듬기(FLOW_AI). 버튼을 눌렀을 때만 시작한다(AI_TRIGGER).</summary>
    private async Task Polish(AiMode mode)
    {
        if (aiCancel is not null) return;
        try
        {
            if (item.Unclassified || !File.Exists(Path.Combine(item.Folder, WorkText.FileName))) throw new InvalidOperationException("공개 내용이 아직 저장되지 않았습니다. 이전 단일기록은 자동으로 AI에 보내지 않습니다. 공개 영역을 나눠 저장한 뒤 다시 눌러주세요.");
            if (ExternalAiSession.IsActive(store)) throw new InvalidOperationException("외부 AI 작업 잠금 중에는 앱 안 AI 요청을 시작하지 않습니다.");
            AiPolishRequest request;
            while (true)
            {
                request = AiPolishRequest.Prepare(publicBox.Text, mode, store.Settings.PhoneExceptions);
                if (request.Findings.Count > 0 && !ScanWindow.Confirm(this, request)) { aiStatus.Text = "전송 취소 · 아무것도 보내지 않았습니다."; return; }
                if (request.StillMatches(publicBox.Text)) break;
                MessageBox.Show("검사한 뒤 공개 내용이 바뀌어 다시 검사합니다. 이전 확인은 새 내용에 쓰지 않습니다.");
            }
            var connector = new CliAiConnector(store.Settings.AiProvider, store.Settings.AiExecutablePath, TimeSpan.FromSeconds(Math.Max(15, store.Settings.AiTimeoutSeconds)), store.Settings.AiModel);
            if (connector.HasLocalTools)
            {
                if (!UI.Confirm($"{connector.Name}는 PC 명령 도구를 가진 연결이라, 요청하는 동안 private 키를 메모리에서 지웁니다(private 잠금). 끝난 뒤 다시 잠금을 해제해야 합니다. 계속할까요?")) return;
                if (!main.TryLockPrivate($"{connector.Name} 요청")) return;
            }
            aiCancel = new System.Threading.CancellationTokenSource(); sentenceButton.IsEnabled = structureButton.IsEnabled = false; cancelAiButton.IsEnabled = true;
            aiStatus.Text = $"{connector.Name}에 공개 정리 내용 요청 중 · 가린 값 {request.Masked.Map.Count}개 · 받은 요청 원문과 private은 보내지 않음";
            var outcome = await connector.Send(request.Prompt, aiCancel.Token);
            if (!outcome.Success) { aiStatus.Text = "요청 실패 · 문서는 바뀌지 않았습니다."; MessageBox.Show(outcome.Status + "\n\n다시 시도하려면 다듬기 버튼을 다시 눌러주세요.", "AI 요청 실패"); return; }
            var result = request.Receive(outcome.Text);
            aiStatus.Text = $"{connector.Name} 응답 받음 · 비교 화면에서 적용/취소를 선택하세요.";
            if (!ResultWindow.Ask(this, result, connector.Name, mode)) { aiStatus.Text = "적용하지 않았습니다 · 기존 내용 유지"; return; }
            if (!request.StillMatches(publicBox.Text)) { MessageBox.Show("요청을 보낸 뒤 공개 내용이 바뀌었습니다. 자동으로 덮어쓰지 않습니다. 현재 내용을 저장한 뒤 다시 다듬어 주세요.", "적용하지 않음"); aiStatus.Text = "요청 중 편집으로 적용 보류"; return; }
            publicBox.Text = request.Apply(publicBox.Text, result).Replace("\n", Environment.NewLine);
            SavePublic(); aiStatus.Text = "AI 수정안을 적용해 저장했습니다 · 받은 요청 원문은 그대로입니다.";
        }
        catch (Exception ex) { aiStatus.Text = "AI 요청을 시작하지 않았거나 중단했습니다 · 문서는 바뀌지 않았습니다."; MessageBox.Show(ex.Message, "확인이 필요합니다", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally
        {
            aiCancel?.Dispose(); aiCancel = null; sentenceButton.IsEnabled = structureButton.IsEnabled = true; cancelAiButton.IsEnabled = false;
        }
    }
    private void BuildSide()
    {
        if (metadataDirty) saveMetadata?.Invoke();
        side.Children.Clear();
        if (File.Exists(Path.Combine(item.Folder, WorkText.PrivateFolder, WorkText.LegacyArchiveName)))
            side.Children.Add(UI.Button("기존 기록 보관본 보기(private)", () =>
            {
                var vault = main.EnsurePrivateUnlocked(this); if (vault is null) return;
                var w = UI.Window("기존 단일기록 보관본 — private · 읽기 전용", 900, 750); w.Owner = this;
                w.Content = new TextBox { Text = store.LoadPrivateArchive(item, vault).Replace("\n", Environment.NewLine), IsReadOnly = true, AcceptsReturn = true, FontFamily = new FontFamily("Consolas, Malgun Gothic"), FontSize = 14, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12) };
                Action locked = () => w.Dispatcher.Invoke(() => { if (main.Vault is null) w.Close(); }); main.PrivateStateChanged += locked; w.Closed += (_, _) => main.PrivateStateChanged -= locked; w.Show();
            }));
        side.Children.Add(UI.Text(item.StateLabel + (item.CycleStart != Planning.Cycle(DateOnly.FromDateTime(DateTime.Today), store.Settings) ? " · 이전 주기" : ""), 16, true));
        side.Children.Add(UI.Button("폴더 이름 직접 변경", () => { var w = UI.Window("폴더 이름 변경", 650, 400); var p = new StackPanel { Margin = new Thickness(24) }; var edit = new TextBox { Text = item.FolderName }; p.Children.Add(UI.Text("요청일/내용 정정으로 폴더명은 자동 변경되지 않습니다. 아래 이름으로 명시 변경할 수 있습니다.")); p.Children.Add(edit); p.Children.Add(UI.Button("이 이름으로 변경", () => { SaveAll(); store.Rename(item, edit.Text); publicBasis = Disk.Hash(Path.Combine(item.Folder, WorkText.FileName)); privateBasis = WorkStore.PrivateHash(item); w.Close(); _ = main.Refresh(); })); w.Content = p; w.Owner = this; w.ShowDialog(); }));
        if (item.Missing) side.Children.Add(UI.Button("원래 주기 폴더 재연결", () => { var p = UI.PickFolder("같은 요청의 폴더를 명시 선택", store.CycleFolder(item.CycleStart)); if (p is not null && UI.Confirm("선택한 폴더를 이 요청에 다시 연결할까요? 다른 업무 ID는 덮어쓰지 않습니다.")) { store.Reconnect(item, p); LoadPublic(); LoadPrivate(); BuildSide(); _ = main.Refresh(); } }));
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
            store.Save(next); item.MetadataHash = next.MetadataHash; item.Name = next.Name; item.RequestedOn = next.RequestedOn; item.DueOn = next.DueOn; item.Effort = next.Effort; item.Priority = next.Priority; metaBasis = Disk.Hash(Path.Combine(item.Folder, WorkStore.MetaName)); metadataDirty = false; UpdateNote(); _ = main.Refresh();
        };
        void MetadataChanged() { metadataDirty = true; UpdateNote(); }
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
        side.Children.Add(UI.Text("요청사항", 16, true)); var info = new StackPanel(); foreach (var section in WorkText.PublicSections) { string text = WorkText.ReadSection(publicBox.Text, section); if (text.Length > 0) { info.Children.Add(UI.Text(section, 12, true)); info.Children.Add(UI.Text(text)); } } side.Children.Add(UI.Card(info));
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
    private void State(WorkState state) { SaveAll(); store.ChangeState(item, state); publicBasis = Disk.Hash(Path.Combine(item.Folder, WorkText.FileName)); privateBasis = WorkStore.PrivateHash(item); BuildSide(); _ = main.Refresh(); }
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
