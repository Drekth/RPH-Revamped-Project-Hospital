using GLib;
using Lopital;
using RPH.Core.Accounting;
using RPH.Core.HR;
using RPH.Mod.Accounting;
using UnityEngine;

namespace RPH.Mod.HR
{
    /// <summary>
    /// Shift handover of doctors, nurses and lab specialists (mod/Database/HR/ShiftHandover.xml): staff arrive before their
    /// shift, the outgoing employee stays to hand over and covers a late relief, and the time spent at the post outside the
    /// shift is paid. The game already sends staff early (EmployeeComponent.m_commuteTime &lt; 0); this class sets how early,
    /// keeps the outgoing employee at the post, and books the extra time.
    /// </summary>
    internal static class ShiftHandover
    {
        private const string EnabledId = "RPH_HR_SHIFT_HANDOVER_ENABLED";
        private const string EarlyMinId = "RPH_HR_SHIFT_HANDOVER_EARLY_MIN";
        private const string EarlyMaxId = "RPH_HR_SHIFT_HANDOVER_EARLY_MAX";
        private const string HandoverMinutesId = "RPH_HR_SHIFT_HANDOVER_MINUTES";
        private const string CoverMinId = "RPH_HR_SHIFT_HANDOVER_COVER_MIN";
        private const string CoverMaxId = "RPH_HR_SHIFT_HANDOVER_COVER_MAX";
        private const string PayPercentId = "RPH_HR_SHIFT_HANDOVER_PAY_PERCENT";
        private const string MaxPaidId = "RPH_HR_SHIFT_HANDOVER_MAX_PAID_MINUTES";

        /// <summary>
        /// Set while the Idle update of one employee who must keep covering runs: DayTime.GetShift then answers with that
        /// employee's own shift, so the game's "my shift is over, go home" test fails (see ShiftHandoverPatches).
        /// </summary>
        public static Shift? Covering;

        private static int s_earlyCount;
        private static int s_earlyPaid;
        private static float s_earlyMinutes;
        private static int s_lateCount;
        private static int s_latePaid;
        private static float s_lateMinutes;

        public static bool Enabled
        {
            get
            {
                GameDBTweakableInt enabled = Database.Instance.GetEntry<GameDBTweakableInt>(EnabledId);
                return enabled != null && enabled.Value != 0 && !RPHCompatibility.ShiftHandoverModLoaded;
            }
        }

        private static float Value(string id)
        {
            GameDBTweakableFloat value = Database.Instance.GetEntry<GameDBTweakableFloat>(id);
            return value != null ? value.Value : 0f;
        }

        private static GameDBSchedule Staff
        {
            get { return Database.Instance.GetEntry<GameDBSchedule>("SCHEDULE_OPENING_HOURS_STAFF"); }
        }

        /// <summary>Same test as DayTime.GetShift, which the cover patch changes.</summary>
        private static Shift ShiftAt(float hour)
        {
            GameDBSchedule staff = Staff;
            return hour > staff.StartTime && hour < staff.EndTime ? Shift.DAY : Shift.NIGHT;
        }

        private static float StartOf(Shift shift)
        {
            return shift == Shift.DAY ? Staff.StartTime : Staff.EndTime;
        }

        private static float EndOf(Shift shift)
        {
            return shift == Shift.DAY ? Staff.EndTime : Staff.StartTime;
        }

        private static bool IsShiftStaff(Entity entity)
        {
            return entity.GetComponent<BehaviorDoctor>() != null
                || entity.GetComponent<BehaviorNurse>() != null
                || entity.GetComponent<BehaviorLabSpecialist>() != null;
        }

        /// <summary>Midnight: every employee on time leaves home a few minutes before their shift. Late commuters (perks) keep the game's delay.</summary>
        public static void SetEarlyCommute(EmployeeComponent employee)
        {
            if (!Enabled || employee.m_state.m_commuteTime > 0f || !IsShiftStaff(employee.m_entity))
            {
                return;
            }
            employee.m_state.m_commuteTime = -Random.Range(Value(EarlyMinId), Value(EarlyMaxId)) / 60f;
        }

