using WorkOrganizer.Core;

int passed = 0;
var results = new List<object>();
void Check(bool value, string name) { if (!value) throw new Exception("실패: " + name); passed++; results.Add(new { name, status = "PASS" }); Console.WriteLine("PASS " + name); }
void Reject(Action action, string name) { bool failed = false; try { action(); } catch { failed = true; } Check(failed, name); }
var settings = new AppSettings(); var wed = new DateOnly(2026, 11, 4); var thu = wed.AddDays(1);
Check(Planning.Days(wed, Planning.WeekEnd(wed), settings) == 3 && Planning.Days(thu, Planning.WeekEnd(thu), settings) == 2, "W11/W17 오늘 포함 수요일3·목요일2");
Check(Planning.Days(new(2026, 11, 7), new(2026, 11, 8), settings) == 0, "주말 능력0");
var holiday = Disk.Clone(settings); holiday.ExcludeHolidays = true; holiday.Holidays.Add(thu); Check(Planning.Days(wed, wed.AddDays(2), holiday) == 2, "선택 공휴일만 제외");
var monday = new DateOnly(2026, 11, 2); var due = new DateOnly(2026, 11, 20);
var jobs = new List<WorkItem> { new() { Name = "A", Effort = 10, State = WorkState.Active, DueOn = due }, new() { Name = "B", Effort = 10, State = WorkState.Active, DueOn = due } };
var capacity = Planning.Calculate(jobs, monday, due, settings); Check(capacity.Overloaded && capacity.Available == -5, "W05 20일치/15평일 과중");
jobs = [new() { Effort = 5, State = WorkState.Completed }]; Check(Planning.Calculate(jobs, thu, Planning.WeekEnd(thu), settings).Available == 2, "완료 업무 제외·과거 일수 이중환급 없음");
jobs = [new() { Effort = 2, State = WorkState.Review, DueOn = wed.AddDays(2) }]; Check(Planning.Calculate(jobs, wed, Planning.WeekEnd(wed), settings).Available == 3 && Planning.Calculate(jobs, wed, Planning.WeekEnd(wed), settings, jobs[0]).Available == 1, "검토중 제외·선택 하나 수락 가정");
var unknown = new WorkItem { Effort = .5m, State = WorkState.Active }; var withUnknown = Planning.Calculate([unknown], wed, Planning.WeekEnd(wed), settings); Check(withUnknown.UnknownEffort == .5m && withUnknown.Available == 2.5m, "기한 미정량0처리 금지");
Check(Planning.Urgency(new() { Effort = .5m, DueOn = wed }, wed, settings) == 1, "오늘 마감0.5일도 긴급");
jobs = [new() { Effort = 6, State = WorkState.Active, DueOn = new(2026, 11, 13) }]; Check(Planning.Calculate(jobs, monday, Planning.WeekEnd(monday), settings).Available == 4, "긴 마감의 후속약속을 보존하는 이번주 여유");
settings.CycleMonth = 4; settings.CycleDay = 1; Check(Planning.Cycle(new(2026, 3, 31), settings) == new DateOnly(2025, 4, 1), "사용자 기준일의 이전 주기");
var type = new WorkType { Name = "월간", Repeat = RepeatMode.Monthly, Day = 15 }; settings.Types.Add(type); Check(Planning.Forecasts([], settings, 2026, 11).Single().From == new DateOnly(2026, 11, 15), "휴일도 고정 반복일 유지·폴더생성 없음");
var yearly = new WorkType { Name = "연간", Repeat = RepeatMode.YearlyApproximate, Month = 3, Day = 4 }; settings.Types.Add(yearly); Check(Planning.Forecasts([], settings, 2026, 2).Any(x => x.Name == "연간"), "±2주 월경계 예상");
string baseFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "검사자료", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6])); Directory.CreateDirectory(baseFolder);
string root = Path.Combine(baseFolder, "업무자료"); Directory.CreateDirectory(root); var store = new WorkStore(root);
string chosen = Path.Combine(baseFolder, "선택양식.txt"), uncheckedFile = Path.Combine(baseFolder, "해제양식.txt"); File.WriteAllText(chosen, "양식원본"); File.WriteAllText(uncheckedFile, "복사금지"); var s = Disk.Clone(store.Settings); s.CommonTemplates.Add(new() { Path = chosen, Selected = true }); s.CommonTemplates.Add(new() { Path = uncheckedFile, Selected = false }); store.SaveSettings(s);
string original = "  동료가 보낸 요청\n원문 그대로  "; var task = store.Create("A작업", original, "제가 이해한 내용", new(2026, 10, 30), .5m, 1, null); string initialFolder = task.Folder; var registered = task.RegisteredAt;
Check(task.FolderName.StartsWith("261030_A작업_") && WorkText.Load(task.Folder).Contains(original) && File.Exists(Path.Combine(task.Folder, "선택양식.txt")) && !File.Exists(Path.Combine(task.Folder, "해제양식.txt")), "요청별 날짜명·원문보존·체크양식만 복사");
var second = store.Create("A작업", original, "", new(2026, 10, 30), 1, 1, null); Check(task.Id != second.Id && task.Folder != second.Folder && second.FolderName.EndsWith("_02"), "같은날 동명요청 독립·덮어쓰기 없음");
string requestKey = Guid.NewGuid().ToString("N"); var once = store.Create("재시도", "동일 요청", "이해", wed, 1, 1, null, requestKey: requestKey); var twice = store.Create("재시도", "동일 요청", "이해", wed, 1, 1, null, requestKey: requestKey); Check(once.Id == twice.Id && once.Folder == twice.Folder, "요청 저장 재시도는 같은 폴더 한 건");
Reject(() => store.Create("오류", "", "", wed, 0, 1, null), "잘못된 예상량으로 폴더 생성 금지");
string recordPath = Path.Combine(task.Folder, WorkText.FileName); string basis = Disk.Hash(recordPath); File.AppendAllText(recordPath, "\n외부수정"); Reject(() => store.SaveText(task, "앱 미저장 수정", basis), "외부편집 충돌 감지"); Check(File.ReadAllText(recordPath).Contains("외부수정") && Directory.GetFiles(task.Folder, "업무기록_앱수정본_*.txt").Length == 1, "충돌 양쪽 내용 보존");
store.ChangeState(task, WorkState.Active); Check(task.Folder == initialFolder, "수락은 이동 없이 상태만"); store.ChangeState(task, WorkState.Completed); string completedFolder = task.Folder; store.ChangeState(task, WorkState.Completed); Check(task.Folder == completedFolder && task.RegisteredAt == registered, "중복 완료·최초시각 보존");
store.ChangeState(task, WorkState.Active); Directory.Move(task.Folder, Path.Combine(Path.GetDirectoryName(task.Folder)!, "직접변경한폴더")); var renamed = store.Scan().Single(x => x.Id == task.Id); Check(renamed.RegisteredAt == registered && renamed.FolderName == "직접변경한폴더", "탐색기 개명·ID/시각 보존");
string externalCompleted = Path.Combine(store.CycleFolder(task.CycleStart), "완료", renamed.FolderName); Directory.Move(renamed.Folder, externalCompleted); var detected = new WorkStore(root).Scan().Single(x => x.Id == task.Id); Check(detected.State == WorkState.Completed && detected.CompletionDateUnknown && detected.CompletedAt is null, "앱 종료중 완료 이동·날짜 꾸미지 않음");
string activeAgain = Path.Combine(store.CycleFolder(task.CycleStart), "작업", renamed.FolderName); Directory.Move(externalCompleted, activeAgain); var reopened = store.Scan().Single(x => x.Id == task.Id); Check(reopened.State == WorkState.Active && reopened.RegisteredAt == registered, "완료폴더 밖 원래주기 복귀");
string past = Path.Combine(baseFolder, "지난결과.txt"); File.WriteAllText(past, "과거원본"); string copied = store.CopyInto(reopened, past, "결과물"); File.AppendAllText(copied, "이번수정"); Check(File.ReadAllText(past) == "과거원본", "지난제출물 복사본 편집·원본 불변"); Reject(() => store.CopyInto(reopened, past, "결과물"), "결과물 동명 덮어쓰기 금지");
var invalidBackup = Disk.Clone(store.Settings); invalidBackup.BackupLocation = Path.Combine(root, "백업"); Reject(() => store.SaveSettings(invalidBackup), "루트 내부 재귀백업 금지");
string restoreSource = Path.Combine(baseFolder, "복원할파일.txt"); File.WriteAllText(restoreSource, "이전내용"); store.RestoreFile(restoreSource, copied); Check(File.ReadAllText(copied) == "이전내용" && Directory.EnumerateFiles(Path.Combine(store.Control, "복원전보존"), "*", SearchOption.AllDirectories).Any(x => File.ReadAllText(x).Contains("이번수정")), "선택 파일 복원 전 현재본 별도보존");
var cycleSettings = Disk.Clone(store.Settings); var originalCycle = Planning.Cycle(DateOnly.FromDateTime(DateTime.Today), store.Settings); cycleSettings.CycleMonth = originalCycle.Month == 4 ? 5 : 4; cycleSettings.CycleDay = 1; store.SaveSettings(cycleSettings); Check(Planning.Cycle(DateOnly.FromDateTime(DateTime.Today), store.Settings) == originalCycle && reopened.CycleStart == originalCycle, "기준일 변경은 이후 새 주기부터·기존소속 보존");
var occurrenceSettings = Disk.Clone(settings); occurrenceSettings.Types.Add(new() { Id = "회차검사", Name = "정기검사", Repeat = RepeatMode.Monthly, Day = 15 }); occurrenceSettings.Types.Single(x => x.Id == "회차검사").OccurrenceDates["회차검사:2026-11"] = new(2026, 11, 20);
Check(Planning.Forecasts([], occurrenceSettings, 2026, 11).Single(x => x.TypeId == "회차검사").From == new DateOnly(2026, 11, 20) && Planning.Forecasts([], occurrenceSettings, 2026, 12).Single(x => x.TypeId == "회차검사").From == new DateOnly(2026, 12, 15), "이번 회차만 시기 변경·다음 회차 고정일 유지");
string metadataPath = Path.Combine(reopened.Folder, WorkStore.MetaName); var externallyChanged = Disk.ReadJson<WorkItem>(metadataPath)!; externallyChanged.Name += "외부정정"; Disk.WriteJson(metadataPath, externallyChanged);
Reject(() => store.Save(reopened), "오래 열린 창이 외부 업무정보를 덮어쓰지 않음");
string beforeConflictFolder = reopened.Folder; Reject(() => store.ChangeState(reopened, WorkState.Completed), "외부 업무정보 충돌이면 완료 이동 전 중단"); Check(Directory.Exists(beforeConflictFolder), "충돌 뒤 원래 작업폴더 위치 보존"); reopened = store.Scan().Single(x => x.Id == reopened.Id);
if (args.Length > 0 && File.Exists(args[0]))
{
    string backup = Path.Combine(baseFolder, "별도백업"); Directory.CreateDirectory(backup); var b = Disk.Clone(store.Settings); b.BackupLocation = backup; b.ResticPath = Path.GetFullPath(args[0]); store.SaveSettings(b); var engine = new BackupEngine(store); string password = BackupEngine.NewPassword();
    var firstBackup = await engine.Backup(password); Check(firstBackup.Success && firstBackup.SnapshotId is not null, "실제 restic 이력백업");
    var points = await engine.Snapshots(password); Check(points.Count == 1, "실제 복구시점 조회");
    var unchanged = await engine.Backup(password); Check(unchanged.Success && unchanged.SnapshotId == firstBackup.SnapshotId && (await engine.Snapshots(password)).Count == 1, "변경 없음은 복구시점을 꾸며 만들지 않음");
    string restore = Path.Combine(baseFolder, "별도복원"); await engine.Restore(password, points[0].Id, restore); var restored = Directory.EnumerateFiles(restore, WorkText.FileName, SearchOption.AllDirectories).First(x => x.Contains("직접변경한폴더")); Check(Disk.Hash(restored) == Disk.Hash(Path.Combine(reopened.Folder, WorkText.FileName)), "실제 별도복원 파일 해시 일치");
    using (var locked = new FileStream(Path.Combine(reopened.Folder, WorkText.FileName), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { var failed = await engine.Backup(password); Check(!failed.Success, "잠긴 파일을 전체성공으로 표시하지 않음"); }
    Check((await engine.Snapshots(password)).Any(x => x.Id == points[0].Id), "백업 실패 후 기존 복구시점 유지");
    bool wrongPassword = false; try { await engine.Snapshots("deliberately-wrong-test-password"); } catch { wrongPassword = true; } Check(wrongPassword, "잘못된 암호 복구실패를 숨기지 않음");
    File.AppendAllText(Path.Combine(reopened.Folder, WorkText.FileName), "\n다음버전"); var nextVersion = await engine.Backup(password); Check(nextVersion.Success && (await engine.Snapshots(password)).Any(x => x.Id == points[0].Id), "새 버전 백업 뒤 직전 복구시점 보호");
}
Directory.CreateDirectory(Path.Combine(baseFolder, "검사결과")); Disk.WriteJson(Path.Combine(baseFolder, "검사결과", "결과.json"), new { at = DateTimeOffset.Now, passed, scope = "핵심 계산·파일흐름·선택 시 실제 restic 복원 검사. UI/사용자목적 전체인수 아님.", results });
Console.WriteLine($"{passed}개 확인 · 자료: {baseFolder}");
