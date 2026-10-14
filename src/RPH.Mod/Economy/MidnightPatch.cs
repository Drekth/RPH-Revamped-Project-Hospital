using System;
using HarmonyLib;
using Lopital;
using RPH.Mod.HR;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// Midnight: the game charges its daily costs here (loan interest, ambulance upkeep), then closes the day's
    /// statistics. Our daily billing runs right after, in this order, so the overdraft is charged on the final balance;
    /// then the daily balance log lines.
    /// </summary>
    [HarmonyPatch(typeof(Hospital), nameof(Hospital.OnDayStart))]
    internal static class MidnightPatch
    {
        private static void Postfix()
        {
            Run(InpatientDays.BillDay, nameof(InpatientDays));
            Run(Loans.OnMidnight, nameof(Loans));
            Run(RenownPatients.LogDay, nameof(RenownPatients));
            Run(ExamBilling.LogDay, nameof(ExamBilling));
            Run(ShiftHandover.LogDay, nameof(ShiftHandover));
        }

        /// <summary>One feature failing must not stop the others.</summary>
        private static void Run(Action step, string name)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(MidnightPatch) + "/" + name, e);
            }
        }
    }
}
