using RPH.Core.Economy;
using Xunit;

namespace RPH.Core.Tests;

public class EconomyMathTests
{
    [Theory]
    [InlineData(0f, 40f)]
    [InlineData(1f, 40f)]
    [InlineData(2.75f, 75f)]
    [InlineData(4.5f, 110f)]
    [InlineData(5f, 110f)]
    public void RampIsFlatOutsideAndLinearInside(float stars, float expected)
    {
        Assert.Equal(expected, EconomyMath.Ramp(stars, 1f, 40f, 4.5f, 110f), 3);
    }

    [Fact]
    public void RampCanFall()
    {
        // Loan rate: 1.5 % at 1 star, 0.5 % at 5 stars.
        Assert.Equal(1f, EconomyMath.Ramp(3f, 1f, 1.5f, 5f, 0.5f), 3);
    }

    [Theory]
    [InlineData(20_000, 1f, 200)]
    [InlineData(20_000, 0.3f, 60)]
    [InlineData(150, 1f, 2)]
    [InlineData(0, 1f, 0)]
    [InlineData(-5_000, 2f, 0)]
    [InlineData(5_000, 0f, 0)]
    public void PercentChargeRoundsToTheDollar(int amount, float percent, int expected)
    {
        Assert.Equal(expected, EconomyMath.PercentCharge(amount, percent));
    }

    [Theory]
    [InlineData(100_000, 5f, 1_000, 5_000)]
    [InlineData(10_000, 5f, 1_000, 1_000)]
    [InlineData(600, 5f, 1_000, 600)]
    [InlineData(0, 5f, 1_000, 0)]
    public void RepaymentHasAMinimumAndNeverExceedsTheLoan(int loan, float percent, int minimum, int expected)
    {
        Assert.Equal(expected, EconomyMath.Repayment(loan, percent, minimum));
    }
}
