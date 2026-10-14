using System;
using System.IO;
using System.Text;
using GLib;
using HarmonyLib;
using Lopital;
using RPH.Core.Accounting;
using RPH.Core.HR;
using RPH.Mod.Accounting;
using UnityEngine;

namespace RPH.Mod.HR
{
    /// <summary>
    /// The porters of the game being played. A porter is a nurse entity the game knows nothing about: his ID is in the
    /// registry, saved in a sidecar file next to the save (<c>maps/&lt;save&gt;.rph.porters.txt</c>).
    /// </summary>
    internal static class PorterStaff
    {
        private const string SidecarSuffix = ".rph.porters.txt";
        private const string Role = "Porter";
        private const string FeeId = "RPH_HR_PORTERS_HIRING_FEE";
        private const string SalaryPercentId = "RPH_HR_PORTERS_SALARY_PERCENT";

        /// <summary>Looking for a porter happens every time a patient waits for a stretcher: the list of porters is kept for a few seconds.</summary>
        private const float CacheSeconds = 2f;

        private static int s_picked;
        private static int s_nurseFallbacks;

        private static PorterRegistry s_registry = new PorterRegistry();
        private static System.Collections.Generic.List<Entity> s_cache;
        private static float s_cacheTime = float.NegativeInfinity;
        private static object s_session;
        private static string s_pendingSidecar;

        public static bool HasPorters
        {
            get { return s_registry.Count > 0; }
        }

        public static bool IsPorter(Entity entity)
        {
            return entity != null && s_registry.Contains(entity.GetEntityID());
        }

        public static string ExpenseSource
        {
            get { return Role; }
        }

        // ------------------------------------------------------------ session and sidecar

        /// <summary>Called when the game reads a save: its porters (if any) are next to it.</summary>
        public static void OnGameLoading(string saveXmlPath)
        {
            s_pendingSidecar = SidecarPathFor(saveXmlPath);
            s_session = null;
        }

