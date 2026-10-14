using System;
using HarmonyLib;
using Lopital;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// Midnight: the game charges its daily costs here (loan interest, ambulance upkeep), then closes the day's
    /// statistics. Our daily billing runs right after, in this order, so the overdraft is charged on the final balance.
    /// </summary>
    [HarmonyPatch(typeof(Hospital), nameof(Hospital.OnDayStart))]
    internal static class MidnightPatch
    {
        private static void Postfix()
        {
            try
            {
                InpatientDays.BillDay();
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(MidnightPatch) + "/" + nameof(InpatientDays), e);
            }
            try
            {
                Loans.OnMidnight();
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(MidnightPatch) + "/" + nameof(Loans), e);
            }
        }
    }
}
