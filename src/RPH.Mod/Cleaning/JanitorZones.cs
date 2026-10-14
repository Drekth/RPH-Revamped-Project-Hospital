using System.Collections.Generic;
using GLib;
using Lopital;
using RPH.Core.Cleaning;
using UnityEngine;

namespace RPH.Mod.Cleaning
{
    /// <summary>
    /// Floor-based cleaning service (mod/Database/Cleaning/Cleaning.xml): which rooms a janitor picks, and what he does when a
    /// room he is going to has become busy.
    /// </summary>
    internal static class JanitorZones
    {
        private const string EnabledId = "RPH_CLEANING_ZONES_ENABLED";
        private const string CoverId = "RPH_CLEANING_COVER_FLOORS_WITHOUT_JANITOR";
        private const string FrontDistanceId = "RPH_CLEANING_FRONT_DISTANCE";
        private const string WaitMinutesId = "RPH_CLEANING_WAIT_MINUTES";

        /// <summary>The janitor whose room choice is running (see JanitorZonePatches), null otherwise.</summary>
        public static BehaviorJanitor Choosing;

        private sealed class Wait
        {
            public BehaviorJanitorState State;
            public Vector2f Destination;
            public int Floor;
            public float Waited;
        }

        private static readonly Dictionary<BehaviorJanitor, Wait> s_waiting = new Dictionary<BehaviorJanitor, Wait>();

        /// <summary>Janitors who waited too long for a room: they go into that room, they do not start waiting again.</summary>
        private static readonly Dictionary<BehaviorJanitor, Room> s_gaveUp = new Dictionary<BehaviorJanitor, Room>();

        private static int s_waits;
        private static int s_timeouts;
        private static float s_waitedSeconds;

        public static bool Enabled
        {
            get
            {
                GameDBTweakableInt enabled = Database.Instance.GetEntry<GameDBTweakableInt>(EnabledId);
                return enabled != null && enabled.Value != 0;
            }
        }

        private static int Int(string id)
        {
            GameDBTweakableInt value = Database.Instance.GetEntry<GameDBTweakableInt>(id);
            return value != null ? value.Value : 0;
        }

        // ------------------------------------------------------------ which rooms

        /// <summary>Starts the choice of a room for this janitor. A janitor with no cleaning closet keeps the game's choice.</summary>
        public static void BeginChoice(BehaviorJanitor janitor)
        {
            Choosing = Enabled && HomeFloor(janitor) >= 0 ? janitor : null;
        }

        /// <summary>The floor of the janitor's cleaning closet; -1 for a janitor with no closet or one of the administrative janitors, who do not clean.</summary>
        private static int HomeFloor(BehaviorJanitor janitor)
        {
            EmployeeComponent employee = janitor.GetComponent<EmployeeComponent>();
            if (employee.m_state.m_homeRoom == null || employee.m_state.m_homeRoom.GetEntity() == null)
            {
                return -1;
            }
            Room home = employee.m_state.m_homeRoom.GetEntity();
            return home.m_roomPersistentData.m_roomType.Entry.HasTag("janitor_workspace") ? home.GetFloorIndex() : -1;
        }

        /// <summary>Floors where at least one janitor of this shift has his closet.</summary>
        private static HashSet<int> FloorsWithJanitor(Shift shift)
        {
            var floors = new HashSet<int>();
            foreach (Department department in Hospital.Instance.m_departments)
            {
                foreach (EntityIDPointer<Entity> civilian in department.m_departmentPersistentData.m_civilianEmployees)
                {
                    Entity entity = civilian.GetEntity();
                    BehaviorJanitor other = entity.GetComponent<BehaviorJanitor>();
                    EmployeeComponent employee = entity.GetComponent<EmployeeComponent>();
                    int floor = other != null ? HomeFloor(other) : -1;
                    if (floor >= 0 && !employee.IsFired() && employee.m_state.m_shift == shift)
                    {
                        floors.Add(floor);
                    }
                }
            }
            return floors;
        }

