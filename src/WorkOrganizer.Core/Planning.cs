using System.Text.RegularExpressions;

namespace WorkOrganizer.Core;

public sealed record Capacity(decimal Available, decimal UnknownEffort, bool Overloaded, bool HasOverdue, string Message);
public static class Planning
{
    public static bool IsWorkingDay(DateOnly date, AppSettings s) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && (!s.ExcludeHolidays || !s.Holidays.Contains(date));
    public static int Days(DateOnly from, DateOnly through, AppSettings s)
    {
        if (through < from) return 0;
        int result = 0;
        for (var d = from; ; d = d.AddDays(1)) { if (IsWorkingDay(d, s)) result++; if (d == through) break; }
        return result;
    }
    public static DateOnly WeekEnd(DateOnly today) => today.AddDays(6 - (((int)today.DayOfWeek + 6) % 7));
    public static DateOnly MonthEnd(DateOnly today) => new(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
    public static Capacity Calculate(IEnumerable<WorkItem> all, DateOnly today, DateOnly horizon, AppSettings s, WorkItem? accepting = null)
    {
        var jobs = all.Where(x => x.State is WorkState.Active or WorkState.Waiting).ToList();
        if (accepting is not null && jobs.All(x => x.Id != accepting.Id)) jobs.Add(accepting);
        decimal unknown = jobs.Where(x => x.DueOn is null).Sum(x => x.Effort);
        var dates = jobs.Where(x => x.DueOn is not null).Select(x => x.DueOn!.Value).Append(horizon).Distinct().Order().ToList();
        bool overload = dates.Any(t => jobs.Where(x => x.DueOn <= t).Sum(x => x.Effort) > Days(today, t, s));
        decimal slack = dates.Where(t => t >= horizon).Select(t => (decimal)Days(today, t, s) - jobs.Where(x => x.DueOn <= t).Sum(x => x.Effort)).Min() - unknown;
        bool overdue = jobs.Any(x => x.DueOn < today);
        string message = slack >= 0 ? $"{slack:0.##}일 작업여유가 있습니다" : $"{-slack:0.##}일치 초과입니다";
        if (overload || overdue) message += " · 기한별 과중/기한초과 확인";
        if (unknown > 0) message += $" · 기한 미정 {unknown:0.##}일치 포함, 보수 참고치";
        return new(slack, unknown, overload, overdue, message);
    }
    public static int Urgency(WorkItem x, DateOnly today, AppSettings s) => x.DueOn is null ? 3 : x.DueOn < today ? 0 : x.DueOn == today ? 1 : Days(today, x.DueOn.Value, s) <= x.Effort ? 2 : 3;
    public static IEnumerable<WorkItem> Ordered(IEnumerable<WorkItem> jobs, DateOnly today, AppSettings s) => jobs.OrderBy(x => x.State == WorkState.Waiting ? 1 : 0).ThenBy(x => Urgency(x, today, s)).ThenByDescending(x => x.Priority).ThenBy(x => x.DueOn ?? DateOnly.MaxValue).ThenBy(x => x.RegisteredAt).ThenBy(x => x.Id);
    public static DateOnly Cycle(DateOnly date, AppSettings s)
    {
        var rule = s.CycleRules.Where(r => r.EffectiveFrom <= date).MaxBy(r => r.EffectiveFrom);
        int month = rule?.Month ?? s.CycleMonth, day = rule?.Day ?? s.CycleDay;
        var start = new DateOnly(date.Year, month, Math.Min(day, DateTime.DaysInMonth(date.Year, month)));
        return date < start ? new(date.Year - 1, month, Math.Min(day, DateTime.DaysInMonth(date.Year - 1, month))) : start;
    }
    public static string NormalizeName(string value) => Regex.Replace(value.ToLowerInvariant(), @"^\d{6}[_\s-]*|[\s_\-]+|\d{4}[-.]?\d{2}", "");
    public static double Similarity(string a, string b)
    {
        a = NormalizeName(a); b = NormalizeName(b); if (a.Length == 0 || b.Length == 0) return 0;
        if (a.Contains(b) || b.Contains(a)) return 1;
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++) for (int j = 1; j <= b.Length; j++) d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return 1 - (double)d[a.Length, b.Length] / Math.Max(a.Length, b.Length);
    }
    public static IEnumerable<Forecast> Forecasts(IEnumerable<WorkItem> all, AppSettings s, int year, int month)
    {
        var jobs = all.ToList(); var begin = new DateOnly(year, month, 1); var end = MonthEnd(begin);
        foreach (var t in s.Types.Where(t => t.Repeat != RepeatMode.None))
        {
            foreach (int y in new[] { year - 1, year, year + 1 })
            foreach (int m in t.Repeat == RepeatMode.Monthly ? Enumerable.Range(1, 12) : new[] { t.Month })
            {
                var date = new DateOnly(y, m, Math.Min(t.Day, DateTime.DaysInMonth(y, m)));
                string key = $"{t.Id}:{y}-{m:00}";
                if (t.OccurrenceDates.TryGetValue(key, out var changedDate)) date = changedDate;
                var from = t.Repeat == RepeatMode.YearlyApproximate ? date.AddDays(-14) : date;
                var to = t.Repeat == RepeatMode.YearlyApproximate ? date.AddDays(14) : date;
                if (from > end || to < begin || t.SkippedOccurrences.Contains(key)) continue;
                yield return new(t.Name, from, to, t.OccurrenceDates.ContainsKey(key) ? "이번 회차 시기 변경" : t.Repeat == RepeatMode.Monthly ? $"매월 {t.Day}일" : "지난 시기 ±2주 예상", t.Id, key, jobs.Any(x => x.TypeId == t.Id && x.Occurrence == key && x.State != WorkState.Cancelled));
            }
        }
        foreach (var group in jobs.Where(x => x.RequestedOn.Year == year - 1 && (x.TypeId is null || !s.Types.Any(t => t.Id == x.TypeId && t.Repeat != RepeatMode.None))).GroupBy(x => x.Name))
        {
            var last = group.MaxBy(x => x.RequestedOn)!;
            var center = new DateOnly(year, last.RequestedOn.Month, Math.Min(last.RequestedOn.Day, DateTime.DaysInMonth(year, last.RequestedOn.Month)));
            if (center.AddDays(-14) <= end && center.AddDays(14) >= begin) yield return new(last.Name, center.AddDays(-14), center.AddDays(14), "지난 주기 참고 · 반복 미등록", last.TypeId, null, false);
        }
    }
}
