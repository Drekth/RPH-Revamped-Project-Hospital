using System;

namespace RPH.Core.Accounting
{
    public enum RangeKind
    {
        Today,
        Yesterday,
        Last7Days,
        Last30Days,
        AllDays,
        SingleDay,
    }

    /// <summary>Resolves a period choice into a day interval.</summary>
    public struct DayRange
    {
        public readonly int From;
        public readonly int To;

        public DayRange(int from, int to)
        {
            From = Math.Min(from, to);
            To = Math.Max(from, to);
        }

        public bool Contains(int day)
        {
            return day >= From && day <= To;
        }

        /// <param name="today">The current accounting day.</param>
        /// <param name="firstDay">The first day of the game (1) or of the ledger.</param>
        /// <param name="singleDay">Day used by <see cref="RangeKind.SingleDay"/>.</param>
        public static DayRange Resolve(RangeKind kind, int today, int firstDay, int singleDay)
        {
            firstDay = Math.Max(1, Math.Min(firstDay, today));
            switch (kind)
            {
                case RangeKind.Today:
                    return new DayRange(today, today);
                case RangeKind.Yesterday:
                    int yesterday = Math.Max(firstDay, today - 1);
                    return new DayRange(yesterday, yesterday);
                case RangeKind.Last7Days:
                    return new DayRange(Math.Max(firstDay, today - 6), today);
                case RangeKind.Last30Days:
                    return new DayRange(Math.Max(firstDay, today - 29), today);
                case RangeKind.AllDays:
                    return new DayRange(firstDay, today);
                default:
                    int day = Math.Max(firstDay, Math.Min(today, singleDay));
                    return new DayRange(day, day);
            }
        }
    }
}
