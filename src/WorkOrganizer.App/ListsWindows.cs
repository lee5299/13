using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WorkOrganizer.Core;

namespace WorkOrganizer.App;

public sealed class CompletedWindow : Window
{
    private readonly MainWindow main;
    private readonly TextBox search = new() { Width = 300 };
    private readonly ComboBox basis = new() { ItemsSource = new[] { "완료일", "요청일" }, SelectedIndex = 0, Width = 100, Margin = new Thickness(5) };
    private readonly ComboBox group = new() { ItemsSource = new[] { "주별", "월별" }, SelectedIndex = 0, Width = 90, Margin = new Thickness(5) };
    private readonly DatePicker from = new() { Width = 130 }, to = new() { Width = 130 };
    private readonly TextBlock summary = UI.Text("");
    private readonly StackPanel detail = new();
    private readonly DataGrid list = UI.Grid(("완료일", "Completed", 110), ("작업 폴더", "Folder", 3), ("요청 내용", "Request", 3), ("요청일", "Requested", 110), ("예상량", "Effort", 70));
    public CompletedWindow(MainWindow main)
    {
        this.main = main; Title = "완료 작업 — 업무 정리"; Width = 1380; Height = 900; MinWidth = 1000; MinHeight = 650; Background = UI.Canvas; FontFamily = main.FontFamily;
        var outer = new DockPanel { Margin = new Thickness(24) }; var head = new StackPanel(); head.Children.Add(UI.Text("완료 작업", 27, true)); head.Children.Add(summary);
        var presets = UI.Row(UI.Button("이번 주", () => Range(0)), UI.Button("지난 주", () => Range(1)), UI.Button("이번 달", () => Range(2)), UI.Button("지난 달", () => Range(3)), UI.Button("이번 주기", () => Range(4)));
        head.Children.Add(UI.Row(search, basis, from, UI.Text(" ~ "), to, group)); head.Children.Add(presets); DockPanel.SetDock(head, Dock.Top); outer.Children.Add(head);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(7, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(3, GridUnitType.Star) }); grid.Children.Add(UI.Card(list)); var side = UI.Card(new ScrollViewer { Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Grid.SetColumn(side, 1); grid.Children.Add(side); outer.Children.Add(grid); Content = outer;
        list.SelectionChanged += (_, _) => Select(); search.TextChanged += (_, _) => Refresh(); basis.SelectionChanged += (_, _) => Refresh(); group.SelectionChanged += (_, _) => Refresh(); from.SelectedDateChanged += (_, _) => Refresh(); to.SelectedDateChanged += (_, _) => Refresh();
        Range(4);
    }
    private void Range(int preset)
    {
        var today = DateOnly.FromDateTime(DateTime.Today); var month = new DateOnly(today.Year, today.Month, 1);
        (DateOnly a, DateOnly b) = preset switch { 0 => (Planning.WeekEnd(today).AddDays(-6), Planning.WeekEnd(today)), 1 => (Planning.WeekEnd(today).AddDays(-13), Planning.WeekEnd(today).AddDays(-7)), 2 => (month, Planning.MonthEnd(month)), 3 => (month.AddMonths(-1), month.AddDays(-1)), _ => (Planning.Cycle(today, main.Store!.Settings), today) };
        from.SelectedDate = a.ToDateTime(TimeOnly.MinValue); to.SelectedDate = b.ToDateTime(TimeOnly.MinValue); Refresh();
    }
    private void Refresh()
    {
        if (from.SelectedDate is not DateTime a || to.SelectedDate is not DateTime b) return;
        if (a > b) { summary.Text = "시작일이 종료일보다 늦습니다."; return; }
        string q = search.Text.Trim(); var completed = main.Items.Where(x => x.State == WorkState.Completed).ToList();
        DateOnly? Date(WorkItem x) => basis.SelectedIndex == 1 ? x.RequestedOn : x.CompletedAt is not null ? DateOnly.FromDateTime(x.CompletedAt.Value.LocalDateTime) : null;
        var selected = completed.Where(x => Date(x) is DateOnly d && d >= DateOnly.FromDateTime(a) && d <= DateOnly.FromDateTime(b)).Where(x => (x.FolderName + " " + x.Name + " " + (x.Missing ? "" : WorkText.ReadSection(WorkText.LoadForDisplay(x.Folder), "요청 원문") + " " + WorkText.ReadSection(WorkText.LoadForDisplay(x.Folder), "본인이 이해한 요청") + " " + WorkText.ReadSection(WorkText.LoadForDisplay(x.Folder), "누가"))).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        string Group(WorkItem x) { var d = Date(x)!.Value; return group.SelectedIndex == 1 ? d.ToString("yyyy년 MM월") : Planning.WeekEnd(d).AddDays(-6).ToString("yyyy.MM.dd") + " 시작 주"; }
        var totals = selected.GroupBy(Group).ToDictionary(g => g.Key, g => $"{g.Key} · {g.Count()}건 · 등록 예상량 {g.Sum(x => x.Effort):0.##}일");
        var rows = selected.OrderByDescending(x => Group(x), StringComparer.Ordinal).ThenByDescending(x => x.RegisteredAt).Select(x => new CompletedRow(x, totals[Group(x)])).ToList();
        var view = new ListCollectionView(rows); view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
        var f = new FrameworkElementFactory(typeof(TextBlock)); f.SetBinding(TextBlock.TextProperty, new Binding("Name")); f.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); f.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 14, 0, 8)); list.GroupStyle.Clear(); list.GroupStyle.Add(new GroupStyle { HeaderTemplate = new DataTemplate { VisualTree = f } }); list.ItemsSource = view;
        summary.Text = $"{basis.SelectedItem} 기준 · {rows.Count}건 · 등록 예상량 합계 {selected.Sum(x => x.Effort):0.##}일 (실제 투입시간 아님) · 완료일 확인 필요 {completed.Count(x => x.CompletionDateUnknown)}건";
        detail.Children.Clear(); detail.Children.Add(UI.Text("목록에서 작업을 선택하면 요청 내용과 결과물을 볼 수 있습니다."));
        if (completed.Any(x => x.CompletionDateUnknown)) detail.Children.Add(UI.Button("완료일 미확인 작업 보기", () => { var w = UI.Window("완료일 확인 필요", 700, 500); var p = new StackPanel { Margin = new Thickness(20) }; foreach (var x in completed.Where(x => x.CompletionDateUnknown)) p.Children.Add(UI.Button(x.FolderName, () => main.ShowDetail(x))); w.Content = new ScrollViewer { Content = p }; w.Show(); }));
    }
    private void Select()
    {
        if (list.SelectedItem is not CompletedRow row) return; var x = row.Item; detail.Children.Clear(); detail.Children.Add(UI.Text(x.Name, 22, true)); detail.Children.Add(UI.Text(x.Folder)); detail.Children.Add(UI.Text(x.Missing ? "폴더 위치 확인 필요" : x.Request)); detail.Children.Add(UI.Button("전체 작업 기록 열기", () => main.ShowDetail(x))); detail.Children.Add(UI.Button("폴더 열기", () => UI.Open(x.Folder)));
        foreach (var s in x.Submissions) { detail.Children.Add(UI.Text(s.Name, 14, true)); foreach (var file in s.Files) detail.Children.Add(UI.Button(System.IO.Path.GetFileName(file), () => UI.Open(main.Store!.Resolve(x, file)))); }
    }
    private sealed record CompletedRow(WorkItem Item, string Group)
    {
        public string Completed => Item.CompletedAt?.LocalDateTime.ToString("yyyy.MM.dd") ?? "확인 필요";
        public string Folder => Item.FolderName;
        public string Request => Item.Missing ? "위치 확인 필요" : Item.Request.ReplaceLineEndings(" ");
        public string Requested => Item.RequestedOn.ToString("yyyy.MM.dd");
        public string Effort => $"{Item.Effort:0.##}일";
        public override string ToString() => Folder + "\n" + Request;
    }
}

