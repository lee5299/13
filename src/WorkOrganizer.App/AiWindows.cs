using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WorkOrganizer.Core;

namespace WorkOrganizer.App;

/// <summary>전송 전 로컬 검사 결과(AI_SCAN_DIALOG). 확인하면 가린 사본을 보내고, 취소하면 아무것도 보내지 않는다.</summary>
public sealed class ScanWindow : Window
{
    private bool accepted;
    private ScanWindow(Window owner, AiPolishRequest request)
    {
        Title = "AI 요청 전 검사 — PC 안에서 확인"; Width = 820; Height = 620; Background = UI.Canvas; FontFamily = owner.FontFamily; Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var dock = new DockPanel { Margin = new Thickness(22) };
        var top = new StackPanel(); top.Children.Add(UI.Text($"보내기 전에 {request.Findings.Count}곳을 찾았습니다", 20, true));
        top.Children.Add(UI.Text("이 검사는 PC 안에서만 했고 아직 아무것도 보내지 않았습니다. 확인을 누르면 아래 값들을 [[가림-…]] 표시로 바꾼 사본만 보내고, 공개 원본은 그대로 둡니다. 응답의 표시는 PC에서 원래 값으로 되돌립니다."));
        top.Children.Add(UI.Text("키·주민등록번호·전화번호 패턴만 찾습니다. 모든 민감 문구를 찾는다는 뜻은 아니므로 민감한 상세는 private에 적어주세요.", 12));
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
        var buttons = UI.Row(UI.Button("취소 · 보내지 않고 편집으로", () => Close()), UI.Button("확인 · 가린 사본으로 보내기", () => { accepted = true; Close(); }, true)); DockPanel.SetDock(buttons, Dock.Bottom); dock.Children.Add(buttons);
        dock.Children.Add(new ListBox { ItemsSource = request.Findings.Select(f => f.Display + " · " + f.Rule).ToList(), FontFamily = new FontFamily("Consolas, Malgun Gothic"), FontSize = 14 });
        Content = dock;
    }
    public static bool Confirm(Window owner, AiPolishRequest request) { var w = new ScanWindow(owner, request); w.ShowDialog(); return w.accepted; }
}

