using RPH.Core.Cleaning;
using Xunit;

namespace RPH.Core.Tests;

public class CleaningMathTests
{
    [Theory]
    // Own floor: always, any department.
    [InlineData(1, 1, true, false, true, true)]
    [InlineData(1, 1, false, false, false, true)]
    // Another floor with a janitor: never.
    [InlineData(2, 1, true, true, true, false)]
    // Another floor with nobody: only the room's own department, and only if allowed.
    [InlineData(2, 1, false, true, true, true)]
    [InlineData(2, 1, false, false, true, false)]
    [InlineData(2, 1, false, true, false, false)]
    public void IsInZone(int roomFloor, int homeFloor, bool floorHasJanitor, bool sameDepartment, bool cover, bool expected)
    {
        Assert.Equal(expected, CleaningMath.IsInZone(roomFloor, homeFloor, floorHasJanitor, sameDepartment, cover));
    }

    [Theory]
    [InlineData(5, 5, 4, 4, 8, 8, 0)]
    [InlineData(4, 4, 4, 4, 8, 8, 0)]
    [InlineData(2, 6, 4, 4, 8, 8, 2)]
    [InlineData(10, 10, 4, 4, 8, 8, 2)]
    [InlineData(1, 12, 4, 4, 8, 8, 4)]
    [InlineData(6, 2, 4, 4, 8, 8, 2)]
    public void DistanceToRange(int x, int y, int minX, int minY, int maxX, int maxY, int expected)
    {
        Assert.Equal(expected, CleaningMath.DistanceToRange(x, y, minX, minY, maxX, maxY));
    }

    [Theory]
    [InlineData(2, false, 2, true)]
    [InlineData(3, false, 2, false)]
    [InlineData(0, true, 2, false)]
    public void IsInFrontOfRoom(int distance, bool inside, int front, bool expected)
    {
        Assert.Equal(expected, CleaningMath.IsInFrontOfRoom(distance, inside, front));
    }

    [Theory]
    // 150 s of game time per game hour: 30 game minutes = 75 s.
    [InlineData(74f, 30f, false)]
    [InlineData(75f, 30f, true)]
    [InlineData(200f, 30f, true)]
    public void WaitExpired(float waited, float limitMinutes, bool expected)
    {
        Assert.Equal(expected, CleaningMath.WaitExpired(waited, limitMinutes, 150f));
    }
}
