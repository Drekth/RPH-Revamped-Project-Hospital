using System;
using GLib;
using HarmonyLib;
using Lopital;
using RPH.Core.Accounting;

namespace RPH.Mod.Accounting
{
    // Counts finished examinations, lab tests and treatments per department.
    // In the base game they cost the hospital nothing; the counts prepare the price list of phase 3.

    /// <summary>Called exactly once when a bedside / room examination finishes, before the queue is cleared.</summary>
    [HarmonyPatch(typeof(BehaviorPatient), nameof(BehaviorPatient.UncoverSymptomsFromLastExamination))]
    internal static class ExaminationFinishedPatch
    {
        private static void Postfix(BehaviorPatient __instance, ProcedureScript procedureScript)
        {
            try
            {
                ProcedureComponent procedures = __instance.m_entity.GetComponent<ProcedureComponent>();
                if (procedures == null)
                {
                    return;
                }
                GameDBExamination examination = procedures.m_state.m_procedureQueue.m_activeExamination.Entry;
                if (examination == null)
                {
                    return;
                }
                Entity employee = procedureScript != null && procedureScript.m_stateData.m_procedureScene != null
                    ? procedureScript.m_stateData.m_procedureScene.EmployeeCharacter
                    : null;
                string department = employee != null
                    ? RPHGameData.EmployeeDepartmentId(employee)
                    : RPHGameData.DepartmentId(__instance.GetDepartment());
                RPHLedgerService.Recorder.RecordActivity(ActivityKind.Examination, examination.DatabaseID.ToString(), department);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(ExaminationFinishedPatch), e);
            }
        }
    }

    [HarmonyPatch(typeof(LabProcedure), nameof(LabProcedure.Finish))]
    internal static class LabProcedureFinishedPatch
    {
        private static void Postfix(LabProcedure __instance)
        {
            try
            {
                GameDBExamination examination = __instance.m_state.m_examination.Entry;
                if (examination == null)
                {
                    return;
                }
                Room lab = __instance.m_state.m_statLab.GetEntity();
                Department department = lab != null ? lab.m_roomPersistentData.m_department.GetEntity() : null;
                if (department == null)
                {
                    Entity patient = __instance.m_state.m_patient.GetEntity();
                    BehaviorPatient behavior = patient != null ? patient.GetComponent<BehaviorPatient>() : null;
                    department = behavior != null ? behavior.GetDepartment() : null;
                }
                RPHLedgerService.Recorder.RecordActivity(ActivityKind.LabExamination, examination.DatabaseID.ToString(), RPHGameData.DepartmentId(department));
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(LabProcedureFinishedPatch), e);
            }
        }
    }

    /// <summary>
    /// ProcedureComponent.Update ends a treatment script when it becomes idle. The prefix notes the treatment
    /// (a few field reads per patient), the postfix books it if the script was really cleared.
    /// </summary>
    [HarmonyPatch(typeof(ProcedureComponent), nameof(ProcedureComponent.Update))]
    internal static class TreatmentFinishedPatch
    {
        private static void Prefix(ProcedureComponent __instance, out GameDBTreatment __state)
        {
            __state = null;
            try
            {
                ProcedureScript script = __instance.m_state.m_currentProcedureScript.GetEntity();
                if (script == null || !script.IsIdle() || __instance.m_state.m_procedureQueue.m_activeExamination.Entry != null)
                {
                    return;
                }
                __state = script.m_stateData.m_treatment.Entry;
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(TreatmentFinishedPatch), e);
            }
        }

        private static void Postfix(ProcedureComponent __instance, GameDBTreatment __state)
        {
            if (__state == null)
            {
                return;
            }
            try
            {
                if (__instance.m_state.m_currentProcedureScript.GetEntity() != null)
                {
                    return;
                }
                BehaviorPatient patient = __instance.m_entity.GetComponent<BehaviorPatient>();
                string department = patient != null ? RPHGameData.DepartmentId(patient.GetDepartment()) : string.Empty;
                ActivityKind kind = __state.TreatmentType == TreatmentType.SURGERY ? ActivityKind.Surgery : ActivityKind.Treatment;
                RPHLedgerService.Recorder.RecordActivity(kind, __state.DatabaseID.ToString(), department);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(TreatmentFinishedPatch), e);
            }
        }
    }
}
