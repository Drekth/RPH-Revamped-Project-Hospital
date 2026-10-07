using System.Globalization;
using System.Text;

namespace RPH.Core.Formatting
{
    /// <summary>Money and percentage text shown in the statistics window.</summary>
    public static class MoneyFormat
    {
        /// <summary>"+$12 345", "-$500", "-" for zero.</summary>
        public static string SignedMoney(long amount)
        {
            if (amount == 0)
            {
                return "-";
            }
            return (amount > 0 ? "+$" : "-$") + Group(amount < 0 ? -amount : amount);
        }

        /// <summary>"$12 345" or "-$12 345".</summary>
        public static string Money(long amount)
        {
            return (amount < 0 ? "-$" : "$") + Group(amount < 0 ? -amount : amount);
        }

        /// <summary>"+$48K", "-$9K": for chart labels.</summary>
        public static string ShortMoney(long amount)
        {
            long abs = amount < 0 ? -amount : amount;
            string sign = amount > 0 ? "+$" : amount < 0 ? "-$" : "$";
            if (abs >= 1000000)
            {
                return sign + (abs / 100000 / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            }
            if (abs >= 1000)
            {
                return sign + (abs / 1000) + "K";
            }
            return sign + abs;
        }

        public static string Percent(long part, long total)
        {
            if (total == 0)
            {
                return string.Empty;
            }
            double value = 100.0 * part / total;
            return value > 0 && value < 1 ? "<1%" : ((int)System.Math.Round(value)) + "%";
        }

        private static string Group(long value)
        {
            string digits = value.ToString(CultureInfo.InvariantCulture);
            var builder = new StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                {
                    builder.Append(' ');
                }
                builder.Append(digits[i]);
            }
            return builder.ToString();
        }
    }
}
