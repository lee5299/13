namespace WorkOrganizer.Core;

public enum DiffKind { Same, Changed, Removed, Added }
public sealed record DiffRow(DiffKind Kind, string Left, string Right, int LeftPrefix = 0, int LeftSuffix = 0, int RightPrefix = 0, int RightSuffix = 0);

/// <summary>
/// 원문/수정안의 줄 단위 비교를 프로그램이 직접 만든다(AI_RESULT). AI에게 비교 산출물을 요청하지 않는다.
/// 바뀐 줄은 공통 앞·뒤 글자 수를 함께 돌려 화면에서 바뀐 가운데 부분만 강조할 수 있게 한다.
/// </summary>
public static class TextDiff
{
    public const int MaxLines = 4000;
    public static List<DiffRow> Lines(string left, string right)
    {
        string[] a = Split(left), b = Split(right);
        if (a.Length > MaxLines || b.Length > MaxLines) return [new DiffRow(DiffKind.Changed, left, right)];
        int n = a.Length, m = b.Length;
        var lcs = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
        var raw = new List<DiffRow>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y]) { raw.Add(new(DiffKind.Same, a[x], b[y])); x++; y++; }
            else if (lcs[x + 1, y] >= lcs[x, y + 1]) raw.Add(new(DiffKind.Removed, a[x++], ""));
            else raw.Add(new(DiffKind.Added, "", b[y++]));
        }
        while (x < n) raw.Add(new(DiffKind.Removed, a[x++], ""));
        while (y < m) raw.Add(new(DiffKind.Added, "", b[y++]));
        // 연속된 삭제·추가 묶음을 같은 위치의 '변경' 줄로 짝짓는다.
        var rows = new List<DiffRow>();
        for (int i = 0; i < raw.Count;)
        {
            if (raw[i].Kind == DiffKind.Same) { rows.Add(raw[i++]); continue; }
            var removed = new List<string>(); var added = new List<string>();
            while (i < raw.Count && raw[i].Kind != DiffKind.Same) { if (raw[i].Kind == DiffKind.Removed) removed.Add(raw[i].Left); else added.Add(raw[i].Right); i++; }
            int pairs = Math.Min(removed.Count, added.Count);
            for (int k = 0; k < pairs; k++) { var (p, s) = Inline(removed[k], added[k]); rows.Add(new(DiffKind.Changed, removed[k], added[k], p, s, p, s)); }
            for (int k = pairs; k < removed.Count; k++) rows.Add(new(DiffKind.Removed, removed[k], ""));
            for (int k = pairs; k < added.Count; k++) rows.Add(new(DiffKind.Added, "", added[k]));
        }
        return rows;
    }
    public static (int Prefix, int Suffix) Inline(string a, string b)
    {
        int p = 0; while (p < a.Length && p < b.Length && a[p] == b[p]) p++;
        int s = 0; while (s < a.Length - p && s < b.Length - p && a[a.Length - 1 - s] == b[b.Length - 1 - s]) s++;
        return (p, s);
    }
    private static string[] Split(string text) => text.Replace("\r\n", "\n").Split('\n');
}
