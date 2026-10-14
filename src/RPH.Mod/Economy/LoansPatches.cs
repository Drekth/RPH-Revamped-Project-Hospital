using System;
using HarmonyLib;
using Lopital;
using RPH.Core.Formatting;
using UnityEngine.UI;

namespace RPH.Mod.Economy
{
    /// <summary>The game charges loan interest once a day (Hospital.OnDayStart): replace its 1 % with our rate.</summary>
    [HarmonyPatch(typeof(Hospital), nameof(Hospital.Spend))]
    internal static class LoanInterestPatch
    {
        private static void Prefix(ref int amount, ExpenseCategory category)
        {
            try
            {
                if (category == ExpenseCategory.LOAN_INTEREST && Loans.Enabled)
                {
                    amount = Loans.DailyInterest();
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(LoanInterestPatch), e);
            }
        }
    }

    /// <summary>The Budget tab shows the next interest charge as loan / 100: show ours. Runs only while the tab is open.</summary>
    [HarmonyPatch(typeof(HospitalManagementBudgetController), "UpdateLoans")]
    internal static class LoanInterestTextPatch
    {
        private static void Postfix(HospitalManagementBudgetController __instance)
        {
            try
            {
                if (Loans.Enabled)
                {
                    __instance.m_textInterest.GetComponent<Text>().text = MoneyFormat.Money(-Loans.DailyInterest());
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(LoanInterestTextPatch), e);
            }
        }
    }
}
