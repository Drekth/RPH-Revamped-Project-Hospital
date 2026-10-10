using System;
using GLib;
using HarmonyLib;
using Lopital;
using RPH.Core.Accounting;
using RPH.Mod.Accounting;

namespace RPH.Mod.Economy
{
    /// <summary>A bedside / room examination finished: every machine of its scene wears a little.</summary>
    [HarmonyPatch(typeof(BehaviorPatient), nameof(BehaviorPatient.UncoverSymptomsFromLastExamination))]
    internal static class MachineWearExaminationPatch
    {
        private static void Postfix(ProcedureScript procedureScript)
        {
            try
            {
                if (procedureScript == null || procedureScript.m_stateData.m_procedureScene == null)
                {
                    return;
                }
                foreach (EntityIDPointer<TileObject> equipment in procedureScript.m_stateData.m_procedureScene.m_equipment)
                {
                    if (equipment != null)
                    {
                        MachineMaintenance.OnUsed(equipment.GetEntity());
                    }
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(MachineWearExaminationPatch), e);
            }
        }
    }

    /// <summary>A lab analysis finished: its analyzer wears.</summary>
    [HarmonyPatch(typeof(LabProcedure), nameof(LabProcedure.Finish))]
    internal static class MachineWearLabPatch
    {
        private static void Postfix(LabProcedure __instance)
        {
            try
            {
                EntityIDPointer<TileObject> equipment = __instance.m_state.m_usedEquipment;
                if (equipment != null)
                {
                    MachineMaintenance.OnUsed(equipment.GetEntity());
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(MachineWearLabPatch), e);
            }
        }
    }

    /// <summary>The only caller of TileObject.Repair is the janitor finishing a service: the hospital pays for it.</summary>
    [HarmonyPatch(typeof(TileObject), nameof(TileObject.Repair))]
    internal static class MachineRepairPatch
    {
        private static void Prefix(TileObject __instance, out int __state)
        {
            __state = 0;
            try
            {
                __state = MachineMaintenance.RepairCost(__instance);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(MachineRepairPatch), e);
            }
        }

        private static void Postfix(TileObject __instance, int __state)
        {
            if (__state <= 0)
            {
                return;
            }
            bool scoped = false;
            try
            {
                RPHLedgerService.Recorder.BeginScope(
                    RPHGameData.DepartmentId(__instance.m_state.m_department.GetEntity()),
                    __instance.m_state.m_gameDBObject.Entry.DatabaseID.ToString());
                scoped = true;
                Hospital.Instance.Spend(__state, ExpenseCategory.PENALTIES);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(MachineRepairPatch), e);
            }
            finally
            {
                if (scoped)
                {
                    RPHLedgerService.Recorder.EndScope(RPHLedgerService.CurrentShift, LedgerCategory.Maintenance);
                }
            }
        }
    }
}
