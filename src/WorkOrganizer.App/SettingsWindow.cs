using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WorkOrganizer.Core;

namespace WorkOrganizer.App;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(MainWindow main, DesktopHost desktop)
    {
        var store = main.Store!; var draft = Disk.Clone(store.Settings); Title = "설정 — 업무 정리"; Width = 820; Height = 850; Background = UI.Canvas; FontFamily = main.FontFamily; Owner = main; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var tabs = new TabControl { Margin = new Thickness(20) }; Content = tabs;
        var general = new StackPanel { Margin = new Thickness(18) }; AddTab(tabs, "기본 / 주기", general); general.Children.Add(UI.Text("업무 루트", 20, true)); general.Children.Add(UI.Text(store.Root)); general.Children.Add(UI.Button("다른 루트로 전환", () => { string? p = UI.PickFolder("관리할 다른 루트 선택", store.Root); if (p is not null && UI.Confirm("기존 자료를 이동/합치지 않고 관리 대상을 전환할까요? 열어둔 작업 창을 먼저 닫아주세요.")) { main.SetRoot(p); Close(); } }));
        general.Children.Add(UI.Text("바탕화면 표시", 20, true)); general.Children.Add(UI.Text("아이콘 아래층 모드는 시험 기능입니다. 실제 Windows의 표시·클릭·배율을 확인해야 합니다. 목록이 가려지면 알림영역 아이콘에서 일반 창으로 복귀할 수 있습니다.")); general.Children.Add(UI.Row(UI.Button("바탕화면 모드 시험", desktop.Attach), UI.Button("일반 창으로 복귀", desktop.Detach)));
        var month = new TextBox { Text = draft.CycleMonth.ToString(), Width = 70 }; var day = new TextBox { Text = draft.CycleDay.ToString(), Width = 70 };
        general.Children.Add(UI.Button("취소 상태로 보관한 요청 보기", () => { var w = UI.Window("취소 요청", 900, 550); var list = new ListBox { ItemsSource = main.Items.Where(x => x.State == WorkState.Cancelled).OrderByDescending(x => x.RegisteredAt).ToList(), DisplayMemberPath = "FolderName" }; list.MouseDoubleClick += (_, _) => { if (list.SelectedItem is WorkItem work) { main.ShowDetail(work); w.Close(); } }; w.Content = list; w.Owner = main; Close(); w.Show(); }));
        general.Children.Add(UI.Text("관리 주기 기준일", 20, true)); general.Children.Add(UI.Text("변경은 이후 새 주기에 적용합니다. 기존 업무의 주기/파일은 자동으로 옮기지 않습니다.")); general.Children.Add(UI.Row(NewWorkWindow.Labeled("월", month), NewWorkWindow.Labeled("일", day)));
        var holidayToggle = new CheckBox { Content = "설정한 공휴일도 가용일에서 제외", IsChecked = draft.ExcludeHolidays }; general.Children.Add(holidayToggle); var holidays = NewWorkWindow.Multi(100); holidays.Text = string.Join("\n", draft.Holidays.Select(x => x.ToString("yyyy-MM-dd"))); NewWorkWindow.Field(general, "공휴일 · 한 줄에 YYYY-MM-DD", holidays);
        var templates = new StackPanel { Margin = new Thickness(18) }; AddTab(tabs, "양식 / 업무 유형", templates); templates.Children.Add(UI.Text("공통 기본양식", 20, true)); templates.Children.Add(UI.Text("체크한 파일만 새 요청폴더에 복사합니다. 원본 양식과 기존 업무는 바뀌지 않습니다.")); templates.Children.Add(TemplateEditor(store, draft.CommonTemplates));
        var typeList = new ListBox { Height = 160, DisplayMemberPath = "Name" }; void RefreshTypes() => typeList.ItemsSource = draft.Types.ToList(); RefreshTypes(); templates.Children.Add(UI.Text("사용자 업무 유형", 20, true)); templates.Children.Add(typeList);
        templates.Children.Add(UI.Row(UI.Button("유형 추가", () => { var type = new WorkType(); if (new TypeWindow(store, type) { Owner = this }.ShowDialog() == true) { draft.Types.Add(type); RefreshTypes(); } }), UI.Button("선택 유형 편집", () => { if (typeList.SelectedItem is WorkType t) { var edit = Disk.Clone(t); if (new TypeWindow(store, edit) { Owner = this }.ShowDialog() == true) { draft.Types[draft.Types.IndexOf(t)] = edit; RefreshTypes(); } } })));
        var backup = new StackPanel { Margin = new Thickness(18) }; AddTab(tabs, "백업 / 복원", backup); backup.Children.Add(UI.Text("로컬 이력 백업", 20, true)); backup.Children.Add(UI.Text("최초 전체 보관, 이후 변경 확인. 저장된 파일만 보관하며 미저장 편집은 포함하지 않습니다. 같은 물리 디스크의 백업은 그 디스크 고장에 대비하지 못합니다."));
        var location = new TextBox { Text = draft.BackupLocation }; NewWorkWindow.Field(backup, "백업 위치 · 업무 루트와 별도", location); backup.Children.Add(UI.Button("백업 위치 선택", () => { string? p = UI.PickFolder("별도 백업 위치 선택", location.Text); if (p is not null) location.Text = p; }));
        var minutes = new TextBox { Text = draft.BackupMinutes.ToString(), Width = 80 }; var daily = new TextBox { Text = draft.KeepDaily.ToString(), Width = 80 }; var monthly = new TextBox { Text = draft.KeepMonthly.ToString(), Width = 80 }; backup.Children.Add(UI.Row(NewWorkWindow.Labeled("실행 중 간격(분)", minutes), NewWorkWindow.Labeled("일별 복구시점", daily), NewWorkWindow.Labeled("월별 복구시점", monthly)));
        var records = new CheckBox { Content = "공개기록", IsChecked = draft.BackupRecords }; var captures = new CheckBox { Content = "캡처", IsChecked = draft.BackupCaptures }; var results = new CheckBox { Content = "결과물", IsChecked = draft.BackupResults }; var privateScope = new CheckBox { Content = "private 상세(암호문)", IsChecked = draft.BackupPrivate, Margin = new Thickness(12, 0, 0, 0) }; backup.Children.Add(UI.Text("주 로컬 백업 범위 · 업무정보/설정/양식/이름규칙은 항상 포함 · 기본은 private 포함")); backup.Children.Add(UI.Row(records, captures, results, privateScope));
        var scopeNote = UI.Text("", 12); void Scope() => scopeNote.Text = "실행 전 범위: " + (records.IsChecked == true ? "공개기록 · " : "") + (captures.IsChecked == true ? "캡처 · " : "") + (results.IsChecked == true ? "결과물 · " : "") + (privateScope.IsChecked == true ? "private 포함" : "private 제외 — 이 백업으로는 private을 복구할 수 없습니다") + "\n범위를 바꿔도 이전 범위의 복구시점은 지우지 않습니다. 아래 ‘백업파일 만들기’의 범위와는 별개입니다."; Scope();
        foreach (var c in new[] { records, captures, results, privateScope }) { c.Checked += (_, _) => Scope(); c.Unchecked += (_, _) => Scope(); } backup.Children.Add(scopeNote);
        string bundled = Path.Combine(AppContext.BaseDirectory, "도구", "restic.exe"); if (string.IsNullOrWhiteSpace(draft.ResticPath) && File.Exists(bundled)) draft.ResticPath = bundled;
        var restic = new TextBox { Text = draft.ResticPath }; NewWorkWindow.Field(backup, "백업 엔진 위치", restic); backup.Children.Add(UI.Button("백업 엔진 선택", () => { var p = UI.PickFile("restic 실행파일 선택", null, "실행파일|*.exe"); if (p is not null) restic.Text = p; }));
        backup.Children.Add(UI.Text("변경한 설정은 ‘설정 저장’ 후 적용됩니다. 저장한 다음 설정을 다시 열어 아래 백업/복원 기능을 사용해주세요.\n첫 백업 전에 복구정보를 별도로 저장해주세요. 복구정보는 업무정리 암호로 잠기며, 다른 PC에서는 복구정보 파일과 업무정리 암호로 복원합니다. 사용자가 입력하는 암호는 private·복구정보·백업파일 모두 같은 업무정리 암호 하나입니다."));
        backup.Children.Add(UI.Row(UI.Button("복구정보 저장", () => { string? pw = main.AskUserPassword(this, "복구정보를 업무정리 암호로 잠가 저장합니다."); if (pw is not null) main.Backups!.ExportKey(pw); }), UI.Button("기존 복구정보 가져오기", () => main.Backups!.ImportKey(() => MainWindow.AskPasswordOnly(this, "복구정보를 저장할 때 쓴 업무정리 암호를 입력해주세요."))), UI.Button("업무정리 암호 변경", () => main.ChangeUserPassword(this))));
        var backupStatus = UI.Text(store.Settings.LastBackupStatus); backup.Children.Add(backupStatus);
        backup.Children.Add(UI.Row(UI.Button("지금 백업", () => { _ = UI.GuardAsync(async () => { backupStatus.Text = "백업 중 · 범위: " + main.Backups!.Engine.ScopeDescription(); var r = await main.Backups!.BackupNow(); backupStatus.Text = r.Message + $" · {r.Elapsed.TotalSeconds:0.0}초"; await main.Refresh(); }); }), UI.Button("백업 중단", () => main.Backups!.Cancel()), UI.Button("복구시점 보기 / 복원", () => new RestoreWindow(main) { Owner = this }.ShowDialog())));
        backup.Children.Add(UI.Text("백업파일 만들기 · 클라우드 보관용 · private 제외", 20, true));
        backup.Children.Add(UI.Text("누를 때만 private을 뺀 독립 백업파일 하나를 만듭니다. 자동 생성·업로드·동기화는 하지 않으며, 클라우드 업로드는 직접 해주세요. private 원본·이전본·임시본·검색색인·백업키·미분류 이전 기록은 넣지 않습니다. 업무정리 암호로 보호되며, 복원할 때 만들 당시의 업무정리 암호가 필요합니다. 주 로컬 백업 범위는 바꾸지 않습니다."));
        var portableStatus = UI.Text("", 12); backup.Children.Add(portableStatus);
        backup.Children.Add(UI.Row(UI.Button("백업파일 만들기", () => { _ = UI.GuardAsync(async () => { portableStatus.Text = "만드는 중 · private 제외"; var r = await main.Backups!.CreatePortable(() => main.AskUserPassword(this, "백업파일을 업무정리 암호로 잠급니다.")); portableStatus.Text = r is null ? "취소 · 파일을 만들지 않았습니다." : $"완료 · {r.File} · 파일 {r.FileCount}개 · {r.Bytes / 1024.0 / 1024.0:0.0}MB · 제외: private {r.SkippedPrivate}개, 미분류 이전 기록 {r.SkippedLegacy}개"; }); }, true), UI.Button("백업파일을 빈 폴더로 복원", () => { _ = UI.GuardAsync(async () => { var n = await main.Backups!.RestorePortable(() => MainWindow.AskPasswordOnly(this, "이 백업파일을 만들 때의 업무정리 암호를 입력해주세요.")); if (n is not null) portableStatus.Text = $"복원 및 해시 확인 완료 · {n}개 파일 · 현재 업무와 private은 바뀌지 않았습니다."; }); })));
        var ai = new StackPanel { Margin = new Thickness(18) }; AddTab(tabs, "AI 연결 / 전송 전 검사", ai);
        ai.Children.Add(UI.Text("AI 연결", 20, true));
        ai.Children.Add(UI.Text("상세창의 ‘문장 다듬기’·‘내용+구조 다듬기’를 누를 때만, 공개 영역의 정리 내용(받은 요청 원문 제외)을 PC에서 검사·가린 뒤 보냅니다. private은 보내지 않습니다. PC에 설치한 공식 Claude Code 또는 Codex 프로그램을 앱이 뒤에서 실행하며, 그 프로그램에서 본인이 로그인한 구독 범위를 씁니다. 실패해도 별도 유료 API로 바꾸지 않습니다. 보낸 내용은 해당 서비스의 서버에서 처리됩니다."));
        var provider = new ComboBox { ItemsSource = new[] { "Claude (도구 모두 끔 · 텍스트 응답만)", "Codex (읽기 전용 · 요청 중 private 잠금)" }, SelectedIndex = (int)draft.AiProvider, Width = 380, Margin = new Thickness(0, 4, 0, 8) }; NewWorkWindow.Field(ai, "연결할 프로그램", provider);
        var exe = new TextBox { Text = draft.AiExecutablePath, ToolTip = "비워두면 PATH와 기본 설치 위치에서 찾습니다." }; NewWorkWindow.Field(ai, "실행파일 위치 · 비워두면 자동으로 찾음", exe);
        ai.Children.Add(UI.Button("실행파일 선택", () => { var f = UI.PickFile("claude/codex 실행파일 선택", null, "실행파일|*.exe;*.cmd"); if (f is not null) exe.Text = f; }));
        var model = new TextBox { Text = draft.AiModel, Width = 260, ToolTip = "비우면 프로그램 기본 모델" }; NewWorkWindow.Field(ai, "모델 · Claude는 haiku(가벼움)/sonnet/opus · Codex는 모델 이름 · 비우면 기본값", model);
        var timeout = new TextBox { Text = draft.AiTimeoutSeconds.ToString(), Width = 80 }; NewWorkWindow.Field(ai, "응답 대기 한도(초)", timeout);
        var loginStatus = new TextBox { IsReadOnly = true, Height = 110, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        ai.Children.Add(UI.Button("연결·로그인 상태 확인", () => { _ = UI.GuardAsync(async () => { var c = new CliAiConnector((AiProvider)provider.SelectedIndex, exe.Text.Trim(), TimeSpan.FromSeconds(30)); loginStatus.Text = "확인 중 · " + (c.Executable ?? "프로그램을 찾지 못함"); var r = await c.CheckLogin(default); loginStatus.Text = r.Status + "\n" + r.Text; }); })); ai.Children.Add(loginStatus);
        ai.Children.Add(UI.Text("전화번호 검사 예외", 20, true));
        ai.Children.Add(UI.Text("보내도 되는 번호(예: 사내 대표번호)를 한 줄에 하나씩 적습니다. 숫자만 비교합니다. re: 로 시작하면 정규식입니다. ‘내선 123’처럼 내선으로 적힌 짧은 번호는 자동으로 예외입니다. 일반 전화번호는 옆에 내선이 적혀 있어도 검사합니다."));
        var exceptions = NewWorkWindow.Multi(110); exceptions.Text = string.Join("\n", draft.PhoneExceptions); ai.Children.Add(exceptions);
        ai.Children.Add(UI.Text("외부 폴더 AI 작업", 20, true));
        ai.Children.Add(UI.Text("업무 루트를 Claude Code/Codex로 직접 열어 작업할 때 사용합니다. 시작하면 private 키를 지우고, 루트 안에 private 평문(미분류 이전 기록 등)이 없을 때만 PowerShell 창을 엽니다. 작업 중에는 private을 볼 수 없고, 끝낼 때 창이 닫혔는지 확인한 뒤 잠금을 풉니다. private 파일은 암호문이라 AI가 읽어도 내용을 얻을 수 없습니다. 같은 사용자 권한의 프로그램이 앱 메모리를 읽는 경우는 검증하지 않았습니다."));
        ai.Children.Add(UI.Row(UI.Button("Claude로 시작", () => { main.StartExternalAi("claude"); Close(); }), UI.Button("Codex로 시작", () => { main.StartExternalAi("codex"); Close(); }), UI.Button("터미널만 열기", () => { main.StartExternalAi("terminal"); Close(); }), UI.Button("외부 AI 작업 종료 확인", main.EndExternalAi)));
        void Save()
        {
            if (main.Backups!.Engine.Running) throw new IOException("백업/복원이 끝난 뒤 설정을 저장해주세요.");
            if (!int.TryParse(month.Text, out int cm) || !int.TryParse(day.Text, out int cd) || !int.TryParse(minutes.Text, out int bm) || !int.TryParse(daily.Text, out int kd) || !int.TryParse(monthly.Text, out int km)) throw new ArgumentException("월/일과 백업 숫자를 확인해주세요.");
            var dates = new List<DateOnly>(); foreach (string line in holidays.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) { if (!DateOnly.TryParseExact(line, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) throw new ArgumentException("공휴일은 YYYY-MM-DD 형식으로 입력해주세요: " + line); dates.Add(d); }
            draft.CycleMonth = cm; draft.CycleDay = cd; draft.ExcludeHolidays = holidayToggle.IsChecked == true; draft.Holidays = dates; draft.BackupLocation = location.Text.Trim(); draft.BackupMinutes = bm; draft.KeepDaily = kd; draft.KeepMonthly = km; draft.BackupRecords = records.IsChecked == true; draft.BackupCaptures = captures.IsChecked == true; draft.BackupResults = results.IsChecked == true; draft.BackupPrivate = privateScope.IsChecked == true; draft.ResticPath = restic.Text.Trim();
            if (!int.TryParse(timeout.Text, out int seconds) || seconds < 15 || seconds > 3600) throw new ArgumentException("AI 응답 대기 한도는 15~3600초로 입력해주세요.");
            var phoneRules = exceptions.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            foreach (string rule in phoneRules.Where(r => r.StartsWith("re:", StringComparison.OrdinalIgnoreCase))) { try { _ = new System.Text.RegularExpressions.Regex(rule[3..]); } catch (ArgumentException) { throw new ArgumentException("전화번호 예외 정규식을 확인해주세요: " + rule); } }
            if (exe.Text.Trim().Length > 0 && !File.Exists(exe.Text.Trim())) throw new ArgumentException("AI 실행파일 위치를 확인해주세요.");
            draft.AiProvider = (AiProvider)provider.SelectedIndex; draft.AiExecutablePath = exe.Text.Trim(); draft.AiTimeoutSeconds = seconds; draft.PhoneExceptions = phoneRules; draft.AiModel = model.Text.Trim(); _ = new CliAiConnector(draft.AiProvider, null, TimeSpan.FromSeconds(seconds), draft.AiModel);
            store.ValidateSettings(draft); Materialize(store, draft); store.SaveSettings(draft); DialogResult = true;
        }
        foreach (TabItem tab in tabs.Items)
        {
            var dock = new DockPanel(); var buttons = UI.Row(UI.Button("취소", () => Close()), UI.Button("설정 저장", Save, true)); DockPanel.SetDock(buttons, Dock.Bottom); dock.Children.Add(buttons); var old = tab.Content; tab.Content = null; dock.Children.Add(new ScrollViewer { Content = old, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); tab.Content = dock;
        }
    }
    private static void AddTab(TabControl tabs, string title, UIElement content) => tabs.Items.Add(new TabItem { Header = title, Content = content });
    public static UIElement TemplateEditor(WorkStore store, List<TemplateFile> files)
    {
        var panel = new StackPanel(); var list = new StackPanel();
        void Refresh() { list.Children.Clear(); foreach (var file in files.ToList()) { var check = new CheckBox { Content = Path.GetFileName(file.Path), IsChecked = file.Selected, Margin = new Thickness(3), ToolTip = file.Path }; check.Checked += (_, _) => file.Selected = true; check.Unchecked += (_, _) => file.Selected = false; list.Children.Add(UI.Row(check, UI.Button("목록에서 제거", () => { files.Remove(file); Refresh(); }))); } }
        panel.Children.Add(list); panel.Children.Add(UI.Button("양식 파일 추가", () => { var p = UI.PickFile("등록할 기본양식 파일 선택"); if (p is not null) { files.Add(new TemplateFile { Path = p, Selected = false }); Refresh(); } })); Refresh(); return panel;
    }
    public static void Materialize(WorkStore store, AppSettings settings)
    {
        string Import(string path, string area)
        {
            if (!Path.IsPathFullyQualified(path)) return path;
            if (!File.Exists(path)) throw new FileNotFoundException("등록할 양식/지난 파일을 찾을 수 없습니다.", path);
            if (Disk.IsInside(path, store.Control)) return Path.GetRelativePath(store.Root, path);
            string folder = Path.Combine(store.Control, area, Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(folder); string target = Path.Combine(folder, Path.GetFileName(path)); File.Copy(path, target, false); return Path.GetRelativePath(store.Root, target);
        }
        foreach (var file in settings.CommonTemplates.Concat(settings.Types.SelectMany(t => t.Templates))) file.Path = Import(file.Path, "기본양식");
        foreach (var t in settings.Types) t.PastFiles = t.PastFiles.Select(p => Import(p, "지난제출물")).ToList();
    }
}

public sealed class TypeWindow : Window
{
    public TypeWindow(WorkStore store, WorkType type)
    {
        Title = "업무 유형 / 선택적 반복 설정"; Width = 680; Height = 800; Background = UI.Canvas; FontFamily = new System.Windows.Media.FontFamily("Malgun Gothic");
        var panel = new StackPanel { Margin = new Thickness(24) }; var name = new TextBox { Text = type.Name }; NewWorkWindow.Field(panel, "업무 유형 이름", name);
        var repeat = new ComboBox { ItemsSource = new[] { "반복 등록 안 함", "매월 고정일", "매년 대략적 시기 ±2주" }, SelectedIndex = (int)type.Repeat, Margin = new Thickness(0, 4, 0, 8) }; NewWorkWindow.Field(panel, "반복 설정 · 유형과 별개 선택", repeat);
        var month = new TextBox { Text = type.Month.ToString(), Width = 70 }; var day = new TextBox { Text = type.Day.ToString(), Width = 70 }; panel.Children.Add(UI.Row(NewWorkWindow.Labeled("시기(월)", month), NewWorkWindow.Labeled("일", day)));
        var names = NewWorkWindow.Multi(90); names.Text = string.Join("\n", type.SubmissionNames); NewWorkWindow.Field(panel, "제출항목의 지정 이름 · 한 줄에 하나 · 확장자 고정 없음", names);
        panel.Children.Add(UI.Text("유형별 기본양식 · 체크한 파일만 새 업무에 복사")); panel.Children.Add(SettingsWindow.TemplateEditor(store, type.Templates));
        var past = new ListBox { Height = 95, ItemsSource = type.PastFiles.ToList() }; NewWorkWindow.Field(panel, "첫해에 참고할 지난 제출파일 · 선택한 파일만 등록", past);
        panel.Children.Add(UI.Row(UI.Button("지난 파일 직접 지정", () => { var p = UI.PickFile("과거 제출물 파일 하나 선택"); if (p is not null) { type.PastFiles.Add(p); past.ItemsSource = type.PastFiles.ToList(); } }), UI.Button("선택 해제", () => { if (past.SelectedItem is string p) { type.PastFiles.Remove(p); past.ItemsSource = type.PastFiles.ToList(); } })));
        panel.Children.Add(UI.Text("변경은 이후 새 업무에 적용합니다. 이미 생성한 파일/제출항목/과거 회차는 소급 변경하지 않습니다."));
        panel.Children.Add(UI.Row(UI.Button("취소", () => DialogResult = false), UI.Button("유형 저장", () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || !int.TryParse(month.Text, out int m) || !int.TryParse(day.Text, out int d) || m is < 1 or > 12 || d is < 1 or > 31) throw new ArgumentException("유형 이름과 월/일을 확인해주세요.");
            type.Name = name.Text.Trim(); type.Repeat = (RepeatMode)repeat.SelectedIndex; type.Month = m; type.Day = d; type.SubmissionNames = names.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(); DialogResult = true;
        }, true))); Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
