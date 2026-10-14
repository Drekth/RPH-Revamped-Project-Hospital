using System;

namespace RPH.Core.HR
{
    /// <summary>Time and pay formulas of the shift handover. Hours are game clock hours (0-24), inputs come from the XML tweakables.</summary>
    public static class ShiftHandoverMath
    {
        private const float MinutesPerDay = 1440f;

        /// <summary>Minutes elapsed since the clock last passed <paramref name="boundaryHour"/>, in [0, 1440).</summary>
        public static float MinutesSince(float nowHour, float boundaryHour)
        {
            float minutes = (nowHour - boundaryHour) * 60f % MinutesPerDay;
            return minutes < 0f ? minutes + MinutesPerDay : minutes;
        }

        /// <summary>Minutes until the clock next passes <paramref name="boundaryHour"/>, in (0, 1440].</summary>
        public static float MinutesUntil(float nowHour, float boundaryHour)
        {
            float elapsed = MinutesSince(nowHour, boundaryHour);
            return elapsed == 0f ? MinutesPerDay : MinutesPerDay - elapsed;
        }

        /// <summary>Length of the shift that runs from <paramref name="startHour"/> to <paramref name="endHour"/> (wraps over midnight).</summary>
        public static float ShiftHours(float startHour, float endHour)
        {
            float hours = endHour - startHour;
            return hours > 0f ? hours : hours + 24f;
        }

        /// <summary>How long the outgoing employee may cover a late relief: a whole number of minutes in [min, max], fixed by <paramref name="seed"/>.</summary>
        public static int CoverLimit(float minMinutes, float maxMinutes, uint seed)
        {
            int min = (int)minMinutes;
            int max = Math.Max(min, (int)maxMinutes);
            return min + (int)(seed % (uint)(max - min + 1));
        }

        /// <summary>
        /// Whether the outgoing employee still has to stay at the post. Nobody to hand over to: no. Otherwise stay for the
        /// handover itself, then, while the relief is not in the building, up to the cover limit.
        /// </summary>
        public static bool MustCover(float minutesSinceShiftEnd, bool hasRelief, bool reliefArrived, float handoverMinutes, float coverLimitMinutes)
        {
            if (!hasRelief)
            {
                return false;
            }
            if (minutesSinceShiftEnd < handoverMinutes)
            {
                return true;
            }
            return !reliefArrived && minutesSinceShiftEnd < coverLimitMinutes;
        }

        /// <summary>
        /// Pay for <paramref name="minutes"/> worked outside the employee's shift: the shift's wage prorated by its length,
        /// times <paramref name="percent"/> % (100 = the normal hourly rate). 0 for no time or no pay.
        /// </summary>
        public static int OvertimePay(int shiftWage, float minutes, float shiftHours, float percent)
        {
            if (shiftWage <= 0 || minutes <= 0f || shiftHours <= 0f || percent <= 0f)
            {
                return 0;
            }
            return (int)Math.Round(shiftWage * (double)minutes / 60.0 / shiftHours * percent / 100.0, MidpointRounding.AwayFromZero);
        }
    }
}