/// <summary>프로그램이 만든 좌우 비교(AI_RESULT, AI_RESULT_DIFF). 연결 스크롤·크기 조절·변경 강조. 적용을 누를 때만 반영한다.</summary>
public sealed class ResultWindow : Window
{
    private static readonly Brush ChangedLine = new SolidColorBrush(Color.FromRgb(255, 246, 214));
    private static readonly Brush ChangedSpan = new SolidColorBrush(Color.FromRgb(250, 222, 140));
    private static readonly Brush AddedLine = new SolidColorBrush(Color.FromRgb(222, 242, 232));
    private static readonly Brush RemovedLine = new SolidColorBrush(Color.FromRgb(248, 225, 225));
    private bool apply;
    private bool syncing;
    private ResultWindow(Window owner, AiPolishResult result, string provider, AiMode mode)
    {
        Title = $"AI 수정안 비교 — {(mode == AiMode.Sentence ? "문장 다듬기" : "내용+구조 다듬기")} · {provider}"; Width = 1400; Height = 900; MinWidth = 900; MinHeight = 600; Background = UI.Canvas; FontFamily = owner.FontFamily; Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var dock = new DockPanel { Margin = new Thickness(20) };
        var top = new StackPanel();
        int changed = result.Diff.Count(d => d.Kind != DiffKind.Same);
        top.Children.Add(UI.Text($"왼쪽 현재 공개 정리 내용 · 오른쪽 AI 수정안 · 바뀐 줄 {changed}개", 18, true));
        top.Children.Add(UI.Text("비교는 이 프로그램이 직접 만들었습니다. 받은 요청 원문은 보내지도 바꾸지도 않습니다. 적용을 눌러야만 반영되고, 취소하면 기존 내용이 그대로 남습니다.", 12));
        if (result.Problems.Count > 0)
        {
            var warn = new StackPanel(); warn.Children.Add(UI.Text("가림 표시 복원 확인 필요", 14, true));
            foreach (string p in result.Problems) warn.Children.Add(UI.Text("· " + p, 12));
            top.Children.Add(UI.Card(warn));
        }
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
        var buttons = UI.Row(UI.Button("취소 · 적용하지 않음", () => Close()), UI.Button("적용", () =>
        {
            if (result.Problems.Count > 0 && !UI.Confirm("복원이 불확실한 부분이 있습니다. 오른쪽 내용 그대로 적용할까요? 남은 표시는 직접 고쳐야 합니다.")) return;
            apply = true; Close();
        }, true));
        DockPanel.SetDock(buttons, Dock.Bottom); dock.Children.Add(buttons);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 250 }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(8) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 250 });
        var left = Box(); var right = Box();
        foreach (var row in result.Diff)
        {
            left.Document.Blocks.Add(Line(row.Kind is DiffKind.Same ? null : row.Kind == DiffKind.Added ? null : row.Kind == DiffKind.Removed ? RemovedLine : ChangedLine, row.Left, row.Kind == DiffKind.Changed ? row.LeftPrefix : -1, row.LeftSuffix, row.Kind == DiffKind.Added));
            right.Document.Blocks.Add(Line(row.Kind is DiffKind.Same ? null : row.Kind == DiffKind.Removed ? null : row.Kind == DiffKind.Added ? AddedLine : ChangedLine, row.Right, row.Kind == DiffKind.Changed ? row.RightPrefix : -1, row.RightSuffix, row.Kind == DiffKind.Removed));
        }
        grid.Children.Add(Labeled("현재 내용", left));
        var splitter = new GridSplitter { Width = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch, Background = UI.Line, ResizeDirection = GridResizeDirection.Columns }; Grid.SetColumn(splitter, 1); grid.Children.Add(splitter);
        var r = Labeled("AI 수정안 (가린 값은 PC에서 복원)", right); Grid.SetColumn(r, 2); grid.Children.Add(r);
        left.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, e) => Sync(right, e)));
        right.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, e) => Sync(left, e)));
        dock.Children.Add(grid); Content = dock;
    }
    private void Sync(RichTextBox other, ScrollChangedEventArgs e)
    {
        if (syncing || (e.VerticalChange == 0 && e.HorizontalChange == 0)) return;
        syncing = true; try { other.ScrollToVerticalOffset(e.VerticalOffset); other.ScrollToHorizontalOffset(e.HorizontalOffset); } finally { syncing = false; }
    }
    private static RichTextBox Box() => new() { IsReadOnly = true, FontFamily = new FontFamily("Malgun Gothic"), FontSize = 15, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Document = new FlowDocument { PagePadding = new Thickness(8), PageWidth = 2000 }, Background = Brushes.White, BorderBrush = UI.Line };
    private static DockPanel Labeled(string label, UIElement child) { var d = new DockPanel(); var t = UI.Text(label, 14, true); DockPanel.SetDock(t, Dock.Top); d.Children.Add(t); d.Children.Add(child); return d; }
    private static Paragraph Line(Brush? background, string text, int prefix, int suffix, bool placeholder)
    {
        var p = new Paragraph { Margin = new Thickness(0), Background = background, LineHeight = 22 };
        if (placeholder) { p.Inlines.Add(new Run(" ") { Foreground = UI.Muted }); return p; }
        if (prefix >= 0 && prefix + suffix <= text.Length)
        {
            p.Inlines.Add(new Run(text[..prefix]));
            p.Inlines.Add(new Run(text[prefix..(text.Length - suffix)]) { Background = ChangedSpan });
            p.Inlines.Add(new Run(text[(text.Length - suffix)..]));
        }
        else p.Inlines.Add(new Run(text.Length == 0 ? " " : text));
        return p;
    }
    public static bool Ask(Window owner, AiPolishResult result, string provider, AiMode mode) { var w = new ResultWindow(owner, result, provider, mode); w.ShowDialog(); return w.apply; }
}