public sealed class CalendarWindow : Window
{
    private readonly MainWindow main;
    private readonly Grid months = new();
    private readonly TextBlock yearLabel = UI.Text("", 24, true);
    private int year = DateTime.Today.Year;
    public CalendarWindow(MainWindow main)
    {
        this.main = main; Title = "1년 일정"; Width = 1250; Height = 900; Background = UI.Canvas; FontFamily = main.FontFamily;
        var outer = new DockPanel { Margin = new Thickness(24) }; var head = UI.Row(UI.Button("이전 해", () => { year--; Render(); }), yearLabel, UI.Button("다음 해", () => { year++; Render(); }), UI.Button("반복업무 직접 등록", () => EditType(null)));
        DockPanel.SetDock(head, Dock.Top); outer.Children.Add(head); for (int i = 0; i < 3; i++) months.ColumnDefinitions.Add(new()); for (int i = 0; i < 4; i++) months.RowDefinitions.Add(new()); outer.Children.Add(months); Content = outer; Render();
    }
    private void EditType(WorkType? type)
    {
        var settings = Disk.Clone(main.Store!.Settings); var existing = settings.Types.FirstOrDefault(x => x.Id == type?.Id); var draft = existing ?? new WorkType();
        if (new TypeWindow(main.Store, draft) { Owner = this }.ShowDialog() != true) return;
        if (existing is null) settings.Types.Add(draft); SettingsWindow.Materialize(main.Store, settings); main.Store.SaveSettings(settings); _ = main.Refresh(); Render();
    }
    private void Render()
    {
        yearLabel.Text = $"{year}년 일정"; months.Children.Clear();
        for (int m = 1; m <= 12; m++)
        {
            var content = new StackPanel(); content.Children.Add(UI.Text($"{m}월", 19, true));
            foreach (var f in Planning.Forecasts(main.Items, main.Store!.Settings, year, m))
            {
                var b = UI.Button(f.Name + "\n" + f.Description + $" · {f.From:MM.dd}~{f.To:MM.dd}" + (f.HasWork ? " · 등록됨" : ""), () =>
                {
                    var w = UI.Window(f.Name, 620, 480); var p = new StackPanel { Margin = new Thickness(24) }; p.Children.Add(UI.Text(f.Name, 22, true)); p.Children.Add(UI.Text(f.Description + $"\n{f.From:yyyy.MM.dd} ~ {f.To:yyyy.MM.dd}\n예상 일정이며 자동으로 작업폴더를 만들지 않습니다."));
                    var type = main.Store.Settings.Types.FirstOrDefault(x => x.Id == f.TypeId);
                    p.Children.Add(UI.Button(f.HasWork ? "이미 등록한 작업 열기" : "요청을 받아 새 작업 생성", () => { if (f.HasWork) { var existing = main.Items.FirstOrDefault(x => x.Occurrence == f.Occurrence && x.TypeId == f.TypeId && x.State != WorkState.Cancelled); if (existing is not null) main.ShowDetail(existing); } else main.NewWork(f); w.Close(); }));
                    p.Children.Add(UI.Button(type is null ? "반복업무로 등록" : "이후 반복/유형 설정 편집", () => { EditType(type); w.Close(); }));
                    if (type is not null && f.Occurrence is not null)
                    {
                        var occurrenceDate = new DatePicker { SelectedDate = (type.Repeat == RepeatMode.YearlyApproximate ? f.From.AddDays(14) : f.From).ToDateTime(TimeOnly.MinValue) }; NewWorkWindow.Field(p, "이번 회차만 시기 변경 · 기존 요청일/마감은 유지", occurrenceDate);
                        p.Children.Add(UI.Button("이번 회차 시기 저장", () => { if (occurrenceDate.SelectedDate is not DateTime selected || selected.Year != int.Parse(f.Occurrence.Split(':').Last()[..4])) throw new ArgumentException("해당 회차와 같은 해의 날짜를 선택해주세요."); var s = Disk.Clone(main.Store.Settings); s.Types.Single(x => x.Id == type.Id).OccurrenceDates[f.Occurrence] = DateOnly.FromDateTime(selected); main.Store.SaveSettings(s); w.Close(); Render(); _ = main.Refresh(); }));
                    }
                    if (type is not null && f.Occurrence is not null) p.Children.Add(UI.Button("이번 회차 건너뛰기", () => { if (!UI.Confirm("예상 일정의 이번 회차만 건너뛸까요? 기존 작업과 과거 파일은 바뀌지 않습니다.")) return; var s = Disk.Clone(main.Store.Settings); s.Types.Single(x => x.Id == type.Id).SkippedOccurrences.Add(f.Occurrence); main.Store.SaveSettings(s); w.Close(); Render(); }));
                    w.Content = p; w.Owner = this; w.ShowDialog();
                });
                b.HorizontalContentAlignment = HorizontalAlignment.Left;
                if (f.Description.Contains("±")) b.BorderThickness = new Thickness(0, 0, 0, 2);
                if (f.TypeId is null) b.Opacity = .7;
                content.Children.Add(b);
            }
            var card = UI.Card(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); card.Margin = new Thickness(6); Grid.SetColumn(card, (m - 1) % 3); Grid.SetRow(card, (m - 1) / 3); months.Children.Add(card);
        }
    }
}