        /// <summary>A procedure is running in the room: the game marks it with the procedure's owner.</summary>
        public static bool IsBusy(Room room)
        {
            return room.m_roomPersistentData.m_currentProcedureOwner != null && room.m_roomPersistentData.m_currentProcedureOwner.GetEntity() != null;
        }

        /// <summary>
        /// The dirtiest tile the janitor may clean, as (x, y, floor); (0, 0, 0) for none. Same choice as the game's
        /// FindDirtiestTile...AnyFloor (blood first, then dirt above the threshold), over the rooms of his zone that are not busy.
        /// </summary>
        public static Vector3i FindDirtiest(BehaviorJanitor janitor, bool respectAssignment, int threshold)
        {
            int homeFloor = HomeFloor(janitor);
            EmployeeComponent self = janitor.GetComponent<EmployeeComponent>();
            HashSet<int> served = FloorsWithJanitor(self.m_state.m_shift);
            bool cover = Int(CoverId) != 0;
            Department own = self.m_state.m_department.GetEntity();

            var rooms = new List<Room>();
            foreach (Department department in Hospital.Instance.m_departments)
            {
                foreach (EntityIDPointer<Room> pointer in department.m_departmentPersistentData.m_rooms)
                {
                    Room room = pointer.GetEntity();
                    if (!CleaningMath.IsInZone(room.GetFloorIndex(), homeFloor, served.Contains(room.GetFloorIndex()), department == own, cover) || IsBusy(room))
                    {
                        continue;
                    }
                    bool assignedToHim = janitor.m_state.m_assignedRooms.Contains(room);
                    bool unassigned = room.m_roomPersistentData.m_assignedJanitors.Count == 0 && janitor.m_state.m_assignedRooms.Count == 0;
                    if (!respectAssignment || assignedToHim || unassigned)
                    {
                        rooms.Add(room);
                    }
                }
            }

            float best = 0f;
            Vector3i result = default(Vector3i);
            foreach (Room room in rooms)
            {
                Floor floor = Hospital.Instance.m_floors[room.GetFloorIndex()];
                for (int i = room.m_roomPersistentData.m_positionBottom.m_x; i <= room.m_roomPersistentData.m_positionTop.m_x; i++)
                {
                    for (int j = room.m_roomPersistentData.m_positionBottom.m_y; j <= room.m_roomPersistentData.m_positionTop.m_y; j++)
                    {
                        if (floor.m_mapPersistentData.m_tiles[i, j].m_dirtType == DirtType.BLOOD && IsFreeDirt(floor, i, j, best, 0f, false))
                        {
                            best = floor.m_mapPersistentData.m_tiles[i, j].m_dirtLevel;
                            result = new Vector3i(i, j, room.GetFloorIndex());
                        }
                    }
                }
                if (best > 0f)
                {
                    return result;
                }
            }
            foreach (Room room in rooms)
            {
                Floor floor = Hospital.Instance.m_floors[room.GetFloorIndex()];
                for (int i = room.m_roomPersistentData.m_positionBottom.m_x; i <= room.m_roomPersistentData.m_positionTop.m_x; i++)
                {
                    for (int j = room.m_roomPersistentData.m_positionBottom.m_y; j <= room.m_roomPersistentData.m_positionTop.m_y; j++)
                    {
                        if (IsFreeDirt(floor, i, j, best, threshold, true))
                        {
                            best = floor.m_mapPersistentData.m_tiles[i, j].m_dirtLevel;
                            result = new Vector3i(i, j, room.GetFloorIndex());
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>The game's conditions for a tile a janitor can take: dirtier than the best so far (and the threshold), in a room nobody reserved, empty, reachable.</summary>
        private static bool IsFreeDirt(Floor floor, int x, int y, float best, float threshold, bool needsNoUser)
        {
            Tile tile = floor.m_mapPersistentData.m_tiles[x, y];
            return tile.m_dirtLevel > threshold && tile.m_dirtLevel > best && (!needsNoUser || tile.m_user == null)
                && floor.m_roomTiles[x, y] != null && floor.m_roomTiles[x, y].m_roomPersistentData.m_reservedByCharacter == null
                && floor.m_tileObjects[x, y].GetAllObjects().Count == 0 && floor.m_accessibility[x, y] != 2;
        }

        // ------------------------------------------------------------ waiting in front of a busy room

        /// <summary>
        /// Called before the janitor's own update. Returns false while he waits (his update is skipped). A janitor walking to a
        /// room that has just become busy stops close to it; he walks on when the procedure is over or the wait is too long.
        /// </summary>
        public static bool BeforeUpdate(BehaviorJanitor janitor, float deltaTime)
        {
            BehaviorJanitorState state = janitor.m_state.m_janitorState;
            Wait wait;
            if (s_waiting.TryGetValue(janitor, out wait))
            {
                Room waitedRoom = janitor.m_state.m_room != null ? janitor.m_state.m_room.GetEntity() : null;
                if (wait.State != state || waitedRoom == null)
                {
                    s_waiting.Remove(janitor);
                    return true;
                }
                wait.Waited += deltaTime;
                bool expired = CleaningMath.WaitExpired(wait.Waited, Float(WaitMinutesId), DayTime.Instance.IngameTimeHoursToRealTimeSeconds(1f));
                if (IsBusy(waitedRoom) && !expired)
                {
                    Hospital.Instance.m_currentHospitalStatus.m_hadAnyActiveCharactersThisFrame = true;
                    return false;
                }
                s_waiting.Remove(janitor);
                s_waitedSeconds += wait.Waited;
                if (expired && IsBusy(waitedRoom))
                {
                    s_timeouts++;
                    s_gaveUp[janitor] = waitedRoom;
                }
                janitor.GetComponent<WalkComponent>().SetDestination(wait.Destination, wait.Floor);
                return true;
            }
            if (state != BehaviorJanitorState.Walking && state != BehaviorJanitorState.WalkingToNextRoom)
            {
                if (s_gaveUp.Count > 0)
                {
                    s_gaveUp.Remove(janitor);
                }
                return true;
            }
            Room room = janitor.m_state.m_room != null ? janitor.m_state.m_room.GetEntity() : null;
            Room gaveUpOn;
            if (s_gaveUp.TryGetValue(janitor, out gaveUpOn) && gaveUpOn == room)
            {
                return true;
            }
            WalkComponent walk = janitor.GetComponent<WalkComponent>();
            if (room == null || !walk.IsBusy() || walk.GetFloorIndex() != room.GetFloorIndex() || !IsBusy(room))
            {
                return true;
            }
            Vector2i tile = walk.GetCurrentTile();
            Range2i range = room.GetRange();
            int distance = CleaningMath.DistanceToRange(tile.m_x, tile.m_y, range.m_start.m_x, range.m_start.m_y, range.m_end.m_x, range.m_end.m_y);
            if (!CleaningMath.IsInFrontOfRoom(distance, room.IsPositionInRoom(tile), Int(FrontDistanceId)))
            {
                return true;
            }
            s_waiting[janitor] = new Wait { State = state, Destination = walk.m_state.m_destination, Floor = walk.m_state.m_destinationFloor };
            walk.Stop();
            janitor.GetComponent<AnimModelComponent>().PlayAnimation("stand_idle");
            s_waits++;
            return false;
        }

        private static float Float(string id)
        {
            GameDBTweakableFloat value = Database.Instance.GetEntry<GameDBTweakableFloat>(id);
            return value != null ? value.Value : 0f;
        }

        /// <summary>Midnight: how often the janitors waited in front of a busy room since the last midnight.</summary>
        public static void LogDay()
        {
            if (s_waits > 0)
            {
                RPHLog.Balance(BalanceSection.Cleaning, "waits=" + s_waits + " waitedMinutes=" + RPHLog.Num(s_waitedSeconds / DayTime.Instance.IngameTimeHoursToRealTimeSeconds(1f) * 60f)
                    + " timeouts=" + s_timeouts);
            }
            s_waits = s_timeouts = 0;
            s_waitedSeconds = 0f;
        }
    }
}
