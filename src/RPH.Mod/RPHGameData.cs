using System;
using System.Collections.Generic;
using GLib;
using Lopital;
using RPH.Core.Accounting;
using UnityEngine;

namespace RPH.Mod
{
    /// <summary>Reads IDs from game objects and turns IDs back into localized names and icons.</summary>
    internal static class RPHGameData
    {
        private static readonly Dictionary<string, string> s_nameCache = new Dictionary<string, string>();
        private static string s_cacheLanguage;

        public static string DepartmentId(Department department)
        {
            if (department == null || department.m_departmentPersistentData == null)
            {
                return string.Empty;
            }
            GameDBDepartment type = department.m_departmentPersistentData.m_departmentType.Entry;
            return type != null ? type.DatabaseID.ToString() : string.Empty;
        }

        public static string EmployeeDepartmentId(Entity employee)
        {
            if (employee == null)
            {
                return string.Empty;
            }
            EmployeeComponent component = employee.GetComponent<EmployeeComponent>();
            return component != null ? DepartmentId(component.m_state.m_department.GetEntity()) : string.Empty;
        }

        public static string EmployeeRole(Entity employee)
        {
            if (employee == null)
            {
                return string.Empty;
            }
            EmployeeComponent component = employee.GetComponent<EmployeeComponent>();
            return component != null ? component.m_state.m_employeeType.ToString() : string.Empty;
        }

        /// <summary>The diagnosis the patient is paid for, or empty.</summary>
        public static string PatientDiagnosisId(Entity patient)
        {
            if (patient == null)
            {
                return string.Empty;
            }
            BehaviorPatient behavior = patient.GetComponent<BehaviorPatient>();
            if (behavior == null || behavior.m_state == null || behavior.m_state.m_medicalCondition == null)
            {
                return string.Empty;
            }
            GameDBMedicalCondition diagnosis = behavior.m_state.m_medicalCondition.m_diagnosedMedicalCondition.Entry;
            return diagnosis != null ? diagnosis.DatabaseID.ToString() : string.Empty;
        }

        public static string DepartmentName(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RPHText.Get("RPH_HOSPITAL_WIDE");
            }
            return EntryName<GameDBDepartment>(id);
        }

        public static string ProcedureName(ActivityKind kind, string id)
        {
            return kind == ActivityKind.Treatment || kind == ActivityKind.Surgery
                ? EntryName<GameDBTreatment>(id)
                : EntryName<GameDBExamination>(id);
        }

        public static string DiagnosisName(string id)
        {
            return EntryName<GameDBMedicalCondition>(id);
        }

        public static string RoleName(string role)
        {
            if (string.IsNullOrEmpty(role))
            {
                return string.Empty;
            }
            string key = "RPH_ROLE_" + role;
            string text = RPHText.Get(key);
            return text.StartsWith("!LOC!", StringComparison.Ordinal) ? role.Replace("Character", string.Empty) : text;
        }

        public static string CategoryName(LedgerCategory category)
        {
            return RPHText.Get("RPH_CAT_" + category);
        }

        /// <summary>Department icon from the game's atlas (or its custom icon), null if unknown.</summary>
        public static Sprite DepartmentIcon(string id)
        {
            if (string.IsNullOrEmpty(id) || IconManager.Instance == null)
            {
                return null;
            }
            GameDBDepartment department = Database.Instance.GetEntry<GameDBDepartment>(id);
            if (department == null)
            {
                return null;
            }
            if (department.CustomIcon_1_DarkBackgroundAssetRef != null)
            {
                return IconManager.Instance.GetIcon(department.CustomIcon_1_DarkBackgroundAssetRef.XmlID);
            }
            return IconManager.Instance.GetIcon(department.IconID + 1);
        }

        private static string EntryName<T>(string id) where T : DatabaseEntry
        {
            if (string.IsNullOrEmpty(id))
            {
                return "-";
            }
            string language = StringTable.GetInstance().GetCurrentLanguage();
            if (language != s_cacheLanguage)
            {
                s_nameCache.Clear();
                s_cacheLanguage = language;
            }
            string cacheKey = typeof(T).Name + ":" + id;
            string name;
            if (s_nameCache.TryGetValue(cacheKey, out name))
            {
                return name;
            }
            T entry = Database.Instance.GetEntry<T>(id);
            name = entry != null ? StringTable.GetInstance().GetLocalizedText(entry) : id;
            s_nameCache[cacheKey] = name;
            return name;
        }
    }
}
