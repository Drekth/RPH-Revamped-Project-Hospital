using RPH.Core.Formatting;
using Xunit;

namespace RPH.Core.Tests;

public class MoneyFormatTests
{
    [Theory]
    [InlineData(0, "-")]
    [InlineData(117869, "+$117 869")]
    [InlineData(-50104, "-$50 104")]
    [InlineData(-142, "-$142")]
    [InlineData(1000000, "+$1 000 000")]
    public void SignedMoney(long amount, string expected) => Assert.Equal(expected, MoneyFormat.SignedMoney(amount));

    [Theory]
    [InlineData(181144, "$181 144")]
    [InlineData(-3000, "-$3 000")]
    [InlineData(0, "$0")]
    public void Money(long amount, string expected) => Assert.Equal(expected, MoneyFormat.Money(amount));

    [Theory]
    [InlineData(46, 100, "46%")]
    [InlineData(1, 1000, "<1%")]
    [InlineData(0, 100, "0%")]
    [InlineData(5, 0, "")]
    public void Percent(long part, long total, string expected) => Assert.Equal(expected, MoneyFormat.Percent(part, total));
}
