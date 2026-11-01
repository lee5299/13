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
string recordPath = Path.Combine(task.Folder, WorkText.FileName); string basis = Disk.Hash(recordPath); File.AppendAllText(recordPath, "\n외부수정"); Reject(() => store.SaveText(task, "앱 미저장 수정", basis), "외부편집 충돌 감지"); Check(File.ReadAllText(recordPath).Contains("외부수정") && Directory.GetFiles(task.Folder, "공개기록_앱수정본_*.txt").Length == 1, "충돌 양쪽 내용 보존");
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

// ===== 2026-10-31 추가 요구: 공개/private 분리·AI 다듬기·백업 (모든 값은 합성 시험값) =====
const string PrivateUnique = "PRIVATE-합성고유값-7Q2Z-명령어 sudo systemctl restart 합성서비스";
const string PublicUnique = "PUBLIC-합성고유값-K8M1";
const string PrivatePass = "합성시험-private-암호-0001";
var vault = PrivateVault.Create(store.Control, PrivatePass, 2000);
Reject(() => PrivateVault.Create(store.Control, PrivatePass, 2000), "private 보관소 중복 생성 금지");
var split = store.Create("분리시험", "공개 요청 원문 " + PublicUnique, "공개 이해 내용", wed, 1, 1, null, privateText: PrivateUnique, vault: vault);
Check(WorkText.Load(split.Folder).Contains(PublicUnique) && !WorkText.Load(split.Folder).Contains("PRIVATE-합성고유값"), "DOC_PUBLIC/DOC_PRIVATE 두 영역 분리 저장");
Check(store.LoadPrivate(split, vault).Contains(PrivateUnique), "DOC_PRIVATE_SAVE private 별도 문서 저장·재열기");
string privateHash = WorkStore.PrivateHash(split); privateHash = store.SavePrivate(split, vault, store.LoadPrivate(split, vault) + "\n두번째 저장 " + PrivateUnique, privateHash);
string publicBasis = Disk.Hash(Path.Combine(split.Folder, WorkText.FileName)); store.SaveText(split, WorkText.Load(split.Folder) + "\n공개 추가", publicBasis);
vault.Lock(); var reopenedVault = PrivateVault.Unlock(store.Control, PrivatePass); var splitAgain = new WorkStore(root).Scan().Single(x => x.Id == split.Id);
Check(store.LoadPrivate(splitAgain, reopenedVault).Contains("두번째 저장") && WorkText.Load(splitAgain.Folder).Contains("공개 추가"), "공개/private 각각 재열기 일치");
Reject(() => store.LoadPrivate(splitAgain, vault), "잠긴 private는 읽지 않음");
bool wrongPrivate = false; try { PrivateVault.Unlock(store.Control, "틀린-합성-암호-0000"); } catch (UnauthorizedAccessException) { wrongPrivate = true; } Check(wrongPrivate, "틀린 private 암호 거부");
Reject(() => store.Create("잠금생성", "원문", "", wed, 1, 1, null, privateText: "상세", vault: vault), "잠긴 상태에서 private 포함 생성 금지");
var other = store.Create("다른업무", "원문", "", wed, 1, 1, null, privateText: "다른 상세", vault: reopenedVault);
File.Copy(WorkStore.PrivatePath(other), WorkStore.PrivatePath(splitAgain), true);
Reject(() => store.LoadPrivate(splitAgain, reopenedVault), "다른 업무의 private 문서는 해독되지 않음(업무별 결합)");
File.Copy(WorkStore.PrivatePath(splitAgain) + ".previous", WorkStore.PrivatePath(splitAgain), true);
Check(store.LoadPrivate(splitAgain, reopenedVault).Contains(PrivateUnique), "private 이전본도 같은 업무 암호문으로 보존");
bool PlainSomewhere(string folder, string needle) => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any(f => File.ReadAllText(f).Contains(needle) || System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(f)).Contains(needle));
Check(!PlainSomewhere(root, "PRIVATE-합성고유값"), "AI_PRIVATE_DERIVED 루트 어디에도(이전본·임시본·저널·설정) private 평문 없음");
Check(store.ExternalAiBlockers().Count == 0, "외부 AI 사전점검: 평문 없음이면 시작 가능");
File.WriteAllText(Path.Combine(splitAgain.Folder, WorkText.PrivateFolder, "메모.txt"), "합성 평문"); Check(store.ExternalAiBlockers().Any(x => x.Contains("메모.txt")), "PRIVATE_LOCK_GATE private 폴더의 평문 파일 차단"); File.Delete(Path.Combine(splitAgain.Folder, WorkText.PrivateFolder, "메모.txt"));
Reject(() => ExternalAiSession.Begin(store, reopenedVault, "시험", null), "키가 메모리에 있으면 외부 AI 시작 거부");
reopenedVault.Lock(); ExternalAiSession.Begin(store, reopenedVault, "시험", null); Check(ExternalAiSession.IsActive(new WorkStore(root)), "PRIVATE_LOCK_RECOVER 재실행 뒤에도 잠금 상태 확인"); ExternalAiSession.End(store, false); Check(!ExternalAiSession.IsActive(store), "PRIVATE_LOCK_END 프로세스 없음 확인 후 해제");
reopenedVault = PrivateVault.Unlock(store.Control, PrivatePass);

