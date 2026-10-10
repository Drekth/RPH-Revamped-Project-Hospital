using System;
using HarmonyLib;
using Lopital;

namespace RPH.Mod.Economy
{
    /// <summary>Called exactly once when a bedside / room examination finishes, before the queue is cleared.</summary>
    [HarmonyPatch(typeof(BehaviorPatient), nameof(BehaviorPatient.UncoverSymptomsFromLastExamination))]
    internal static class ExamBillingFinishedPatch
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
                ExamBilling.Bill(
                    procedures.m_state.m_procedureQueue.m_activeExamination.Entry,
                    __instance.m_entity,
                    RPHGameData.ExaminingDepartment(__instance, procedureScript));
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(ExamBillingFinishedPatch), e);
            }
        }
    }

    /// <summary>A lab procedure holds the sampling exam: bill its testing exam, once the analysis is done.</summary>
    [HarmonyPatch(typeof(LabProcedure), nameof(LabProcedure.Finish))]
    internal static class ExamBillingLabFinishedPatch
    {
        private static void Postfix(LabProcedure __instance)
        {
            try
            {
                GameDBExamination sampling = __instance.m_state.m_examination.Entry;
                if (sampling == null || sampling.LabTestingExaminationRef == null)
                {
                    return;
                }
                ExamBilling.Bill(
                    sampling.LabTestingExaminationRef.Entry,
                    __instance.m_state.m_patient.GetEntity(),
                    RPHGameData.LabDepartment(__instance));
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(ExamBillingLabFinishedPatch), e);
            }
        }
    }
}
