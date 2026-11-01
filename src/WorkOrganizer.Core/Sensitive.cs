using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WorkOrganizer.Core;

public enum FindingKind { Key, ResidentId, Phone }
public sealed record Finding(FindingKind Kind, int Start, int Length, string Value, int Line, int Column, string Rule)
{
    public string KindLabel => Kind switch { FindingKind.Key => "키·비밀값", FindingKind.ResidentId => "주민등록번호", _ => "전화번호" };
    public string Display => $"{Line}줄 {Column}칸 · {KindLabel} · {Value}";
}

/// <summary>
/// AI 요청 전 PC 안에서만 수행하는 사전검사(AI_SCAN_LOCAL). 원문을 검사 목적으로 외부에 보내지 않는다.
/// 지원 경계는 docs/사전검사_지원범위.md에 기록한다. 모든 임의의 비밀·민감문구를 판별한다고 보장하지 않는다.
/// </summary>
public static class SensitiveScanner
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(2);
    private static Regex R(string pattern, RegexOptions extra = RegexOptions.None) => new(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled | extra, Limit);
    // 값 그룹(v)이 있으면 그 부분만 가린다.
    private static readonly (string Rule, Regex Pattern)[] KeyRules =
    [
        ("개인키 블록", R(@"-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----[\s\S]*?(?:-----END [A-Z0-9 ]*PRIVATE KEY-----|\z)")),
        ("sk- 형식 키", R(@"(?<![A-Za-z0-9])sk-(?:ant-|proj-|live-|test-)?[A-Za-z0-9_\-]{20,}")),
        ("AWS 액세스 키", R(@"(?<![A-Z0-9])(?:AKIA|ASIA)[0-9A-Z]{16}(?![A-Z0-9])")),
        ("GitHub 토큰", R(@"(?<![A-Za-z0-9])(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{20,})")),
        ("Google API 키", R(@"(?<![A-Za-z0-9])AIza[0-9A-Za-z_\-]{30,}")),
        ("Slack 토큰", R(@"(?<![A-Za-z0-9])xox[abprs]-[A-Za-z0-9\-]{10,}")),
        ("JWT", R(@"(?<![A-Za-z0-9])eyJ[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}")),
        ("Bearer 토큰", R(@"\bbearer\s+(?<v>[A-Za-z0-9\-._~+/]{16,}=*)", RegexOptions.IgnoreCase)),
        ("키 이름=값", R(@"(?:api[_\- ]?key|secret|token|password|passwd|pwd|access[_\- ]?key|client[_\- ]?secret|비밀번호|비번|암호|패스워드|인증키|토큰)\s*[:=：]\s*[""']?(?<v>[^\s,;""']{6,})", RegexOptions.IgnoreCase)),
    ];
    private static readonly Regex Resident = R(@"(?<!\d)\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\s?[-–]?\s?[1-8](?:\d{6}|\*{6})(?!\d)");
    private static readonly (string Rule, Regex Pattern)[] PhoneRules =
    [
        ("휴대전화", R(@"(?<![\d+])(?:\+82[\s\-.]?(?:\(0\))?|0)1[016789][\s\-.)]?\d{3,4}[\s\-.]?\d{4}(?!\d)")),
        ("지역번호 전화", R(@"(?<![\d+])(?:\+82[\s\-.]?|0)(?:2|[3-6][1-5]|70|50[2-8])[\s\-.)]?\d{3,4}[\s\-.]?\d{4}(?!\d)")),
        ("괄호 지역번호", R(@"\(0(?:2|[3-6][1-5]|70)\)\s?\d{3,4}[\s\-.]?\d{4}(?!\d)")),
        ("대표번호", R(@"(?<![\d\-])1[5-9]\d{2}-\d{4}(?![\d\-])")),
        ("국번 전화", R(@"(?<![\d\-.])\d{3,4}-\d{4}(?![\d\-])")),
        ("표시된 짧은 번호", R(@"(?:전화|연락처|핸드폰|휴대폰|tel|phone|☎)\s*(?:번호)?\s*[:：]?\s*(?<v>\d{3,5})(?![\d\-])", RegexOptions.IgnoreCase)),
    ];
    // 내선으로 명시된 짧은 번호(AI_EXTENSION_MARKED). 2~5자리만 내선 예외. 일반 전화번호 전체는 예외로 만들지 않는다.
    private static readonly Regex Extension = R(@"(?:내선|ext\.?|extension)\s*(?:번호)?\s*[:：]?\s*(?<v>\d{2,5})(?![\d\-])", RegexOptions.IgnoreCase);

    public static List<Finding> Scan(string text, IEnumerable<string>? phoneExceptions = null)
    {
        text ??= "";
        var raw = new List<(FindingKind Kind, int Start, int Length, string Rule)>();
        foreach (var (rule, pattern) in KeyRules) foreach (Match m in pattern.Matches(text)) Add(raw, FindingKind.Key, m, rule);
        foreach (Match m in Resident.Matches(text)) Add(raw, FindingKind.ResidentId, m, "주민등록번호");
        var extensions = Extension.Matches(text).Select(m => (m.Groups["v"].Index, m.Groups["v"].Length)).ToHashSet();
        var exceptions = (phoneExceptions ?? Enumerable.Empty<string>()).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        foreach (var (rule, pattern) in PhoneRules)
            foreach (Match m in pattern.Matches(text))
            {
                var g = m.Groups["v"].Success ? m.Groups["v"] : m.Groups[0];
                if (extensions.Contains((g.Index, g.Length))) continue;
                if (IsException(g.Value, exceptions)) continue;
                Add(raw, FindingKind.Phone, m, rule);
            }
        // 겹치면 키 > 주민번호 > 전화, 같은 종류는 긴 것을 남긴다.
        var chosen = new List<(FindingKind Kind, int Start, int Length, string Rule)>();
        foreach (var f in raw.OrderBy(x => (int)x.Kind).ThenByDescending(x => x.Length).ThenBy(x => x.Start))
            if (!chosen.Any(c => f.Start < c.Start + c.Length && c.Start < f.Start + f.Length)) chosen.Add(f);
        return chosen.OrderBy(x => x.Start).Select(x =>
        {
            int line = 1, column = 1;
            for (int i = 0; i < x.Start; i++) { if (text[i] == '\n') { line++; column = 1; } else if (text[i] != '\r') column++; }
            return new Finding(x.Kind, x.Start, x.Length, text.Substring(x.Start, x.Length), line, column, x.Rule);
        }).ToList();
    }
    private static void Add(List<(FindingKind, int, int, string)> list, FindingKind kind, Match m, string rule)
    {
        var g = m.Groups["v"].Success ? m.Groups["v"] : m.Groups[0];
        if (g.Length > 0) list.Add((kind, g.Index, g.Length, rule));
    }
    public static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
    public static bool IsException(string value, IReadOnlyList<string> exceptions)
    {
        foreach (string rule in exceptions)
        {
            if (rule.StartsWith("re:", StringComparison.OrdinalIgnoreCase))
            {
                try { if (Regex.IsMatch(value, rule[3..], RegexOptions.CultureInvariant, Limit)) return true; }
                catch (ArgumentException) { throw new ArgumentException("전화번호 예외 규칙의 정규식을 확인해주세요: " + rule); }
            }
            else if (Digits(rule).Length > 0 && Digits(rule) == Digits(value)) return true;
        }
        return false;
    }
}