// MIGRATION: 이전 단일기록
var legacy = store.Create("이전기록", "원문", "", wed, 1, 1, null); string legacyText = "============ 요청 원문\n이전 원문\n============ 작업 순서 · 실행 명령어\nLEGACY-합성명령-9X";
File.Delete(Path.Combine(legacy.Folder, WorkText.FileName)); File.WriteAllText(Path.Combine(legacy.Folder, WorkText.LegacyFileName), legacyText); legacy = store.Scan().Single(x => x.Id == legacy.Id);
Check(legacy.Unclassified && WorkText.Load(legacy.Folder) == "" && legacy.Request.Contains("이전 원문"), "MIGRATION_UNCLASSIFIED 미분류 기록은 공개 전송본으로 읽지 않음(화면 표시만)");
Check(File.ReadAllText(Path.Combine(legacy.Folder, WorkText.LegacyFileName)) == legacyText, "MIGRATION_PRESERVE 기존 단일기록 자동 덮어쓰기 없음");
Check(store.ExternalAiBlockers().Any(x => x.Contains(WorkText.LegacyFileName)), "미분류 평문이 있으면 외부 AI 시작 차단");
Reject(() => store.ArchiveLegacy(legacy, reopenedVault), "공개 분리 저장 전에는 기존 기록 이동 금지");
store.SaveText(legacy, WorkText.Build("이전 원문", "사용자가 직접 나눈 공개 내용"), ""); int moved = store.ArchiveLegacy(legacy, reopenedVault);
Check(moved == 1 && !File.Exists(Path.Combine(legacy.Folder, WorkText.LegacyFileName)) && store.LoadPrivateArchive(legacy, reopenedVault).Contains("LEGACY-합성명령-9X") && !PlainSomewhere(root, "LEGACY-합성명령"), "사용자 분리 후 기존 기록을 private 암호문으로 보존·평문 제거");

