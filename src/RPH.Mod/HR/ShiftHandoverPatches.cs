using System;
using GLib;
using HarmonyLib;
using Lopital;

namespace RPH.Mod.HR
{
    /// <summary>Midnight: each employee on time draws how early they leave home for tomorrow's shift.</summary>
    [HarmonyPatch(typeof(EmployeeComponent), nameof(EmployeeComponent.OnDayStart))]
    internal static class HandoverEarlyCommutePatch
    {
        private static void Postfix(EmployeeComponent __instance)
        {
            try
            {
                ShiftHandover.SetEarlyCommute(__instance);
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(HandoverEarlyCommutePatch), e);
            }
        }
    }

    /// <summary>
    /// A doctor who arrives for the next shift waits until the doctor of the desk is gone (the only caller of this method).
    /// Nurses and lab specialists never wait: nobody waits any more, the handover happens at the desk.
    /// </summary>
    [HarmonyPatch(typeof(EmployeeComponent), nameof(EmployeeComponent.GetOppositeShiftEmployee))]
    internal static class HandoverNoWaitPatch
    {
        private static void Postfix(ref Entity __result)
        {
            try
            {
                if (ShiftHandover.Enabled)
                {
                    __result = null;
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(nameof(HandoverNoWaitPatch), e);
            }
        }
    }

    /// <summary>While an employee who must cover is updated, the game believes their shift is still on.</summary>
    [HarmonyPatch(typeof(DayTime), nameof(DayTime.GetShift))]
    internal static class HandoverCoverShiftPatch
    {
        private static void Postfix(ref Shift __result)
        {
            if (ShiftHandover.Covering.HasValue)
            {
                __result = ShiftHandover.Covering.Value;
            }
        }
    }

    /// <summary>Common part of the three Idle patches: ask whether the employee has to stay, answer through <see cref="ShiftHandover.Covering"/>.</summary>
    internal static class HandoverIdle
    {
        public static void Begin(Entity entity, string patch)
        {
            try
            {
                if (ShiftHandover.MustCover(entity))
                {
                    ShiftHandover.Covering = entity.GetComponent<EmployeeComponent>().m_state.m_shift;
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(patch, e);
            }
        }

        public static void StateChanged(Entity entity, bool arrivedFromCommute, bool wentHome, string patch)
        {
            try
            {
                if (arrivedFromCommute)
                {
                    ShiftHandover.OnArrivedEarly(entity);
                }
                else if (wentHome)
                {
                    ShiftHandover.OnLeftLate(entity);
                }
            }
            catch (Exception e)
            {
                RPHLog.PatchError(patch, e);
            }
        }
    }

    [HarmonyPatch(typeof(BehaviorDoctor), "UpdateStateIdle")]
    internal static class DoctorHandoverIdlePatch
    {
        private static void Prefix(BehaviorDoctor __instance)
        {
            HandoverIdle.Begin(__instance.m_entity, nameof(DoctorHandoverIdlePatch));
        }

        // A finalizer runs even if the game throws: the shift override must never leak to other code.
        private static Exception Finalizer(Exception __exception)
        {
            ShiftHandover.Covering = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(BehaviorNurse), "UpdateStateIdle")]
    internal static class NurseHandoverIdlePatch
    {
        private static void Prefix(BehaviorNurse __instance)
        {
            HandoverIdle.Begin(__instance.m_entity, nameof(NurseHandoverIdlePatch));
        }

        private static Exception Finalizer(Exception __exception)
        {
            ShiftHandover.Covering = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(BehaviorLabSpecialist), "UpdateStateIdle")]
    internal static class LabHandoverIdlePatch
    {
        private static void Prefix(BehaviorLabSpecialist __instance)
        {
            HandoverIdle.Begin(__instance.m_entity, nameof(LabHandoverIdlePatch));
        }

        private static Exception Finalizer(Exception __exception)
        {
            ShiftHandover.Covering = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(BehaviorDoctor), nameof(BehaviorDoctor.SwitchState))]
    internal static class DoctorHandoverStatePatch
    {
        private static void Prefix(BehaviorDoctor __instance, out DoctorState __state)
        {
            __state = __instance.m_state.m_doctorState;
        }

        private static void Postfix(BehaviorDoctor __instance, DoctorState __state, DoctorState state)
        {
            HandoverIdle.StateChanged(__instance.m_entity,
                __state == DoctorState.Commuting && state == DoctorState.GoingToWorkPlace,
                __state != DoctorState.GoingHome && state == DoctorState.GoingHome,
                nameof(DoctorHandoverStatePatch));
        }
    }

    [HarmonyPatch(typeof(BehaviorNurse), nameof(BehaviorNurse.SwitchState))]
    internal static class NurseHandoverStatePatch
    {
        private static void Prefix(BehaviorNurse __instance, out NurseState __state)
        {
            __state = __instance.m_state.m_nurseState;
        }

        private static void Postfix(BehaviorNurse __instance, NurseState __state, NurseState state)
        {
            HandoverIdle.StateChanged(__instance.m_entity,
                __state == NurseState.Commuting && state == NurseState.GoingToWorkplace,
                __state != NurseState.GoingHome && state == NurseState.GoingHome,
                nameof(NurseHandoverStatePatch));
        }
    }

    [HarmonyPatch(typeof(BehaviorLabSpecialist), nameof(BehaviorLabSpecialist.SwitchState))]
    internal static class LabHandoverStatePatch
    {
        private static void Prefix(BehaviorLabSpecialist __instance, out LabSpecialistState __state)
        {
            __state = __instance.m_state.m_labSpecialistState;
        }

        private static void Postfix(BehaviorLabSpecialist __instance, LabSpecialistState __state, LabSpecialistState state)
        {
            HandoverIdle.StateChanged(__instance.m_entity,
                __state == LabSpecialistState.Commuting && state == LabSpecialistState.GoingToWorkplace,
                __state != LabSpecialistState.GoingHome && state == LabSpecialistState.GoingHome,
                nameof(LabHandoverStatePatch));
        }
    }
}
