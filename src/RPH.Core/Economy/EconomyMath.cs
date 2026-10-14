using System;

namespace RPH.Core.Economy
{
    /// <summary>Small formulas shared by the economy features. Inputs come from the XML tweakables.</summary>
    public static class EconomyMath
    {
        /// <summary>
        /// The value at <paramref name="x"/> on the line from (x0, y0) to (x1, y1): y0 before x0, y1 after x1.
        /// Used for "value by renown" curves; y can grow or fall.
        /// </summary>
        public static float Ramp(float x, float x0, float y0, float x1, float y1)
        {
            if (x <= x0)
            {
                return y0;
            }
            if (x >= x1)
            {
                return y1;
            }
            return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
        }

        /// <summary>One day of a charge of <paramref name="percent"/> % on <paramref name="amount"/>, rounded to the dollar. 0 for nothing owed.</summary>
        public static int PercentCharge(int amount, float percent)
        {
            if (amount <= 0 || percent <= 0f)
            {
                return 0;
            }
            return (int)Math.Round(amount * (double)percent / 100.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Today's loan repayment: <paramref name="percent"/> % of what is owed, at least <paramref name="minimum"/>, never more than the loan.</summary>
        public static int Repayment(int loan, float percent, int minimum)
        {
            if (loan <= 0)
            {
                return 0;
            }
            return Math.Min(Math.Max(PercentCharge(loan, percent), minimum), loan);
        }
    }
}
