using System;
using System.Collections.Generic;
using GLib;
using Lopital;
using RPH.Core.Formatting;
using RPH.Mod.Accounting;
using RPH.Mod.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RPH.Mod.HR
{
    /// <summary>
    /// Porters window (F10): hire a porter for the day or night shift, see where each one waits and what he earns, fire him.
    /// Porters belong to no department, so they are not in the department hiring panel.
    /// </summary>
    internal sealed class RPHPortersWindow : MonoBehaviour
    {
        private const KeyCode Hotkey = KeyCode.F10;
        private const float Width = 720f;
        private const float Height = 460f;
        private const float RowHeight = 30f;

        private RectTransform m_window;
        private RectTransform m_content;
        private string m_message = string.Empty;
        private int m_builtHour = -1;

        public bool IsOpen
        {
            get { return m_window != null && m_window.gameObject.activeSelf; }
        }

        public void Update()
        {
            if (Input.GetKeyDown(Hotkey))
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }
            if (!IsOpen)
            {
                return;
            }
            if (!RPHLedgerService.GameRunning)
            {
                Close();
                return;
            }
            int hour = (int)DayTime.Instance.GetDayTimeHours();
            if (hour != m_builtHour)
            {
                Rebuild();
            }
        }

        private void Open()
        {
            if (!RPHLedgerService.GameRunning || !Porters.Enabled)
            {
                return;
            }
            try
            {
                if (m_window == null)
                {
                    BuildWindow();
                }
                m_message = string.Empty;
                m_window.gameObject.SetActive(true);
                m_window.SetAsLastSibling();
                Rebuild();
            }
            catch (Exception e)
            {
                RPHLog.Error("Could not open the porters window", e);
            }
        }

        private void Close()
        {
            if (m_window != null)
            {
                m_window.gameObject.SetActive(false);
            }
        }

        private void BuildWindow()
        {
            HospitalManagementPanelController panel = HospitalManagementPanelController.Instance;
            Transform parent = panel != null && panel.transform.parent != null ? panel.transform.parent : FindObjectOfType<Canvas>().transform;
            m_window = RPHUi.Rect(parent, "RPHPortersWindow");
            m_window.sizeDelta = new Vector2(Width, Height);
            m_window.anchorMin = m_window.anchorMax = new Vector2(0.5f, 0.5f);
            m_window.anchoredPosition = Vector2.zero;
            if (panel != null)
            {
                m_window.localScale = panel.transform.localScale;
            }
            Image background = m_window.gameObject.AddComponent<Image>();
            background.color = RPHUi.WindowColor;
            Image gamePanel = panel != null ? panel.GetComponent<Image>() : null;
            if (gamePanel != null)
            {
                background.sprite = gamePanel.sprite;
                background.type = gamePanel.type;
                background.color = gamePanel.color;
            }

            RectTransform title = RPHUi.Place(RPHUi.Rect(m_window, "Title"), new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -56f), Vector2.zero);
            title.gameObject.AddComponent<Image>().color = RPHUi.HeadingColor;
            title.gameObject.AddComponent<RPHDragHandle>().Target = m_window;
            Text heading = RPHUi.Label(title, RPHText.Get("RPH_PORTERS_TITLE"), 22, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            RPHUi.Place(heading.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-70f, 0f));
            Button close = RPHUi.Button(title, "X", false, Close);
            RPHUi.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-54f, -16f), new Vector2(-14f, 16f));

            RectTransform viewport = RPHUi.Place(RPHUi.Rect(m_window, "Viewport"), Vector2.zero, Vector2.one, new Vector2(16f, 12f), new Vector2(-16f, -66f));
            viewport.gameObject.AddComponent<RectMask2D>();
            m_content = RPHUi.Rect(viewport, "Content");
            m_content.anchorMin = new Vector2(0f, 1f);
            m_content.anchorMax = Vector2.one;
            m_content.pivot = new Vector2(0.5f, 1f);
            m_content.offsetMin = m_content.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = m_content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 8, 4, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            m_content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = m_content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        private void Rebuild()
        {
            m_builtHour = (int)DayTime.Instance.GetDayTimeHours();
            for (int i = m_content.childCount - 1; i >= 0; i--)
            {
                Destroy(m_content.GetChild(i).gameObject);
            }

            string fee = MoneyFormat.Money(Porters.Int("RPH_HR_PORTERS_HIRING_FEE"));
            HorizontalLayoutGroup hire = RPHUi.Row(m_content, 36f, 8f);
            Hire(hire.transform, RPHText.Get("RPH_PORTERS_HIRE_DAY", fee), Shift.DAY);
            Hire(hire.transform, RPHText.Get("RPH_PORTERS_HIRE_NIGHT", fee), Shift.NIGHT);

            string message = string.IsNullOrEmpty(m_message) ? RPHText.Get("RPH_PORTERS_HINT") : m_message;
            Text note = RPHUi.Label(m_content, message, 14, string.IsNullOrEmpty(m_message) ? RPHUi.MutedColor : RPHUi.TextColor, TextAnchor.MiddleLeft);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            RPHUi.Size(note, -1f, 40f, 1f);

            List<Entity> porters = PorterStaff.All();
            if (porters.Count == 0)
            {
                Text empty = RPHUi.Label(m_content, RPHText.Get("RPH_PORTERS_NONE"), 16, RPHUi.TextColor, TextAnchor.MiddleLeft);
                RPHUi.Size(empty, -1f, RowHeight, 1f);
                return;
            }
            HorizontalLayoutGroup header = RPHUi.Row(m_content, RowHeight, 8f);
            Cell(header.transform, RPHText.Get("RPH_PORTERS_COL_NAME"), 250f, FontStyle.Bold);
            Cell(header.transform, RPHText.Get("RPH_PORTERS_COL_SHIFT"), 90f, FontStyle.Bold);
            Cell(header.transform, RPHText.Get("RPH_PORTERS_COL_FLOOR"), 70f, FontStyle.Bold);
            Cell(header.transform, RPHText.Get("RPH_PORTERS_COL_WAGE"), 90f, FontStyle.Bold);
            foreach (Entity porter in porters)
            {
                EmployeeComponent employee = porter.GetComponent<EmployeeComponent>();
                int floor = PorterStaff.HomeFloor(porter);
                HorizontalLayoutGroup row = RPHUi.Row(m_content, RowHeight, 8f);
                Cell(row.transform, porter.GetComponent<CharacterPersonalInfoComponent>().m_personalInfo.GetFullName(), 250f, FontStyle.Normal);
                Cell(row.transform, RPHText.Get(employee.m_state.m_shift == Shift.DAY ? "RPH_PORTERS_SHIFT_DAY" : "RPH_PORTERS_SHIFT_NIGHT"), 90f, FontStyle.Normal);
                Cell(row.transform, floor >= 0 ? floor.ToString() : "-", 70f, FontStyle.Normal);
                Cell(row.transform, MoneyFormat.Money(employee.m_state.m_salary), 90f, FontStyle.Normal);
                Entity target = porter;
                Button fire = RPHUi.Button(row.transform, RPHText.Get("RPH_PORTERS_FIRE"), false, () => OnFire(target));
                RPHUi.Size(fire, 100f, -1f, 0f);
            }
        }

        private void Hire(Transform parent, string text, Shift shift)
        {
            Button button = RPHUi.Button(parent, text, false, () => OnHire(shift));
            RPHUi.Size(button, -1f, -1f, 1f);
        }

        private static void Cell(Transform row, string text, float width, FontStyle style)
        {
            Text label = RPHUi.Label(row, text, 14, RPHUi.TextColor, TextAnchor.MiddleLeft, style);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            RPHUi.Size(label, width, -1f, 0f);
        }

        private void OnHire(Shift shift)
        {
            try
            {
                string error = PorterStaff.TryHire(shift);
                m_message = error != null ? RPHText.Get(error) : RPHText.Get("RPH_PORTERS_HIRED");
            }
            catch (Exception e)
            {
                RPHLog.Error("Could not hire a porter", e);
                m_message = RPHText.Get("RPH_PORTERS_ERR_FAILED");
            }
            Rebuild();
        }

        private void OnFire(Entity porter)
        {
            try
            {
                PorterStaff.Fire(porter);
                m_message = string.Empty;
            }
            catch (Exception e)
            {
                RPHLog.Error("Could not fire a porter", e);
            }
            Rebuild();
        }
    }
}
