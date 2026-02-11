namespace MentorshipPlatform.Application.Common.Extensions;

public static class DateTimeExtensions
{
    public static bool IsWithinNext(this DateTime dateTime, TimeSpan timeSpan)
    {
        return dateTime >= DateTime.UtcNow && dateTime <= DateTime.UtcNow.Add(timeSpan);
    }

    public static bool IsPast(this DateTime dateTime)
    {
        return dateTime < DateTime.UtcNow;
    }

    public static DateTime ToStartOfDay(this DateTime dateTime)
    {
        return dateTime.Date;
    }

    public static DateTime ToEndOfDay(this DateTime dateTime)
    {
        return dateTime.Date.AddDays(1).AddTicks(-1);
    }
}