        /// <summary>
        /// The outgoing employee has to stay at the post: the handover is not done, or the relief is late and the cover
        /// limit is not reached. Asked every frame by the Idle update of employees whose shift is over.
        /// </summary>
        public static bool MustCover(Entity entity)
        {
            if (!Enabled)
            {
                return false;
            }
            EmployeeComponent employee = entity.GetComponent<EmployeeComponent>();
            Shift shift = employee.m_state.m_shift;
            float now = DayTime.Instance.GetDayTimeHours();
            if (shift == Shift.ANY || shift == ShiftAt(now))
            {
                return false;
            }
            float elapsed = ShiftHandoverMath.MinutesSince(now, EndOf(shift));
            float handoverMinutes = Value(HandoverMinutesId);
            float coverMax = Value(CoverMaxId);
            if (elapsed >= handoverMinutes && elapsed >= coverMax)
            {
                return false;
            }
            Entity relief = ReliefOf(employee, shift);
            if (relief == null)
            {
                return false;
            }
            uint seed = entity.GetEntityID() + (uint)DayTime.Instance.GetDay() * 31u;
            float coverLimit = ShiftHandoverMath.CoverLimit(Value(CoverMinId), coverMax, seed);
            return ShiftHandoverMath.MustCover(elapsed, true, HasArrived(relief), handoverMinutes, coverLimit);
        }

        /// <summary>Whoever works the same desk in the other shift, if they can come.</summary>
        private static Entity ReliefOf(EmployeeComponent employee, Shift shift)
        {
            if (employee.m_state.m_workDesk == null)
            {
                return null;
            }
            TileObject desk = employee.m_state.m_workDesk.GetEntity();
            if (desk == null)
            {
                return null;
            }
            Entity relief = desk.GetWorkspaceOwner(shift == Shift.DAY ? Shift.NIGHT : Shift.DAY);
            EmployeeComponent reliefEmployee = relief != null ? relief.GetComponent<EmployeeComponent>() : null;
            return reliefEmployee != null && !reliefEmployee.IsFired() ? relief : null;
        }

        /// <summary>The relief is in the hospital: not at home, on the way home, commuting or in training.</summary>
        private static bool HasArrived(Entity relief)
        {
            BehaviorDoctor doctor = relief.GetComponent<BehaviorDoctor>();
            if (doctor != null)
            {
                DoctorState state = doctor.m_state.m_doctorState;
                return state != DoctorState.AtHome && state != DoctorState.FiredAtHome && state != DoctorState.GoingHome
                    && state != DoctorState.Commuting && state != DoctorState.Training;
            }
            BehaviorNurse nurse = relief.GetComponent<BehaviorNurse>();
            if (nurse != null)
            {
                NurseState state = nurse.m_state.m_nurseState;
                return state != NurseState.AtHome && state != NurseState.FiredAtHome && state != NurseState.GoingHome
                    && state != NurseState.Commuting && state != NurseState.Training;
            }
            BehaviorLabSpecialist lab = relief.GetComponent<BehaviorLabSpecialist>();
            if (lab != null)
            {
                LabSpecialistState state = lab.m_state.m_labSpecialistState;
                return state != LabSpecialistState.AtHome && state != LabSpecialistState.FiredAtHome && state != LabSpecialistState.GoingHome
                    && state != LabSpecialistState.Commuting && state != LabSpecialistState.Training;
            }
            return true;
        }

