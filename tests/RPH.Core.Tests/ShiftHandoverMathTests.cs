using RPH.Core.HR;
using Xunit;

namespace RPH.Core.Tests;

public class ShiftHandoverMathTests
{
    [Theory]
    [InlineData(20.25f, 20f, 15f)]
    [InlineData(7f, 20f, 660f)]
    [InlineData(1f, 20f, 300f)]
    [InlineData(20f, 20f, 0f)]
    public void MinutesSinceWrapsOverMidnight(float now, float boundary, float expected)
    {
        Assert.Equal(expected, ShiftHandoverMath.MinutesSince(now, boundary), 2);
    }

    [Theory]
    [InlineData(6.5f, 7f, 30f)]
    [InlineData(19.75f, 20f, 15f)]
    [InlineData(23f, 7f, 480f)]
    [InlineData(7f, 7f, 1440f)]
    public void MinutesUntilIsTheNextBoundary(float now, float boundary, float expected)
    {
        Assert.Equal(expected, ShiftHandoverMath.MinutesUntil(now, boundary), 2);
    }

    [Theory]
    [InlineData(7f, 20f, 13f)]
    [InlineData(20f, 7f, 11f)]
    public void ShiftHoursWrapsOverMidnight(float start, float end, float expected)
    {
        Assert.Equal(expected, ShiftHandoverMath.ShiftHours(start, end), 3);
    }

    [Theory]
    [InlineData(0u, 15)]
    [InlineData(5u, 20)]
    [InlineData(15u, 30)]
    [InlineData(16u, 15)]
    public void CoverLimitStaysInRangeAndFollowsTheSeed(uint seed, int expected)
    {
        Assert.Equal(expected, ShiftHandoverMath.CoverLimit(15f, 30f, seed));
    }

    [Fact]
    public void CoverLimitWithEqualBoundsIsThatValue()
    {
        Assert.Equal(20, ShiftHandoverMath.CoverLimit(20f, 20f, 12345u));
    }

    [Theory]
    // No relief: leave at once.
    [InlineData(0f, false, false, false)]
    // Handover time, relief already there: stay.
    [InlineData(3f, true, true, true)]
    // Handover done, relief there: leave.
    [InlineData(5f, true, true, false)]
    // Handover done, relief late: keep covering...
    [InlineData(12f, true, false, true)]
    // ... until the limit (20 min).
    [InlineData(20f, true, false, false)]
    public void MustCover(float minutes, bool hasRelief, bool reliefArrived, bool expected)
    {
        Assert.Equal(expected, ShiftHandoverMath.MustCover(minutes, hasRelief, reliefArrived, 5f, 20f));
    }

    [Theory]
    // 130 for a 13 h shift = 10 per hour.
    [InlineData(130, 30f, 13f, 100f, 5)]
    [InlineData(130, 30f, 13f, 150f, 8)]
    [InlineData(130, 30f, 13f, 0f, 0)]
    [InlineData(130, 0f, 13f, 100f, 0)]
    [InlineData(0, 30f, 13f, 100f, 0)]
    [InlineData(130, -10f, 13f, 100f, 0)]
    public void OvertimePayIsTheProratedWage(int wage, float minutes, float shiftHours, float percent, int expected)
    {
        Assert.Equal(expected, ShiftHandoverMath.OvertimePay(wage, minutes, shiftHours, percent));
    }
}