// 사전검사(로컬)
string sample = string.Join("\n", "키 sk-ant-TESTONLY000000000000000000abcd 와 AKIATESTONLY00000000", "password = Synth3tic!Pw", "주민 900101-1234567 / 900101 1234567", "날짜 2026-10-31 숫자 1234561234567", "휴대 010-1234-5678 회사 02-123-4567 내선 123", "내선 2345 · 전화 4321", "예외 010-0000-1111");
var found = SensitiveScanner.Scan(sample, ["010-0000-1111"]);
Check(found.Count(x => x.Kind == FindingKind.Key) == 3, "AI_SCAN_KEY sk-·AWS·키=값 검출");
Check(found.Count(x => x.Kind == FindingKind.ResidentId) == 2 && !found.Any(x => x.Value.Contains("2026-10-31") || x.Value.Contains("1234561234567")), "AI_SCAN_ID 구분자 변형 검출·날짜/무효 월 비대상");
Check(found.Any(x => x.Value == "010-1234-5678") && found.Any(x => x.Value == "02-123-4567") && found.Any(x => x.Value == "4321"), "AI_SCAN_PHONE 휴대·지역·표시된 짧은 번호 검출");
Check(!found.Any(x => x.Value == "123" || x.Value == "2345"), "AI_EXTENSION_MARKED 내선 명시 짧은 번호 예외");
Check(!found.Any(x => x.Value.Contains("0000-1111")) && SensitiveScanner.Scan(sample).Any(x => x.Value == "010-0000-1111"), "AI_EXTENSION_CUSTOM 사용자 예외 적용·삭제 시 다시 검출");
Check(found.First(x => x.Value == "010-1234-5678").Line == 5, "검출 위치(줄) 표시");

// 가림·복원
string publicDoc = WorkText.Build("받은 원문 그대로 · 010-9999-8888", "담당 010-1234-5678 에게 확인. 다시 010-1234-5678. 키 sk-ant-TESTONLY111111111111111111wxyz");
var polish = AiPolishRequest.Prepare(publicDoc, AiMode.Sentence, []);
Check(!polish.Prompt.Contains("010-1234-5678") && !polish.Prompt.Contains("sk-ant-TESTONLY") && !polish.Prompt.Contains("받은 원문 그대로") && polish.Masked.Map.Count == 2, "AI_SCAN_CONFIRM 가린 사본만 전송·요청 원문 미전송·반복값 하나의 표시");
Check(polish.Masked.Text.Split(MaskedText.Token(polish.Masked.RequestTag, 1)).Length - 1 == 2, "AI_MASK 반복값은 같은 표시로");
var fake = new FakeAi(p => p.Replace("확인.", "확인합니다."));
var outcome = await fake.Send(polish.Prompt, default); var received = polish.Receive(outcome.Text);
Check(received.Revised.Contains("010-1234-5678") && received.Revised.Contains("확인합니다.") && received.Problems.Count == 0, "AI_MASK_RESULT 로컬 복원");
Check(fake.Prompts.All(x => !x.Contains("PRIVATE-합성고유값") && !x.Contains("LEGACY-합성명령")), "AI_INTERNAL_DENY 전송문에 private 없음");
string applied = polish.Apply(publicDoc, received); Check(applied.StartsWith(WorkText.SplitOriginal(publicDoc).Original) && applied.Contains("확인합니다."), "AI_RESULT_APPLY 적용 시 받은 요청 원문 보존(R06)");
Reject(() => polish.Apply(publicDoc + "\n요청 중 편집", received), "AI_RESULT_CONFLICT 요청 중 수정되면 덮어쓰지 않음");
Check(!polish.StillMatches(publicDoc + " 변경"), "AI_SCAN_CHANGED 내용 변경 시 재검사 필요");
string token1 = MaskedText.Token(polish.Masked.RequestTag, 1);
var dropped = polish.Receive(polish.Masked.Text.Replace(token1, ""));
Check(dropped.Problems.Any(x => x.Contains("사라졌")), "AI_MASK_UNCERTAIN 표시 삭제 알림");
var mutated = polish.Receive(polish.Masked.Text.Replace(token1, token1.Replace("[[", "[").Replace("]]", "]")));
Check(mutated.Problems.Count > 0 && !mutated.Revised.Contains("010-1234-5678 에게"), "변형 표시는 추측해 복원하지 않음");
var another = AiPolishRequest.Prepare(WorkText.Build("원문", "다른 번호 010-2222-3333"), AiMode.Structure, []);
while (another.Masked.RequestTag == polish.Masked.RequestTag) another = AiPolishRequest.Prepare(WorkText.Build("원문", "다른 번호 010-2222-3333"), AiMode.Structure, []);
var cross = polish.Receive(another.Masked.Text);
Check(!cross.Revised.Contains("010-2222-3333") && cross.Problems.Any(x => x.Contains("다른 요청")), "AI_MASK_SCOPE 다른 요청의 대응정보와 섞지 않음");
Reject(() => polish.Receive("   "), "AI_RESULT_FAILURE 빈 응답 적용 불가");
Reject(() => AiPolishRequest.Prepare(WorkText.Build("원문", "연락 010-1234-5678"), AiMode.Sentence, ["re:(["]), "AI_SCAN_FAIL 검사 오류이면 전송하지 않음");
Reject(() => AiPolishRequest.Prepare("============ 요청 원문\n원문만\n", AiMode.Sentence, []), "요청 원문만 있으면 보낼 대상 없음");
var diff = TextDiff.Lines("가\n나\n다", "가\n나나\n다\n라");
Check(diff.Count(x => x.Kind == DiffKind.Changed) == 1 && diff.Count(x => x.Kind == DiffKind.Added) == 1 && diff.Count(x => x.Kind == DiffKind.Same) == 2, "AI_RESULT_DIFF 프로그램이 줄 비교 생성");
var env = new Dictionary<string, string?> { ["ANTHROPIC_API_KEY"] = "x", ["OPENAI_API_KEY"] = "y", ["CODEX_API_KEY"] = "z", ["PATH"] = "p" }; CliAiConnector.Sanitize(env);
Check(env.Count == 1 && env.ContainsKey("PATH"), "AI_NO_PAID_FALLBACK API 키 환경변수 제거");
var claudeArgs = CliAiConnector.Arguments(AiProvider.Claude, "w", "o"); Check(claudeArgs.IndexOf("--tools") >= 0 && claudeArgs[claudeArgs.IndexOf("--tools") + 1] == "" && !new CliAiConnector(AiProvider.Claude, null, TimeSpan.FromSeconds(1)).HasLocalTools && new CliAiConnector(AiProvider.Codex, null, TimeSpan.FromSeconds(1)).HasLocalTools, "Claude 도구 전부 끔·Codex는 잠금 대상");
var haikuArgs = CliAiConnector.Arguments(AiProvider.Claude, "w", "o", "haiku"); Check(haikuArgs[haikuArgs.IndexOf("--model") + 1] == "haiku" && new CliAiConnector(AiProvider.Codex, null, TimeSpan.FromSeconds(1), "haiku").Model is null && new AppSettings().AiModel == "haiku", "모델 지정: Claude 기본 haiku·Codex에는 Claude 별칭 넘기지 않음");
Reject(() => new CliAiConnector(AiProvider.Claude, null, TimeSpan.FromSeconds(1), "haiku; rm -rf"), "모델 이름에 명령 문자 거부");
Check(CliAiConnector.Classify(1, "", "Error: Not logged in").StartsWith("로그인") && CliAiConnector.Classify(1, "", "usage limit reached").StartsWith("사용 한도"), "인증/한도 실패 구분 안내");

