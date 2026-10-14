using System.Reflection;
using GLib;
using Lopital;
using UnityEngine;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// Wear, random breakdowns and paid repairs for the machines listed in mod/Database/Economy/MachineMaintenance.xml.
    /// The game already has the whole mechanism (TileObject wear, janitors that repair, broken objects skipped by
    /// the exams) but no object turns it on: this class turns it on for our machines and feeds it.
    /// </summary>
    internal static class MachineMaintenance
    {
        private const string EnabledId = "RPH_ECONOMY_MACHINE_MAINTENANCE_ENABLED";
        private const string BaseChanceId = "RPH_ECONOMY_MACHINE_BREAKDOWN_BASE_CHANCE";
        private const string MaxChanceId = "RPH_ECONOMY_MACHINE_BREAKDOWN_MAX_CHANCE";
        private const string WearPrefix = "RPH_ECONOMY_MACHINE_WEAR_";
        private const string RepairPrefix = "RPH_ECONOMY_MACHINE_REPAIR_";
        private const string BreakdownRepairPrefix = "RPH_ECONOMY_MACHINE_BREAKDOWN_REPAIR_";

        /// <summary>The game caps wear at 100 (TileObject.ReduceDurability); a machine breaks when wear reaches its Durability.</summary>
        private const int MaxWear = 100;

        private static bool s_applied;

        /// <summary>Once per session, as soon as the database (with our XML) is loaded.</summary>
        public static void Tick()
        {
            if (s_applied || !Database.Instance.Loaded)
            {
                return;
            }
            s_applied = true;
            ApplyToMachines();
        }

        private static void ApplyToMachines()
        {
            GameDBTweakableInt enabled = Database.Instance.GetEntry<GameDBTweakableInt>(EnabledId);
            if (enabled == null || enabled.Value == 0)
            {
                return;
            }
            MethodInfo setMaintenance = typeof(GameDBObject).GetProperty("RequiresMaintenance").GetSetMethod(true);
            MethodInfo setDurability = typeof(GameDBObject).GetProperty("Durability").GetSetMethod(true);
            int count = 0;
            foreach (GameDBObject entry in Database.Instance.GetEntries<GameDBObject>())
            {
                if (WearPerUse(entry) > 0f)
                {
                    setMaintenance.Invoke(entry, new object[] { true });
                    setDurability.Invoke(entry, new object[] { MaxWear });
                    count++;
                }
            }
            RPHLog.Info("Machine maintenance on for " + count + " machine types");
        }

        private static float WearPerUse(GameDBObject entry)
        {
            GameDBTweakableFloat wear = Database.Instance.GetEntry<GameDBTweakableFloat>(WearPrefix + entry.DatabaseID);
            return wear != null ? wear.Value : 0f;
        }

        private static float Float(string id)
        {
            GameDBTweakableFloat value = Database.Instance.GetEntry<GameDBTweakableFloat>(id);
            return value != null ? value.Value : 0f;
        }

        /// <summary>A machine was used for one exam: it wears, and may break down.</summary>
        public static void OnUsed(TileObject machine)
        {
            GameDBObject type = machine != null ? machine.m_state.m_gameDBObject.Entry : null;
            if (type == null || !type.RequiresMaintenance || machine.IsBroken())
            {
                return;
            }
            float wearPerUse = WearPerUse(type);
            if (wearPerUse <= 0f)
            {
                return;
            }
            machine.ReduceDurability(wearPerUse);
            float baseChance = Float(BaseChanceId);
            float chance = baseChance + (Float(MaxChanceId) - baseChance) * machine.m_state.m_wear / type.Durability;
            if (Random.value < chance)
            {
                machine.ReduceDurability(MaxWear);
                RPHLog.Balance(BalanceSection.Maintenance, "event=breakdown machine=" + type.DatabaseID
                    + " department=" + RPHGameData.DepartmentId(machine.m_state.m_department.GetEntity()));
            }
        }

        /// <summary>
        /// What repairing this machine now costs the hospital: 0 if it is not one of ours or not worn.
        /// Must be read before Repair() resets the wear.
        /// </summary>
        public static int RepairCost(TileObject machine)
        {
            GameDBObject type = machine.m_state.m_gameDBObject.Entry;
            if (!type.RequiresMaintenance || machine.m_state.m_wear <= 0f)
            {
                return 0;
            }
            string prefix = machine.IsBroken() ? BreakdownRepairPrefix : RepairPrefix;
            GameDBTweakableInt cost = Database.Instance.GetEntry<GameDBTweakableInt>(prefix + type.DatabaseID);
            return cost != null ? cost.Value : 0;
        }
    }
}