/// <summary>업무정리 암호 입력. private 잠금 해제·복구정보·백업파일에 같은 암호를 쓴다. 처음이면 새로 정한다. 암호는 저장하지 않는다.</summary>
public sealed class PassphraseWindow : Window
{
    public string? Passphrase { get; private set; }
    public PassphraseWindow(Window? owner, bool create, string? purpose = null)
    {
        Title = create ? "업무정리 암호 정하기" : "업무정리 암호"; Width = 640; Height = create ? 560 : 360; Background = UI.Canvas; FontFamily = new FontFamily("Malgun Gothic"); ResizeMode = ResizeMode.NoResize; WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner; if (owner is not null) Owner = owner;
        var p = new StackPanel { Margin = new Thickness(24) };
        p.Children.Add(UI.Text(create ? "업무정리 암호를 정합니다" : "업무정리 암호를 입력해주세요", 19, true));
        if (purpose is not null) p.Children.Add(UI.Text(purpose, 12));
        if (create)
        {
            p.Children.Add(UI.Text("이 암호 하나로 private 잠금 해제, 백업 복구정보, 백업파일(.wobak)을 모두 보호합니다. 암호는 PC·업무자료·백업 어디에도 저장하지 않으므로, 잃으면 private과 백업파일을 복구할 수 없습니다. 안전한 곳에 따로 적어 두세요."));
            p.Children.Add(UI.Text($"{PrivateVault.MinimumPassphraseLength}자 이상 · 나중에 설정에서 바꿀 수 있습니다", 12));
        }
        var first = new PasswordBox { Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(8), FontSize = 14 }; var second = new PasswordBox { Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(8), FontSize = 14 };
        p.Children.Add(first); if (create) { p.Children.Add(UI.Text("한 번 더 입력", 12)); p.Children.Add(second); }
        void Ok()
        {
            if (create && first.Password != second.Password) throw new ArgumentException("두 암호가 다릅니다.");
            if (create) PrivateVault.CheckNewPassphrase(first.Password);
            Passphrase = first.Password; DialogResult = true;
        }
        first.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter && !create) UI.Guard(Ok); };
        p.Children.Add(UI.Row(UI.Button("취소", () => DialogResult = false), UI.Button(create ? "암호 정하기" : "확인", Ok, true)));
        Content = p; Loaded += (_, _) => first.Focus();
    }
}

/// <summary>업무정리 암호 변경. 기존 private 파일은 다시 암호화하지 않고 새 암호로 열린다.</summary>
public sealed class ChangePasswordWindow : Window
{
    public string? NewPassword { get; private set; }
    public ChangePasswordWindow(Window owner, string control)
    {
        Title = "업무정리 암호 변경"; Width = 660; Height = 600; Background = UI.Canvas; FontFamily = new FontFamily("Malgun Gothic"); ResizeMode = ResizeMode.NoResize; Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var p = new StackPanel { Margin = new Thickness(24) };
        p.Children.Add(UI.Text("업무정리 암호 변경", 19, true));
        p.Children.Add(UI.Text("· private 파일: 그대로 두고 새 암호로 열립니다. 옛 암호로는 더 이상 열리지 않습니다.\n· 백업 복구정보: 옛 암호로 잠근 파일은 버리고 새 암호로 다시 저장해주세요(변경 뒤 바로 안내).\n· 이미 만든 백업파일(.wobak): 만들 때의 암호가 그대로 필요합니다. 앱이 고칠 수 없으니 새로 만들어 올리고 예전 파일은 지우는 것을 권합니다.", 12));
        PasswordBox Box() => new() { Margin = new Thickness(0, 4, 0, 8), Padding = new Thickness(8), FontSize = 14 };
        var old = Box(); var next = Box(); var again = Box();
        p.Children.Add(UI.Text("지금 암호", 12)); p.Children.Add(old); p.Children.Add(UI.Text("새 암호", 12)); p.Children.Add(next); p.Children.Add(UI.Text("새 암호 한 번 더", 12)); p.Children.Add(again);
        p.Children.Add(UI.Row(UI.Button("취소", () => DialogResult = false), UI.Button("암호 바꾸기", () =>
        {
            if (next.Password != again.Password) throw new ArgumentException("새 암호 두 칸이 다릅니다.");
            PrivateVault.ChangePassphrase(control, old.Password, next.Password);
            NewPassword = next.Password; DialogResult = true;
        }, true)));
        Content = p; Loaded += (_, _) => old.Focus();
    }
}