// 로컬 백업 범위
var scoped = new BackupEngine(store); Check(scoped.SourceFiles().Any(f => f.Contains(Path.DirectorySeparatorChar + WorkText.PrivateFolder + Path.DirectorySeparatorChar)), "BACKUP_LOCAL_INCLUDE 기본 private 포함");
var excludeSettings = Disk.Clone(store.Settings); excludeSettings.BackupPrivate = false; store.SaveSettings(excludeSettings);
Check(!new BackupEngine(store).SourceFiles().Any(f => BackupEngine.IsPrivatePath(Path.GetRelativePath(root, f))) && new BackupEngine(store).ScopeDescription().Contains("private 제외"), "BACKUP_PRIVATE_OPTION 제외 선택·범위 표시");
var includeSettings = Disk.Clone(store.Settings); includeSettings.BackupPrivate = true; store.SaveSettings(includeSettings);

// 수동 private 제외 백업파일
string portableFolder = Path.Combine(baseFolder, "수동백업파일"); Directory.CreateDirectory(portableFolder);
File.WriteAllText(Path.Combine(splitAgain.Folder, WorkText.LegacyFileName), "LEGACY2-합성평문-민감"); // 다시 생긴 미분류 평문도 제외되는지
string portablePassword = PrivatePass; // 사용자가 입력하는 업무정리 암호 하나로 private·백업파일·복구정보를 보호
Check(Directory.GetFiles(portableFolder).Length == 0, "BACKUP_CLOUD_NO_AUTO 명시 실행 전에는 파일 없음");
var portable = await PortableBackup.Create(store, portablePassword, Path.Combine(portableFolder, "업무정리_백업" + PortableBackup.Extension), iterations: 2000);
var listed = PortableBackup.ListEntries(portable.File, portablePassword);
Check(listed.Count > 0 && !listed.Any(e => e.Path.Contains("/private/") || e.Path.EndsWith(".previous") || e.Path.Contains("검색.sqlite") || e.Path.Contains("private보관소") || e.Path.Contains("업무기록")) && portable.SkippedPrivate > 0 && portable.SkippedLegacy > 0, "BACKUP_CLOUD_CLEAN private·이전본·색인·보관소·미분류 제외");
string portableRestore = Path.Combine(baseFolder, "수동백업복원"); var restoredEntries = PortableBackup.Restore(portable.File, portablePassword, portableRestore);
Check(!PlainSomewhere(portableRestore, "PRIVATE-합성고유값") && !PlainSomewhere(portableRestore, "LEGACY2-합성평문") && PlainSomewhere(portableRestore, PublicUnique), "BACKUP_CLOUD_PORTABLE 저장소 없이 별도 복원·private 부재·공개 내용 존재");
Check(store.LoadPrivate(splitAgain, reopenedVault).Contains(PrivateUnique), "BACKUP_RESTORE_EXCLUDED 제외본 복원이 현재 private를 지우지 않음");
bool wrongPortable = false; try { PortableBackup.Restore(portable.File, "deliberately-wrong-portable-pass", Path.Combine(baseFolder, "틀린암호복원")); } catch (UnauthorizedAccessException) { wrongPortable = true; } Check(wrongPortable, "백업파일 틀린 암호 복원 실패");
string truncated = Path.Combine(baseFolder, "잘린파일" + PortableBackup.Extension); var bytes = File.ReadAllBytes(portable.File); File.WriteAllBytes(truncated, bytes[..(bytes.Length / 2)]);
Reject(() => PortableBackup.Restore(truncated, portablePassword, Path.Combine(baseFolder, "잘린복원")), "중간에 끊긴 백업파일은 복원하지 않음");
Reject(() => PortableBackup.Create(store, portablePassword, Path.Combine(root, "안쪽" + PortableBackup.Extension), iterations: 2000).GetAwaiter().GetResult(), "백업파일을 업무 루트 안에 만들지 않음");
Check(!File.Exists(Path.Combine(portableFolder, "업무정리_백업" + PortableBackup.Extension + ".partial")) && Directory.GetFiles(portableFolder).Length == 1, "불완전 파일을 성공으로 남기지 않음");
File.Delete(Path.Combine(splitAgain.Folder, WorkText.LegacyFileName));