        /// <summary>Called after the game wrote a save.</summary>
        public static void OnGameSaved(string saveXmlPath)
        {
            if (string.IsNullOrEmpty(saveXmlPath) || saveXmlPath.StartsWith(Application.streamingAssetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            string path = SidecarPathFor(saveXmlPath);
            s_registry.KeepOnly(Exists);
            try
            {
                if (s_registry.Count == 0)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    return;
                }
                File.WriteAllText(path, s_registry.WriteToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                RPHLog.Error("Could not write the porters to " + path, e);
            }
        }

        /// <summary>Starts a fresh registry when the game state object changes (new game) or a save was loaded.</summary>
        public static void Tick()
        {
            if (!RPHLedgerService.GameRunning)
            {
                return;
            }
            object session = Hospital.Instance.m_state;
            if (ReferenceEquals(session, s_session) && s_pendingSidecar == null)
            {
                return;
            }
            s_session = session;
            s_cache = null;
            s_registry = LoadSidecar(s_pendingSidecar) ?? new PorterRegistry();
            s_pendingSidecar = null;
            s_registry.KeepOnly(Exists);
            if (s_registry.Count > 0)
            {
                RPHLog.Info("Porters: " + s_registry.Count);
            }
        }

        private static PorterRegistry LoadSidecar(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }
            try
            {
                return PorterRegistry.ReadFromString(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e)
            {
                RPHLog.Error("Could not read the porters " + path + ", starting without", e);
                return null;
            }
        }

        private static string SidecarPathFor(string saveXmlPath)
        {
            string withoutExtension = saveXmlPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                ? saveXmlPath.Substring(0, saveXmlPath.Length - 4)
                : saveXmlPath;
            return withoutExtension + SidecarSuffix;
        }

        private static bool Exists(uint id)
        {
            foreach (Entity character in Hospital.Instance.m_characters)
            {
                if (character.GetEntityID() == id)
                {
                    return !character.GetComponent<EmployeeComponent>().IsFired();
                }
            }
            return false;
        }

        // ------------------------------------------------------------ the porters

        /// <summary>Every porter of the hospital, hired in any department.</summary>
        public static System.Collections.Generic.List<Entity> All()
        {
            var porters = new System.Collections.Generic.List<Entity>();
            foreach (Department department in Hospital.Instance.m_departments)
            {
                foreach (EntityIDPointer<Entity> nurse in department.m_departmentPersistentData.m_nurses)
                {
                    Entity entity = nurse.GetEntity();
                    if (IsPorter(entity) && !entity.GetComponent<EmployeeComponent>().IsFired())
                    {
                        porters.Add(entity);
                    }
                }
            }
            return porters;
        }

        private static System.Collections.Generic.List<Entity> Cached()
        {
            if (s_cache == null || Time.realtimeSinceStartup - s_cacheTime > CacheSeconds)
            {
                s_cache = All();
                s_cacheTime = Time.realtimeSinceStartup;
            }
            return s_cache;
        }

        /// <summary>Floor of the porter's room: where he waits, and the only floor where he fetches patients.</summary>
        public static int HomeFloor(Entity porter)
        {
            EmployeeComponent employee = porter.GetComponent<EmployeeComponent>();
            return employee.m_state.m_homeRoom != null && employee.m_state.m_homeRoom.GetEntity() != null
                ? employee.m_state.m_homeRoom.GetEntity().GetFloorIndex()
                : -1;
        }

        /// <summary>
        /// The porter who fetches this patient: on duty now, free, waiting on the patient's floor, the closest to him.
        /// Null when there is none: the game then picks a nurse as usual.
        /// </summary>
        public static Entity FindFor(Entity patient)
        {
            if (s_registry.Count == 0)
            {
                return null;
            }
            WalkComponent patientWalk = patient.GetComponent<WalkComponent>();
            Vector2i patientTile = patientWalk.GetCurrentTile();
            int floor = patientWalk.GetFloorIndex();
            Shift now = DayTime.Instance.GetShift();
            GameDBRoomType roomType = Database.Instance.GetEntry<GameDBRoomType>(Porters.RoomTypeId);
            Entity best = null;
            int bestDistance = int.MaxValue;
            foreach (Entity porter in Cached())
            {
                EmployeeComponent employee = porter.GetComponent<EmployeeComponent>();
                if (employee.IsFired())
                {
                    continue;
                }
                BehaviorNurse behavior = porter.GetComponent<BehaviorNurse>();
                if (employee.m_state.m_shift != now || behavior.m_state.m_nurseState == NurseState.Training
                    || employee.GetHomeRoomType() != roomType || HomeFloor(porter) != floor || !behavior.IsFree())
                {
                    continue;
                }
                Vector2i tile = porter.GetComponent<WalkComponent>().GetCurrentTile();
                int dx = tile.m_x - patientTile.m_x;
                int dy = tile.m_y - patientTile.m_y;
                int distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    best = porter;
                    bestDistance = distance;
                }
            }
            if (best != null)
            {
                s_picked++;
            }
            else
            {
                s_nurseFallbacks++;
            }
            return best;
        }

        /// <summary>Midnight: how the porters were used since the last midnight.</summary>
        public static void LogDay()
        {
            if (s_registry.Count > 0)
            {
                RPHLog.Balance(BalanceSection.Porters, "porters=" + All().Count + " picked=" + s_picked + " nurseFallbacks=" + s_nurseFallbacks);
            }
            s_picked = s_nurseFallbacks = 0;
        }

        // ------------------------------------------------------------ hiring

        /// <summary>The first department with a porters' room that has a free desk for this shift. Null: build a porters' room first.</summary>
        private static Department FindHost(Shift shift)
        {
            foreach (Department department in Hospital.Instance.m_departments)
            {
                foreach (EntityIDPointer<TileObject> pointer in department.m_departmentPersistentData.m_objects)
                {
                    TileObject desk = pointer.GetEntity();
                    if (desk.User != null || !desk.HasTag("pc") || desk.GetWorkspaceOwner(shift) != null || desk.IsBroken() || !desk.IsValid())
                    {
                        continue;
                    }
                    Room room = MapScriptInterface.Instance.GetRoomAt(desk.m_state.m_position, desk.GetFloorIndex());
                    if (room != null && room.m_roomPersistentData.m_roomType.Entry.HasTag(Porters.RoomTag)
                        && (room.m_roomPersistentData.m_valid == RoomValidity.OK || room.m_roomPersistentData.m_valid == RoomValidity.MISSING_STAFF))
                    {
                        return department;
                    }
                }
            }
            return null;
        }

        /// <summary>Hires a porter for the given shift. Returns null when done, else the string ID of what is missing.</summary>
        public static string TryHire(Shift shift)
        {
            if (!Porters.Enabled)
            {
                return "RPH_PORTERS_ERR_OFF";
            }
            if (Hospital.Instance.ReachedCharacterLimit())
            {
                return "RPH_PORTERS_ERR_LIMIT";
            }
            int fee = Porters.Int(FeeId);
            if (RPHLedgerService.Balance < fee)
            {
                return "RPH_PORTERS_ERR_MONEY";
            }
            Department host = FindHost(shift);
            if (host == null)
            {
                return "RPH_PORTERS_ERR_ROOM";
            }

            Entity porter = LopitalEntityFactory.CreateCharacterNurse(null, Vector2i.ZERO_VECTOR, 1, 3);
            EmployeeComponent employee = porter.GetComponent<EmployeeComponent>();
            employee.m_state.m_hiredForDepartment = host.GetDepartmentType();
            employee.m_state.m_hiredLevel = employee.m_state.m_level;
            employee.m_state.m_hiredSalaryRandomization = UnityEngine.Random.Range(0f, 1f);
            employee.m_state.m_employeeType = LopitalTypes.CharacterNurse;
            employee.m_state.m_shift = shift;
            s_registry.Add(porter.GetEntityID());
            employee.ComputeSalary();

            EntityIDPointer<Department> previous = Hospital.Instance.m_activeDepartment;
            Shift previousShift = HiringManager.Instance.m_shift;
            try
            {
                // Floor.AddCharacter files the new employee in the active department.
                Hospital.Instance.m_activeDepartment = host;
                HiringManager.Instance.m_shift = shift;
                HiringManager.Instance.HiredCharacter = porter;
                HiringManager.Instance.Hire(host, Hospital.Instance.GetGroundFloor(), MapScriptInterface.Instance.GetRandomSpawnPosition());
            }
            finally
            {
                Hospital.Instance.m_activeDepartment = previous;
                HiringManager.Instance.m_shift = previousShift;
            }
            s_cache = null;
            ApplyUniform(porter);
            RPHLedgerService.Spend(fee, ExpenseCategory.HIRING_AGENCY, string.Empty, Role, LedgerCategory.HiringAgency);
            RPHLog.Info("Porter hired for the " + shift + " shift in " + RPHGameData.DepartmentId(host) + ": " + porter.Name);
            return null;
        }

        public static void Fire(Entity porter)
        {
            porter.GetComponent<EmployeeComponent>().GetFired();
        }

        /// <summary>The porters' tunic colour, so they can be told from the nurses.</summary>
        public static void ApplyUniform(Entity porter)
        {
            var color = new Vector3(Porters.Float("RPH_HR_PORTERS_COAT_RED"), Porters.Float("RPH_HR_PORTERS_COAT_GREEN"), Porters.Float("RPH_HR_PORTERS_COAT_BLUE"));
            AnimModelComponent model = porter.GetComponent<AnimModelComponent>();
            model.m_state.m_defaultClothes.m_colorClothesCoat = color;
            model.m_state.m_defaultClothes.m_colorClothesPants = color;
            model.m_state.m_defaultClothes.m_colorClothesTop = color;
            model.m_colorsDirty = true;
            model.RevertToDefaultClothes();
        }

        /// <summary>A porter earns a share of the wage a nurse of his level would.</summary>
        public static void ScaleSalary(EmployeeComponent employee)
        {
            employee.m_state.m_salary = (int)Math.Round(employee.m_state.m_salary * (double)Porters.Float(SalaryPercentId) / 100.0, MidpointRounding.AwayFromZero);
        }
    }
}
