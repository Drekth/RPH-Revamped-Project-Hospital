using System;
using GLib;
using HarmonyLib;
using Lopital;
using RPH.Core.Accounting;
using RPH.Mod.Accounting;

namespace RPH.Mod.HR
{
    /// <summary>A porter's desk is in a porters' room, not in a nurses' station.</summary>
    [HarmonyPatch(typeof(BehaviorNurse), nameof(BehaviorNurse.GetWorkspaceRoomTag))]
    internal static class PorterWorkspacePatch
    {
        private static void Postfix(BehaviorNurse __instance, ref string __result)
        {
            try
            {
                if (PorterStaff.IsPorter(__instance.m_entity))
                {
                    __result = Porters.RoomTag;
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(PorterWorkspacePatch), e);
            }
        }
    }

    /// <summary>The game computes the wage again every frame: a porter's is a share of what a nurse of his level gets.</summary>
    [HarmonyPatch(typeof(EmployeeComponent), nameof(EmployeeComponent.ComputeSalary))]
    internal static class PorterSalaryPatch
    {
        private static void Postfix(EmployeeComponent __instance)
        {
            try
            {
                if (PorterStaff.IsPorter(__instance.m_entity))
                {
                    PorterStaff.ScaleSalary(__instance);
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(PorterSalaryPatch), e);
            }
        }
    }

    /// <summary>The department's dress code is for its nurses: porters keep their own tunic.</summary>
    [HarmonyPatch(typeof(EmployeeComponent), nameof(EmployeeComponent.SwitchDressCodeColors))]
    internal static class PorterUniformPatch
    {
        private static bool Prefix(EmployeeComponent __instance)
        {
            try
            {
                if (PorterStaff.IsPorter(__instance.m_entity))
                {
                    PorterStaff.ApplyUniform(__instance.m_entity);
                    return false;
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(PorterUniformPatch), e);
            }
            return true;
        }
    }

    /// <summary>
    /// Wages of a porter (private Department.PayWages overload): same amount and same department counters as the game,
    /// but booked in the ledger for the whole hospital, not for the department that hosts his room.
    /// </summary>
    [HarmonyPatch]
    internal static class PorterWagesPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Department), "PayWages", new[] { typeof(EntityIDPointer<Entity>), typeof(Shift) });
        }

        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Department __instance, EntityIDPointer<Entity> character, Shift shift)
        {
            try
            {
                Entity entity = character.GetEntity();
                if (!PorterStaff.IsPorter(entity))
                {
                    return true;
                }
                EmployeeComponent employee = entity.GetComponent<EmployeeComponent>();
                if (employee.m_state.m_shift == shift)
                {
                    __instance.m_departmentPersistentData.m_todaysStatistics.m_wagesClinic += employee.m_state.m_salary;
                    RPHLedgerService.Spend(employee.m_state.m_salary, ExpenseCategory.WAGES, string.Empty, PorterStaff.ExpenseSource, LedgerCategory.Wages);
                }
                return false;
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(PorterWagesPatch), e);
                return true;
            }
        }
    }

    /// <summary>
    /// The one place where the game picks who moves a patient on a stretcher or wheelchair (a free nurse with the transfers
    /// role, when the caller names nobody). A free porter of the patient's floor comes first. The stretcher is looked for in
    /// the department of the person who comes, so the porter belongs to the patient's department while the call runs.
    /// </summary>
    [HarmonyPatch(typeof(StretcherComponent), nameof(StretcherComponent.SetDestination),
        typeof(TileObject), typeof(LyingState), typeof(StretcherUse), typeof(BedReservation), typeof(Entity), typeof(MovementType), typeof(Vehicle))]
    internal static class PorterDispatchPatch
    {
        internal sealed class State
        {
            public EmployeeComponent Porter;
            public EntityIDPointer<Department> Department;
        }

        private static void Prefix(StretcherComponent __instance, ref Entity movingCharacter, out State __state)
        {
            __state = null;
            try
            {
                if (movingCharacter != null || !PorterStaff.HasPorters || !Porters.Enabled
                    || (__instance.m_state.m_stretcherPatientSide != null && __instance.m_state.m_movingCharacter != null))
                {
                    return;
                }
                Entity porter = PorterStaff.FindFor(__instance.m_entity);
                if (porter == null)
                {
                    return;
                }
                EmployeeComponent employee = porter.GetComponent<EmployeeComponent>();
                __state = new State { Porter = employee, Department = employee.m_state.m_department };
                employee.m_state.m_department = __instance.m_entity.GetComponent<Behavior>().GetDepartment();
                movingCharacter = porter;
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(PorterDispatchPatch), e);
            }
        }

        // A finalizer runs even if the game throws: the porter must always get his department back.
        private static Exception Finalizer(Exception __exception, State __state)
        {
            if (__state != null)
            {
                __state.Porter.m_state.m_department = __state.Department;
            }
            return __exception;
        }
    }

    // Like the ledger, the porters live next to the save.

    [HarmonyPatch(typeof(GameSave), nameof(GameSave.Save))]
    internal static class PorterSavePatch
    {
        private static void Postfix(string path)
        {
            try
            {
                PorterStaff.OnGameSaved(path);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(PorterSavePatch), e);
            }
        }
    }

    [HarmonyPatch(typeof(GameSave), nameof(GameSave.Load))]
    internal static class PorterLoadPatch
    {
        private static void Postfix(string filepath)
        {
            try
            {
                PorterStaff.OnGameLoading(filepath);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(PorterLoadPatch), e);
            }
        }
    }
}