// 사용자 암호 하나: 복구정보·암호 변경
string recoveryFile = Path.Combine(baseFolder, "복구정보.txt"); string internalBackupPassword = BackupEngine.NewPassword();
RecoveryInfo.Write(recoveryFile, internalBackupPassword, PrivatePass, "합성저장소", 2000);
Check(RecoveryInfo.Read(recoveryFile, PrivatePass) == internalBackupPassword && !File.ReadAllText(recoveryFile).Contains(internalBackupPassword), "복구정보는 사용자 암호로 잠기고 내부 백업 암호 평문 없음");
bool wrongRecovery = false; try { RecoveryInfo.Read(recoveryFile, "틀린-합성-암호-0000"); } catch (UnauthorizedAccessException) { wrongRecovery = true; } Check(wrongRecovery, "복구정보 틀린 사용자 암호 거부");
const string NewPass = "합성시험-새암호-0002";
string beforeChange = Disk.Hash(WorkStore.PrivatePath(splitAgain));
PrivateVault.ChangePassphrase(store.Control, PrivatePass, NewPass);
bool oldRejected = false; try { PrivateVault.Unlock(store.Control, PrivatePass); } catch (UnauthorizedAccessException) { oldRejected = true; }
using (var changed = PrivateVault.Unlock(store.Control, NewPass))
    Check(oldRejected && store.LoadPrivate(splitAgain, changed).Contains(PrivateUnique) && Disk.Hash(WorkStore.PrivatePath(splitAgain)) == beforeChange && !File.Exists(PrivateVault.PathFor(store.Control) + ".previous"), "암호 변경: 기존 private 파일 그대로 새 암호로 열림·옛 암호 거부·옛 포장 미보존");
