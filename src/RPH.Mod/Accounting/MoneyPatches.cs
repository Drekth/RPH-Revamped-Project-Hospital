using System;
using GLib;
using HarmonyLib;
using Lopital;
using RPH.Core.Accounting;

namespace RPH.Mod.Accounting
{
    // Every balance change of the game goes through Hospital.Pay / Hospital.Spend, plus the loan buttons.
    // The patches only observe: they compare the balance before and after, and never change game values.

    [HarmonyPatch(typeof(Hospital), nameof(Hospital.Pay))]
    internal static class HospitalPayPatch
    {
        private static void Prefix(out long __state)
        {
            __state = RPHLedgerService.Balance;
        }

        private static void Postfix(long __state, PaymentCategory category)
        {
            try
            {
                RPHLedgerService.Recorder.OnBalanceChanged(RPHLedgerService.Balance - __state, Map(category), RPHLedgerService.CurrentShift);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(HospitalPayPatch), e);
            }
        }

        internal static LedgerCategory Map(PaymentCategory category)
        {
            switch (category)
            {
                case PaymentCategory.INSURANCE_CLINIC: return LedgerCategory.InsuranceClinic;
                case PaymentCategory.INSURANCE_HOSPITALIZED: return LedgerCategory.InsuranceHospitalization;
                case PaymentCategory.FOOD: return LedgerCategory.Food;
                case PaymentCategory.INSURANCE_OBJECTIVES: return LedgerCategory.InsuranceObjectives;
                case PaymentCategory.EVENTS: return LedgerCategory.EventIncome;
                case PaymentCategory.BUILDING_REFUNDS: return LedgerCategory.Building;
                default: return LedgerCategory.Unknown;
            }
        }
    }

    [HarmonyPatch(typeof(Hospital), nameof(Hospital.Spend))]
    internal static class HospitalSpendPatch
    {
        private static void Prefix(out long __state)
        {
            __state = RPHLedgerService.Balance;
        }

        private static void Postfix(long __state, ExpenseCategory category)
        {
            try
            {
                RPHLedgerService.Recorder.OnBalanceChanged(RPHLedgerService.Balance - __state, Map(category), RPHLedgerService.CurrentShift);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(HospitalSpendPatch), e);
            }
        }

        internal static LedgerCategory Map(ExpenseCategory category)
        {
            switch (category)
            {
                case ExpenseCategory.BUILDING: return LedgerCategory.Building;
                case ExpenseCategory.HIRING_AGENCY: return LedgerCategory.HiringAgency;
                case ExpenseCategory.LOAN_INTEREST: return LedgerCategory.LoanInterest;
                case ExpenseCategory.AMBULANCES: return LedgerCategory.Ambulances;
                case ExpenseCategory.PENALTIES: return LedgerCategory.Penalties;
                case ExpenseCategory.EVENTS: return LedgerCategory.EventExpense;
                case ExpenseCategory.WAGES: return LedgerCategory.Wages;
                case ExpenseCategory.TRAINING: return LedgerCategory.Training;
                default: return LedgerCategory.Unknown;
            }
        }
    }

    /// <summary>Department.Pay knows the department and the patient: wrap Hospital.Pay in a scope.</summary>
    [HarmonyPatch(typeof(Department), nameof(Department.Pay))]
    internal static class DepartmentPayPatch
    {
        private static void Prefix(Department __instance, Entity character, out bool __state)
        {
            __state = false;
            try
            {
                RPHLedgerService.Recorder.BeginScope(RPHGameData.DepartmentId(__instance), RPHGameData.PatientDiagnosisId(character));
                __state = true;
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(DepartmentPayPatch), e);
            }
        }

        // A finalizer runs even if the game throws, so the scope is always closed.
        private static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state)
            {
                try
                {
                    RPHLedgerService.Recorder.EndScope(RPHLedgerService.CurrentShift, null);
                }
                catch (Exception e)
                {
                    RPHLog.PatchError(nameof(DepartmentPayPatch), e);
                }
            }
            return __exception;
        }
    }

    /// <summary>
    /// Wages of one employee (private Department.PayWages overload). The game splits them into clinic and
    /// ward wages by the employee's home room: read which daily counter grew to book the same split.
    /// </summary>
    [HarmonyPatch]
    internal static class DepartmentPayWagesPatch
    {
        internal sealed class State
        {
            public int Clinic;
            public int Hospitalization;
        }

        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Department), "PayWages", new[] { typeof(EntityIDPointer<Entity>), typeof(Shift) });
        }

        private static void Prefix(Department __instance, EntityIDPointer<Entity> character, out State __state)
        {
            __state = null;
            try
            {
                DailyStatistics today = __instance.m_departmentPersistentData.m_todaysStatistics;
                RPHLedgerService.Recorder.BeginScope(RPHGameData.DepartmentId(__instance), RPHGameData.EmployeeRole(character.GetEntity()));
                __state = new State { Clinic = today.m_wagesClinic, Hospitalization = today.m_wagesHospitalization };
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(DepartmentPayWagesPatch), e);
            }
        }

        private static Exception Finalizer(Exception __exception, Department __instance, State __state)
        {
            if (__state != null)
            {
                try
                {
                    DailyStatistics today = __instance.m_departmentPersistentData.m_todaysStatistics;
                    LedgerCategory category = today.m_wagesClinic != __state.Clinic ? LedgerCategory.WagesClinic
                        : today.m_wagesHospitalization != __state.Hospitalization ? LedgerCategory.WagesHospitalization
                        : LedgerCategory.Wages;
                    RPHLedgerService.Recorder.EndScope(RPHLedgerService.CurrentShift, category);
                }
                catch (Exception e)
                {
                    RPHLog.PatchError(nameof(DepartmentPayWagesPatch), e);
                }
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Budget), nameof(Budget.IncreaseLoan))]
    internal static class BudgetIncreaseLoanPatch
    {
        private static void Prefix(Budget __instance, out long __state)
        {
            __state = __instance.m_currentBalance;
        }

        private static void Postfix(Budget __instance, long __state)
        {
            LoanPatches.Record(__instance.m_currentBalance - __state, nameof(BudgetIncreaseLoanPatch));
        }
    }

    [HarmonyPatch(typeof(Budget), nameof(Budget.DecreaseLoan))]
    internal static class BudgetDecreaseLoanPatch
    {
        private static void Prefix(Budget __instance, out long __state)
        {
            __state = __instance.m_currentBalance;
        }

        private static void Postfix(Budget __instance, long __state)
        {
            LoanPatches.Record(__instance.m_currentBalance - __state, nameof(BudgetDecreaseLoanPatch));
        }
    }

    internal static class LoanPatches
    {
        public static void Record(long delta, string patch)
        {
            try
            {
                if (delta != 0)
                {
                    RPHLedgerService.Recorder.OnBalanceChanged(delta, delta > 0 ? LedgerCategory.LoanTaken : LedgerCategory.LoanRepaid, RPHLedgerService.CurrentShift);
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(patch, e);
            }
        }
    }

    /// <summary>The game closes its statistics day here (after the midnight payments): our accounting day follows it.</summary>
    [HarmonyPatch(typeof(Hospital), nameof(Hospital.SwitchToNewStatistics))]
    internal static class HospitalSwitchToNewStatisticsPatch
    {
        private static void Prefix()
        {
            try
            {
                RPHLedgerService.OnDayEnding();
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(HospitalSwitchToNewStatisticsPatch), e);
            }
        }

        private static void Postfix()
        {
            try
            {
                RPHLedgerService.OnDayStarted();
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(HospitalSwitchToNewStatisticsPatch), e);
            }
        }
    }
}
