using System.Collections.Generic;
using System.Reflection;
using GLib;

namespace RPH.Mod.HR
{
    /// <summary>
    /// Offers the porters' room in every department. The game builds the room buttons from the department's room lists
    /// (Departments.xml), which a mod cannot extend without copying the whole department: add the room to the shared
    /// clinic rooms (optional, at most one per department) when the database is loaded, as MachineMaintenance does for machines.
    /// </summary>
    internal static class PortersRoom
    {
        private static bool s_applied;

        /// <summary>Once per session, as soon as the database (with our XML) is loaded.</summary>
        public static void Tick()
        {
            if (s_applied || !Database.Instance.Loaded)
            {
                return;
            }
            s_applied = true;
            if (!Porters.Enabled)
            {
                return;
            }
            GameDBRoomType room = Database.Instance.GetEntry<GameDBRoomType>(Porters.RoomTypeId);
            if (room == null)
            {
                RPHLog.Warn("Porters' room " + Porters.RoomTypeId + " is missing from the database.");
                return;
            }
            MethodInfo setShared = typeof(GameDBDepartment).GetProperty("SharedRoomsClinic").GetSetMethod(true);
            int count = 0;
            foreach (GameDBDepartment department in Database.Instance.GetEntries<GameDBDepartment>())
            {
                if (department.Hidden)
                {
                    continue;
                }
                var rooms = new List<GameDBDepartmentRoomRequirement>(department.SharedRoomsClinic ?? new GameDBDepartmentRoomRequirement[0]);
                if (rooms.Exists(r => r.RoomDatabaseEntryRef.Entry == room))
                {
                    continue;
                }
                rooms.Add(new GameDBDepartmentRoomRequirement { RoomDatabaseEntryRef = new DatabaseEntryRef<GameDBRoomType>(room), MinCount = 0, MaxCount = 1 });
                setShared.Invoke(department, new object[] { rooms.ToArray() });
                count++;
            }
            RPHLog.Info("Porters' room offered in " + count + " departments");
        }
    }
}
