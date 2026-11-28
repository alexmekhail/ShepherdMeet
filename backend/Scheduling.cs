using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal sealed class Scheduling(string timeZoneId)
{
    public const string Church = "St. Philopater & St. Demiana Coptic Orthodox Church";
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(30);
    private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

    public string TimeZoneId => zone.Id;
    public DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;

    public (DateTime Start, DateTime End)? ToUtc(DateTime start, DateTime end)
    {
        start = DateTime.SpecifyKind(start, DateTimeKind.Unspecified);
        end = DateTime.SpecifyKind(end, DateTimeKind.Unspecified);
        if (end - start != Duration || zone.IsInvalidTime(start) || zone.IsInvalidTime(end)
            || zone.IsAmbiguousTime(start) || zone.IsAmbiguousTime(end))
            return null;

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(start, zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(end, zone);
        return endUtc - startUtc == Duration ? (startUtc, endUtc) : null;
    }

    public static string DateKey(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTime[] OverlappingDates(DateTime start, DateTime end)
    {
        var date = start.Date;
        // A 30-minute slot can overlap a prior-day booking or spill into the following day.
        return end.Date > date && end.TimeOfDay != TimeSpan.Zero
            ? [date.AddDays(-1), date, end.Date]
            : [date.AddDays(-1), date];
    }

    public static bool OverlapsLegacy(string date, string time, DateTime start, DateTime end)
    {
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var legacyDate))
            return false;

        var oldStart = time.Split(" - ", 2)[0].Trim();
        if (DateTime.TryParseExact(oldStart, ["h:mm tt", "hh:mm tt"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var explicitTime))
            return Overlaps(explicitTime.TimeOfDay);

        if (DateTime.TryParseExact(oldStart, ["h:mm", "hh:mm"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var ambiguousTime))
        {
            var morning = TimeSpan.FromHours(ambiguousTime.Hour % 12) + TimeSpan.FromMinutes(ambiguousTime.Minute);
            return Overlaps(morning) || Overlaps(morning + TimeSpan.FromHours(12));
        }

        // An unknown start can occupy its whole recorded date and spill into the next day.
        return legacyDate < end && start - legacyDate < TimeSpan.FromDays(1) + Duration;

        bool Overlaps(TimeSpan timeOfDay)
        {
            var legacyStart = legacyDate + timeOfDay;
            return legacyStart < end && start - legacyStart < Duration;
        }
    }

    public static async Task<IDbContextTransaction> LockAsync(AppDbContext db, CancellationToken cancellation)
    {
        // The application has one schedule. Use a transaction-scoped PostgreSQL
        // advisory lock so every writer sees the last committed schedule.
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellation);
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(163685412, 1)", cancellation);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