/// <summary>한 번의 AI 요청에만 쓰는 가림 대응정보(AI_MASK_SCOPE). 원래 값은 AI에 보내지 않는다.</summary>
public sealed class MaskedText
{
    public string RequestTag { get; }
    public string SourceText { get; }
    public string SourceHash { get; }
    public string Text { get; }
    public IReadOnlyDictionary<string, string> Map { get; }
    public int FindingCount { get; }
    private MaskedText(string tag, string source, string text, Dictionary<string, string> map, int count) { RequestTag = tag; SourceText = source; SourceHash = Disk.HashText(source); Text = text; Map = map; FindingCount = count; }
    public static string Token(string tag, int n) => $"[[가림-{tag}-{n}]]";

    public static MaskedText Create(string text, IReadOnlyList<Finding> findings)
    {
        string tag;
        do tag = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)); while (text.Contains("가림-" + tag, StringComparison.Ordinal));
        var byValue = new Dictionary<string, string>(StringComparer.Ordinal); var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var sb = new StringBuilder(); int at = 0;
        foreach (var f in findings.OrderBy(x => x.Start))
        {
            if (f.Start < at) continue;
            sb.Append(text, at, f.Start - at);
            if (!byValue.TryGetValue(f.Value, out var token)) { token = Token(tag, byValue.Count + 1); byValue[f.Value] = token; map[token] = f.Value; }
            sb.Append(token); at = f.Start + f.Length;
        }
        sb.Append(text, at, text.Length - at);
        return new MaskedText(tag, text, sb.ToString(), map, findings.Count);
    }

    /// <summary>응답의 가림 표시를 PC 안에서 원래 값으로 되돌린다. 불확실한 부분은 추측하지 않고 알린다(AI_MASK_UNCERTAIN).</summary>
    public MaskRestore Restore(string response)
    {
        var problems = new List<string>();
        var exact = new Regex(@"\[\[가림-([0-9A-F]{4})-(\d+)\]\]", RegexOptions.CultureInvariant);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        string restored = exact.Replace(response, m =>
        {
            if (m.Groups[1].Value != RequestTag) { problems.Add($"다른 요청의 가림 표시가 있습니다: {m.Value} · 복원하지 않고 그대로 둡니다."); return m.Value; }
            if (!Map.TryGetValue(m.Value, out var value)) { problems.Add($"알 수 없는 가림 번호: {m.Value} · 복원하지 않고 그대로 둡니다."); return m.Value; }
            counts[m.Value] = counts.GetValueOrDefault(m.Value) + 1; return value;
        });
        foreach (var token in Map.Keys)
        {
            int before = CountOccurrences(Text, token), after = counts.GetValueOrDefault(token);
            if (after == 0) problems.Add($"{token} 표시가 응답에서 사라졌습니다. 원래 값({Mask(Map[token])})이 들어갈 위치를 직접 확인해주세요.");
            else if (after > before) problems.Add($"{token} 표시가 원문보다 많이 나타났습니다({before}→{after}). 복원 위치를 확인해주세요.");
        }
        // 변형된 표시: 정확한 형식이 아닌 '가림' 흔적은 복원하지 않고 알린다.
        foreach (Match m in new Regex(@"\[*\s*가림[\s\-_]*[0-9A-Fa-f]{0,4}[\s\-_]*\d*\s*\]*", RegexOptions.CultureInvariant).Matches(restored))
            if (m.Value.Contains('[') || m.Value.Contains(']') || m.Value.Contains(RequestTag, StringComparison.OrdinalIgnoreCase))
                problems.Add($"형식이 바뀐 가림 표시로 보이는 부분: \"{m.Value.Trim()}\" · 추측해 복원하지 않았습니다.");
        return new MaskRestore(restored, problems.Distinct().ToList());
    }
    private static int CountOccurrences(string text, string token) { int n = 0, i = 0; while ((i = text.IndexOf(token, i, StringComparison.Ordinal)) >= 0) { n++; i += token.Length; } return n; }
    public static string Mask(string value) => value.Length <= 4 ? new string('*', value.Length) : value[..2] + new string('*', Math.Min(8, value.Length - 4)) + value[^2..];
}
public sealed record MaskRestore(string Text, List<string> Problems);