        /// <summary>An employee whose shift has not started leaves home for the post: the minutes until it starts are paid.</summary>
        public static void OnArrivedEarly(Entity entity)
        {
            if (!Enabled)
            {
                return;
            }
            EmployeeComponent employee = entity.GetComponent<EmployeeComponent>();
            Shift shift = employee.m_state.m_shift;
            float now = DayTime.Instance.GetDayTimeHours();
            if (shift == Shift.ANY || shift == ShiftAt(now))
            {
                return;
            }
            float minutes = ShiftHandoverMath.MinutesUntil(now, StartOf(shift));
            int paid = Pay(entity, employee, minutes);
            if (paid > 0)
            {
                s_earlyCount++;
                s_earlyPaid += paid;
                s_earlyMinutes += minutes;
            }
        }

        /// <summary>An employee whose shift is over goes home: the minutes since it ended are paid.</summary>
        public static void OnLeftLate(Entity entity)
        {
            if (!Enabled)
            {
                return;
            }
            EmployeeComponent employee = entity.GetComponent<EmployeeComponent>();
            Shift shift = employee.m_state.m_shift;
            float now = DayTime.Instance.GetDayTimeHours();
            if (shift == Shift.ANY || shift == ShiftAt(now) || employee.IsFired())
            {
                return;
            }
            float minutes = ShiftHandoverMath.MinutesSince(now, EndOf(shift));
            int paid = Pay(entity, employee, minutes);
            if (paid > 0)
            {
                s_lateCount++;
                s_latePaid += paid;
                s_lateMinutes += minutes;
            }
        }

        /// <summary>Pays minutes worked outside the shift. Beyond the paid maximum it is no handover (overtime, closed department): nothing.</summary>
        private static int Pay(Entity entity, EmployeeComponent employee, float minutes)
        {
            if (minutes <= 0f || minutes > Value(MaxPaidId))
            {
                return 0;
            }
            Shift shift = employee.m_state.m_shift;
            int amount = ShiftHandoverMath.OvertimePay(employee.m_state.m_salary, minutes, ShiftHandoverMath.ShiftHours(StartOf(shift), EndOf(shift)), Value(PayPercentId));
            if (amount <= 0)
            {
                return 0;
            }
            Department department = employee.m_state.m_department.GetEntity();
            if (department != null)
            {
                BookDepartmentWages(department, employee, amount);
            }
            RPHLedgerService.Recorder.BeginScope(RPHGameData.DepartmentId(department), RPHGameData.EmployeeRole(entity));
            try
            {
                Hospital.Instance.Spend(amount, ExpenseCategory.WAGES);
            }
            finally
            {
                RPHLedgerService.Recorder.EndScope(shift == Shift.DAY ? WorkShift.Day : WorkShift.Night, LedgerCategory.Handover);
            }
            return amount;
        }

        /// <summary>The game's wage counters of the department (what its Budget tab shows), split clinic / ward as Department.PayWages does.</summary>
        private static void BookDepartmentWages(Department department, EmployeeComponent employee, int amount)
        {
            DailyStatistics today = department.m_departmentPersistentData.m_todaysStatistics;
            if (employee.m_state.m_homeRoom != null
                && !Department.IsRoomClinicRoom(department.m_departmentPersistentData.m_departmentType.Entry, employee.m_state.m_homeRoom.GetEntity().m_roomPersistentData.m_roomType.Entry))
            {
                today.m_wagesHospitalization += amount;
            }
            else
            {
                today.m_wagesClinic += amount;
            }
        }

        /// <summary>Midnight: what the handover cost since the last midnight.</summary>
        public static void LogDay()
        {
            if (s_earlyCount + s_lateCount > 0)
            {
                RPHLog.Balance(BalanceSection.Handover, "early=" + s_earlyCount + " earlyMinutes=" + RPHLog.Num(s_earlyMinutes) + " earlyPaid=" + s_earlyPaid
                    + " late=" + s_lateCount + " lateMinutes=" + RPHLog.Num(s_lateMinutes) + " latePaid=" + s_latePaid);
            }
            s_earlyCount = s_earlyPaid = s_lateCount = s_latePaid = 0;
            s_earlyMinutes = s_lateMinutes = 0f;
        }
    }
}