Check(store.LoadPrivate(splitAgain, reopenedVault).Contains(PrivateUnique), "암호 변경 중 열려 있던 잠금 해제 상태 유지(자료 키 동일)");
Reject(() => PrivateVault.ChangePassphrase(store.Control, PrivatePass, "또다른-합성-암호-03"), "옛 암호로는 암호 변경 불가");
Check(PortableBackup.ListEntries(portable.File, PrivatePass).Count == listed.Count, "암호 변경 전에 만든 백업파일은 만들 때의 암호로 열림");
PrivateVault.ChangePassphrase(store.Control, NewPass, PrivatePass);

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
    string privateRestore = Path.Combine(baseFolder, "private포함복원"); var latest = (await engine.Snapshots(password)).First(); await engine.Restore(password, latest.Id, privateRestore);
    var restoredPrivate = Directory.EnumerateFiles(privateRestore, WorkText.PrivateFileName, SearchOption.AllDirectories).First(x => x.Contains("분리시험"));
    Check(Disk.Hash(restoredPrivate) == WorkStore.PrivateHash(splitAgain) && latest.Description.Contains("private 포함"), "BACKUP_LOCAL_INCLUDE private 포함 백업·별도 위치 복원 일치(암호문)");
    var noPrivate = Disk.Clone(store.Settings); noPrivate.BackupPrivate = false; store.SaveSettings(noPrivate); var excludedRun = await engine.Backup(password);
    var allPoints = await engine.Snapshots(password);
    Check(excludedRun.Success && allPoints.Any(x => x.Id == latest.Id) && allPoints.Any(x => x.Description.Contains("private 제외")), "BACKUP_HISTORY_PRESERVE 옵션 변경 뒤 이전 private 포함 복구시점 유지");
    var offline = Disk.Clone(store.Settings); offline.BackupLocation = Path.Combine(baseFolder, "분리된외장디스크"); store.SaveSettings(offline); var missingDevice = await new BackupEngine(store).Backup(password);
    Check(!missingDevice.Success && missingDevice.Message.Contains("연결되지"), "BACKUP_LOCAL_DESTINATION 외장디스크 미연결 실패 안내");
}
Directory.CreateDirectory(Path.Combine(baseFolder, "검사결과")); Disk.WriteJson(Path.Combine(baseFolder, "검사결과", "결과.json"), new { at = DateTimeOffset.Now, passed, scope = "핵심 계산·파일흐름·선택 시 실제 restic 복원 검사. UI/사용자목적 전체인수 아님.", results });
Console.WriteLine($"{passed}개 확인 · 자료: {baseFolder}");

sealed class FakeAi(Func<string, string> transform) : IAiConnector
{
    public List<string> Prompts { get; } = [];
    public string Name => "합성 응답";
    public bool HasLocalTools => false;
    public Task<AiOutcome> Send(string prompt, CancellationToken cancel)
    {
        Prompts.Add(prompt);
        int a = prompt.IndexOf(AiPrompt.BodyStart, StringComparison.Ordinal) + AiPrompt.BodyStart.Length + 1, b = prompt.LastIndexOf(AiPrompt.BodyEnd, StringComparison.Ordinal);
        return Task.FromResult(new AiOutcome(true, transform(prompt[a..b]), "ok"));
    }
}
