using System;
using HarmonyLib;
using Lopital;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// The hospital's own ambulance or helicopter has unloaded its patient onto a stretcher.
    /// City ambulances never come through here: their paramedic fetches the patient another way.
    /// </summary>
    [HarmonyPatch(typeof(Ambulance), nameof(Ambulance.SetupPatient))]
    internal static class AmbulanceFeesPatch
    {
        private static void Postfix(Ambulance __instance, bool __result)
        {
            try
            {
                if (__result && __instance.m_state.m_patient != null)
                {
                    AmbulanceFees.Bill(__instance, __instance.m_state.m_patient.GetEntity());
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(AmbulanceFeesPatch), e);
            }
        }
    }
}
