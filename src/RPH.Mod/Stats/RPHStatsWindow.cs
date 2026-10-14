using System;
using System.Collections.Generic;
using System.Linq;
using Lopital;
using RPH.Core.Accounting;
using RPH.Core.Formatting;
using RPH.Mod.Accounting;
using RPH.Mod.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RPH.Mod.Stats
{
    /// <summary>
    /// "Advanced statistics" window: where the money came from and where it went, for any period,
    /// by category, department, shift and procedure. Read-only view of the ledger.
    /// Opened with F9 or the extra tab button of the hospital management panel.
    /// </summary>
    internal sealed class RPHStatsWindow : MonoBehaviour
    {
        private enum Tab
        {
            Summary,
            Spending,
            Income,
            Shifts,
            Departments,
            Activity,
        }

        private const KeyCode Hotkey = KeyCode.F9;
        private const float Width = 1000f;
        private const float Height = 660f;
        private const float ValueWidth = 110f;
        private const float PercentWidth = 48f;
        private const float BarWidth = 60f;
        private const float RowHeight = 24f;

        public static RPHStatsWindow Instance { get; private set; }

        private RectTransform m_window;
        private RectTransform m_tabs;
        private RectTransform m_ranges;
        private RectTransform m_content;
        private Text m_clock;
        private ScrollRect m_scroll;

        private Tab m_tab = Tab.Summary;
        private RangeKind m_range = RangeKind.Yesterday;
        private int m_singleDay;
        private int m_builtHour = -1;
        private int m_builtDay = -1;

        public void Awake()
        {
            Instance = this;
            RPHLedgerService.SessionChanged += Close;
        }

        public void OnDestroy()
        {
            RPHLedgerService.SessionChanged -= Close;
        }

        public bool IsOpen
        {
            get { return m_window != null && m_window.gameObject.activeSelf; }
        }

        public void Toggle()
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

        public void Open()
        {
            if (!RPHLedgerService.GameRunning || !RPHLedgerService.IsReady)
            {
                return;
            }
            try
            {
                if (m_window == null)
                {
                    BuildWindow();
                }
                m_window.gameObject.SetActive(true);
                m_window.SetAsLastSibling();
                Rebuild();
            }
            catch (Exception e)
            {
                RPHLog.Error("Could not open the statistics window", e);
            }
        }

        public void Close()
        {
            if (m_window != null)
            {
                m_window.gameObject.SetActive(false);
            }
        }

        public void Update()
        {
            if (Input.GetKeyDown(Hotkey))
            {
                Toggle();
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
            // Live data: rebuild at most once per in-game hour.
            int hour = (int)DayTime.Instance.GetDayTimeHours();
            if (hour != m_builtHour || RPHLedgerService.CurrentDay != m_builtDay)
            {
                Rebuild();
            }
        }

        // ---------------------------------------------------------------- window chrome

        private void BuildWindow()
        {
            Transform parent = FindParent();
            m_window = RPHUi.Rect(parent, "RPHStatsWindow");
            m_window.sizeDelta = new Vector2(Width, Height);
            m_window.anchorMin = m_window.anchorMax = new Vector2(0.5f, 0.5f);
            m_window.anchoredPosition = Vector2.zero;
            HospitalManagementPanelController panel = HospitalManagementPanelController.Instance;
            if (panel != null)
            {
                m_window.localScale = panel.transform.localScale;
            }
            // The background takes the clicks so the game behind does not.
            Image background = m_window.gameObject.AddComponent<Image>();
            background.color = RPHUi.WindowColor;
            Image gamePanel = panel != null ? panel.GetComponent<Image>() : null;
            if (gamePanel != null)
            {
                // Same frame as the game's panels.
                background.sprite = gamePanel.sprite;
                background.type = gamePanel.type;
                background.color = gamePanel.color;
            }

            RectTransform title = RPHUi.Place(RPHUi.Rect(m_window, "Title"), new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -56f), Vector2.zero);
            title.gameObject.AddComponent<Image>().color = RPHUi.HeadingColor;
            title.gameObject.AddComponent<RPHDragHandle>().Target = m_window;
            Text heading = RPHUi.Label(title, RPHText.Get("RPH_STATS_TITLE"), 22, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            heading.alignment = TextAnchor.MiddleLeft;
            RPHUi.Place(heading.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-200f, 0f));
            m_clock = RPHUi.Label(title, string.Empty, 16, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            RPHUi.Place(m_clock.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-70f, 0f));
            CloseButton(title, panel);

            m_tabs = RPHUi.Place(RPHUi.Rect(m_window, "Tabs"), new Vector2(0f, 1f), Vector2.one, new Vector2(16f, -96f), new Vector2(-16f, -62f));
            m_ranges = RPHUi.Place(RPHUi.Rect(m_window, "Ranges"), new Vector2(0f, 1f), Vector2.one, new Vector2(16f, -132f), new Vector2(-16f, -102f));

            RectTransform viewport = RPHUi.Place(RPHUi.Rect(m_window, "Viewport"), Vector2.zero, Vector2.one, new Vector2(16f, 12f), new Vector2(-16f, -140f));
            viewport.gameObject.AddComponent<RectMask2D>();
            m_content = RPHUi.Rect(viewport, "Content");
            m_content.anchorMin = new Vector2(0f, 1f);
            m_content.anchorMax = Vector2.one;
            m_content.pivot = new Vector2(0.5f, 1f);
            m_content.offsetMin = m_content.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = m_content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 8, 0, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            m_content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            m_scroll = viewport.gameObject.AddComponent<ScrollRect>();
            m_scroll.content = m_content;
            m_scroll.viewport = viewport;
            m_scroll.horizontal = false;
            m_scroll.movementType = ScrollRect.MovementType.Clamped;
            m_scroll.scrollSensitivity = 30f;
        }

        /// <summary>Same canvas as the hospital management panel, so the window scales like the game's panels.</summary>
        private static Transform FindParent()
        {
            HospitalManagementPanelController panel = HospitalManagementPanelController.Instance;
            if (panel != null && panel.transform.parent != null)
            {
                return panel.transform.parent;
            }
            Canvas best = null;
            foreach (Canvas canvas in FindObjectsOfType<Canvas>())
            {
                if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace && (best == null || canvas.sortingOrder > best.sortingOrder))
                {
                    best = canvas;
                }
            }
            return best != null ? best.transform : null;
        }

        private void Rebuild()
        {
            m_builtHour = (int)DayTime.Instance.GetDayTimeHours();
            m_builtDay = RPHLedgerService.CurrentDay;
            int today = RPHLedgerService.CurrentDay;
            if (m_singleDay <= 0)
            {
                m_singleDay = today;
            }
            DayRange range = DayRange.Resolve(m_range, today, 1, m_singleDay);
            m_singleDay = range.To;

            m_clock.text = RPHText.Get("RPH_STATS_CLOCK", DayTime.Instance.GetDay().ToString(), DayTime.Instance.GetDayTimeHMS().ToStringHHMM());
            BuildTabs();
            BuildRanges(range, today);

            for (int i = m_content.childCount - 1; i >= 0; i--)
            {
                Destroy(m_content.GetChild(i).gameObject);
            }
            var report = new LedgerReport(RPHLedgerService.Ledger, range.From, range.To);
            switch (m_tab)
            {
                case Tab.Summary:
                    BuildSummary(report, range);
                    break;
                case Tab.Spending:
                    BuildCategories(report, CategoryGroup.Expense);
                    break;
                case Tab.Income:
                    BuildCategories(report, CategoryGroup.Income);
                    break;
                case Tab.Shifts:
                    BuildShifts(report);
                    break;
                case Tab.Departments:
                    BuildDepartments(report);
                    break;
                case Tab.Activity:
                    BuildActivity(report);
                    break;
            }
            BuildNotes(report);
        }

        private void BuildTabs()
        {
            Clear(m_tabs);
            HorizontalLayoutGroup row = RPHUi.Row(m_tabs, 34f, 6f);
            RPHUi.Place((RectTransform)row.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            row.childForceExpandWidth = true;
            foreach (Tab tab in Enum.GetValues(typeof(Tab)))
            {
                Tab target = tab;
                RPHUi.Button(row.transform, RPHText.Get("RPH_TAB_" + tab.ToString().ToUpperInvariant()), tab == m_tab, () =>
                {
                    m_tab = target;
                    m_scroll.verticalNormalizedPosition = 1f;
                    Rebuild();
                });
            }
        }

        private void BuildRanges(DayRange range, int today)
        {
            Clear(m_ranges);
            HorizontalLayoutGroup row = RPHUi.Row(m_ranges, 30f, 6f);
            RPHUi.Place((RectTransform)row.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RangeKind[] kinds = { RangeKind.Today, RangeKind.Yesterday, RangeKind.Last7Days, RangeKind.Last30Days, RangeKind.AllDays };
            foreach (RangeKind kind in kinds)
            {
                RangeKind target = kind;
                Button button = RPHUi.Button(row.transform, RPHText.Get("RPH_RANGE_" + kind.ToString().ToUpperInvariant()), kind == m_range, () =>
                {
                    m_range = target;
                    m_singleDay = 0;
                    Rebuild();
                });
                RPHUi.Size(button, 100f, -1f, 0f);
            }

            RPHUi.Size(RPHUi.Rect(row.transform, "Gap"), 20f, -1f, 0f);
            Text dayLabel = RPHUi.Label(row.transform, RPHText.Get("RPH_RANGE_DAY"), 14, RPHUi.MutedColor, TextAnchor.MiddleRight);
            RPHUi.Size(dayLabel, 40f, -1f, 0f);
            RPHUi.Size(RPHUi.Button(row.transform, "<", false, () => StepDay(-1, today)), 30f, -1f, 0f);
            Text day = RPHUi.Label(row.transform, range.To.ToString(), 14, RPHUi.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            RPHUi.Size(day, 40f, -1f, 0f);
            RPHUi.Size(RPHUi.Button(row.transform, ">", false, () => StepDay(1, today)), 30f, -1f, 0f);

            Text label = RPHUi.Label(row.transform, RangeTitle(range, today), 15, RPHUi.TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
            RPHUi.Size(label, -1f, -1f, 1f);
        }

        private void StepDay(int step, int today)
        {
            m_singleDay = Math.Max(1, Math.Min(today, m_singleDay + step));
            m_range = RangeKind.SingleDay;
            Rebuild();
        }

        private static string RangeTitle(DayRange range, int today)
        {
            if (range.From == range.To)
            {
                string key = range.To == today ? "RPH_TITLE_TODAY" : range.To == today - 1 ? "RPH_TITLE_YESTERDAY" : "RPH_TITLE_DAY";
                return RPHText.Get(key, range.To.ToString());
            }
            return RPHText.Get("RPH_TITLE_DAYS", range.From.ToString(), range.To.ToString());
        }

        // ---------------------------------------------------------------- tabs

        private void BuildSummary(LedgerReport report, DayRange range)
        {
            int days = Math.Max(1, report.DaysWithData);
            Budget budget = Hospital.Instance.m_state.m_budget;

            HorizontalLayoutGroup cards = RPHUi.Row(m_content, 84f, 10f);
            cards.childForceExpandWidth = true;
            Card(cards.transform, RPHText.Get("RPH_KPI_IN"), MoneyFormat.SignedMoney(report.MoneyIn), RPHUi.Green,
                RPHText.Get("RPH_KPI_PER_DAY", MoneyFormat.SignedMoney(report.MoneyIn / days)));
            Card(cards.transform, RPHText.Get("RPH_KPI_OUT"), MoneyFormat.SignedMoney(report.MoneyOut), RPHUi.Red,
                RPHText.Get("RPH_KPI_PER_DAY", MoneyFormat.SignedMoney(report.MoneyOut / days)));
            Card(cards.transform, RPHText.Get("RPH_KPI_PROFIT"), MoneyFormat.SignedMoney(report.Profit), RPHUi.MoneyColor(report.Profit),
                RPHText.Get("RPH_KPI_PER_DAY", MoneyFormat.SignedMoney(report.Profit / days)));
            Card(cards.transform, RPHText.Get("RPH_KPI_BALANCE"), MoneyFormat.Money(budget.m_currentBalance), Color.white,
                budget.m_loan > 0 ? RPHText.Get("RPH_KPI_LOAN", MoneyFormat.Money(budget.m_loan)) : RPHText.Get("RPH_KPI_NO_LOAN"));

            ProfitChart(range);

            HorizontalLayoutGroup columns = TwoColumns();
            Transform left = columns.transform.GetChild(0);
            Transform right = columns.transform.GetChild(1);
            Section(left, RPHText.Get("RPH_SECTION_CAME_FROM"));
            CategoryRows(left, report, CategoryGroup.Income);
            Section(right, RPHText.Get("RPH_SECTION_WENT"));
            CategoryRows(right, report, CategoryGroup.Expense);

            List<LedgerTotal> departments = report.GroupBy(l => l.Department, l => l.Department.Length > 0);
            columns = TwoColumns();
            left = columns.transform.GetChild(0);
            right = columns.transform.GetChild(1);
            Section(left, RPHText.Get("RPH_SECTION_DPT_MAKING"));
            foreach (LedgerTotal total in departments.Where(t => t.Net >= 0).OrderByDescending(t => t.Net))
            {
                ValueRow(left, RPHGameData.DepartmentIcon(total.Key), RPHGameData.DepartmentName(total.Key), total.Net, RPHUi.Green);
            }
            Section(right, RPHText.Get("RPH_SECTION_DPT_LOSING"));
            foreach (LedgerTotal total in departments.Where(t => t.Net < 0).OrderBy(t => t.Net))
            {
                ValueRow(right, RPHGameData.DepartmentIcon(total.Key), RPHGameData.DepartmentName(total.Key), total.Net, RPHUi.Yellow);
            }
        }

        private void BuildCategories(LedgerReport report, CategoryGroup group)
        {
            long groupTotal = group == CategoryGroup.Income ? report.MoneyIn : report.MoneyOut;
            Section(m_content, RPHText.Get(group == CategoryGroup.Income ? "RPH_SECTION_CAME_FROM" : "RPH_SECTION_WENT"));
            var categories = report.Lines.Where(l => l.Group == group).GroupBy(l => l.Category)
                .Select(g => new { Category = g.Key, Lines = g.ToList(), Amount = g.Sum(l => l.Amount), Count = g.Sum(l => l.Count) })
                .OrderByDescending(c => Math.Abs(c.Amount));
            foreach (var category in categories)
            {
                HeaderRow(m_content, null, RPHGameData.CategoryName(category.Category), category.Amount, groupTotal, category.Count);
                // Wages: by department and role. Inpatient care: by department and hospitalization type.
                // Income: by department (diagnoses below). Others: by department.
                foreach (var part in category.Lines.GroupBy(l => DetailKey(l, group))
                    .Select(g => new { Key = g.Key, Sample = g.First(), Amount = g.Sum(l => l.Amount), Count = g.Sum(l => l.Count) })
                    .OrderByDescending(p => Math.Abs(p.Amount)))
                {
                    string label = RPHGameData.DepartmentName(part.Sample.Department);
                    if (group == CategoryGroup.Expense && part.Sample.Source.Length > 0)
                    {
                        label += "  ·  " + RPHGameData.ExpenseSourceName(part.Sample.Source);
                    }
                    DetailRow(m_content, RPHGameData.DepartmentIcon(part.Sample.Department), label, part.Amount, part.Count);
                }
            }
            if (group == CategoryGroup.Income)
            {
                Section(m_content, RPHText.Get("RPH_SECTION_DIAGNOSES"));
                foreach (var diagnosis in report.Lines.Where(l => l.Group == CategoryGroup.Income && l.Source.Length > 0).GroupBy(l => l.Source)
                    .Select(g => new { Id = g.Key, Amount = g.Sum(l => l.Amount), Count = g.Sum(l => l.Count) })
                    .OrderByDescending(d => d.Amount).Take(20))
                {
                    DetailRow(m_content, null, RPHGameData.DiagnosisName(diagnosis.Id), diagnosis.Amount, diagnosis.Count);
                }
            }
        }

        private static string DetailKey(LedgerLine line, CategoryGroup group)
        {
            return group == CategoryGroup.Expense ? line.Department + "|" + line.Source : line.Department;
        }

        private void BuildShifts(LedgerReport report)
        {
            WorkShift[] shifts = { WorkShift.Day, WorkShift.Night, WorkShift.Unknown };
            HorizontalLayoutGroup header = RPHUi.Row(m_content, 28f, 8f);
            Cell(header.transform, string.Empty, -1f, RPHUi.MutedColor, TextAnchor.MiddleLeft);
            foreach (WorkShift shift in shifts)
            {
                Cell(header.transform, RPHText.Get("RPH_SHIFT_" + shift.ToString().ToUpperInvariant()), 150f, RPHUi.MutedColor, TextAnchor.MiddleRight);
            }

            ShiftRow(report, shifts, RPHText.Get("RPH_KPI_IN"), l => l.Group == CategoryGroup.Income, true);
            ShiftRow(report, shifts, RPHText.Get("RPH_KPI_OUT"), l => l.Group == CategoryGroup.Expense, true);
            ShiftRow(report, shifts, RPHText.Get("RPH_KPI_PROFIT"), l => l.Group != CategoryGroup.Financing, true);
            Section(m_content, RPHText.Get("RPH_SECTION_BY_CATEGORY"));
            foreach (LedgerCategory category in report.Lines.Where(l => l.Group != CategoryGroup.Financing).Select(l => l.Category).Distinct()
                .OrderBy(c => CategoryInfo.GetGroup(c)))
            {
                LedgerCategory target = category;
                ShiftRow(report, shifts, RPHGameData.CategoryName(category), l => l.Category == target, false);
            }
        }

        private void ShiftRow(LedgerReport report, WorkShift[] shifts, string label, Func<LedgerLine, bool> filter, bool bold)
        {
            HorizontalLayoutGroup row = RPHUi.Row(m_content, RowHeight, 8f);
            Cell(row.transform, label, -1f, RPHUi.TextColor, TextAnchor.MiddleLeft, bold ? FontStyle.Bold : FontStyle.Normal);
            foreach (WorkShift shift in shifts)
            {
                long amount = report.Lines.Where(l => l.Shift == shift && filter(l)).Sum(l => l.Amount);
                MoneyCell(row.transform, amount, 150f, RPHUi.MoneyColor(amount), bold);
            }
        }

        private void BuildDepartments(LedgerReport report)
        {
            HorizontalLayoutGroup header = RPHUi.Row(m_content, 28f, 8f);
            Cell(header.transform, string.Empty, -1f, RPHUi.MutedColor, TextAnchor.MiddleLeft);
            Cell(header.transform, RPHText.Get("RPH_KPI_IN"), ValueWidth, RPHUi.MutedColor, TextAnchor.MiddleRight);
            Cell(header.transform, RPHText.Get("RPH_KPI_OUT"), ValueWidth, RPHUi.MutedColor, TextAnchor.MiddleRight);
            Cell(header.transform, RPHText.Get("RPH_COLUMN_RESULT"), ValueWidth, RPHUi.MutedColor, TextAnchor.MiddleRight);

            foreach (LedgerTotal department in report.GroupBy(l => l.Department, null).OrderByDescending(t => t.Net))
            {
                HorizontalLayoutGroup row = RPHUi.Row(m_content, 28f, 8f);
                row.gameObject.AddComponent<Image>().color = RPHUi.SubPanelColor;
                row.padding = new RectOffset(6, 6, 2, 2);
                Icon(row.transform, RPHGameData.DepartmentIcon(department.Key));
                Cell(row.transform, RPHGameData.DepartmentName(department.Key), -1f, RPHUi.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
                MoneyCell(row.transform, department.In, ValueWidth, RPHUi.MoneyColor(department.In), false);
                MoneyCell(row.transform, department.Out, ValueWidth, RPHUi.MoneyColor(department.Out), false);
                MoneyCell(row.transform, department.Net, ValueWidth, department.Net < 0 && department.Key.Length > 0 ? RPHUi.Yellow : RPHUi.MoneyColor(department.Net), true);

                string id = department.Key;
                foreach (var category in report.Lines.Where(l => l.Department == id && l.Group != CategoryGroup.Financing).GroupBy(l => l.Category)
                    .Select(g => new { Category = g.Key, Amount = g.Sum(l => l.Amount), Count = g.Sum(l => l.Count) })
                    .OrderByDescending(c => c.Amount))
                {
                    DetailRow(m_content, null, RPHGameData.CategoryName(category.Category), category.Amount, category.Count);
                }
            }
        }

        private void BuildActivity(LedgerReport report)
        {
            if (report.Activities.Count == 0)
            {
                Note(RPHText.Get("RPH_NOTE_NO_ACTIVITY"));
                return;
            }
            foreach (ActivityKind kind in Enum.GetValues(typeof(ActivityKind)))
            {
                ActivityKind target = kind;
                List<ActivityLine> lines = report.Activities.Where(a => a.Kind == target).OrderByDescending(a => a.Count).ToList();
                if (lines.Count == 0)
                {
                    continue;
                }
                Section(m_content, RPHText.Get("RPH_ACTIVITY_" + kind.ToString().ToUpperInvariant()) + "  (" + lines.Sum(a => a.Count) + ")");
                foreach (ActivityLine line in lines)
                {
                    HorizontalLayoutGroup row = RPHUi.Row(m_content, RowHeight, 8f);
                    row.padding = new RectOffset(16, 0, 0, 0);
                    Icon(row.transform, RPHGameData.DepartmentIcon(line.Department));
                    Cell(row.transform, RPHGameData.ProcedureName(line.Kind, line.ProcedureId), -1f, RPHUi.TextColor, TextAnchor.MiddleLeft);
                    Cell(row.transform, RPHGameData.DepartmentName(line.Department), 260f, RPHUi.MutedColor, TextAnchor.MiddleLeft);
                    Cell(row.transform, "×" + line.Count, 70f, RPHUi.TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
                }
            }
        }

        private void BuildNotes(LedgerReport report)
        {
            if (report.HasImportedDays)
            {
                Note(RPHText.Get("RPH_NOTE_IMPORTED"));
            }
            if (report.HasPartialDays)
            {
                Note(RPHText.Get("RPH_NOTE_PARTIAL"));
            }
            if (report.ReconciliationError != 0)
            {
                Note(RPHText.Get("RPH_NOTE_RECONCILIATION", MoneyFormat.SignedMoney(report.ReconciliationError)));
            }
        }

        // ---------------------------------------------------------------- widgets

        private static void Card(Transform parent, string title, string value, Color color, string footer)
        {
            Image card = RPHUi.Box(parent, "Card", RPHUi.CellColor);
            RPHUi.Size(card, -1f, -1f, 1f);
            Text titleText = RPHUi.Label(card.transform, title, 13, RPHUi.DimLightTextColor, TextAnchor.UpperLeft);
            RPHUi.Place(titleText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-8f, -8f));
            Text valueText = RPHUi.Label(card.transform, value, 24, color, TextAnchor.MiddleLeft, FontStyle.Bold);
            RPHUi.Place(valueText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-8f, 2f));
            Text footerText = RPHUi.Label(card.transform, footer, 12, RPHUi.DimLightTextColor, TextAnchor.LowerLeft);
            RPHUi.Place(footerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 8f), new Vector2(-8f, 0f));
        }

        /// <summary>Profit or loss of each of the last 30 days; the days of the chosen period are bright.</summary>
        private void ProfitChart(DayRange range)
        {
            List<DayProfit> days = LedgerReport.DailyProfit(RPHLedgerService.Ledger, Math.Max(1, range.To - 29), range.To);
            Image panel = RPHUi.Box(m_content, "Chart", RPHUi.CellColor);
            RPHUi.Size(panel, -1f, 190f, 1f);
            Text title = RPHUi.Label(panel.transform, RPHText.Get("RPH_CHART_TITLE"), 13, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            RPHUi.Place(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, -8f));
            if (days.Count == 0)
            {
                return;
            }

            RectTransform area = RPHUi.Place(RPHUi.Rect(panel.transform, "Bars"), Vector2.zero, Vector2.one, new Vector2(16f, 22f), new Vector2(-16f, -40f));
            RPHUi.Place(RPHUi.Box(area, "Zero", RPHUi.ZeroLineColor).rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 1f));
            long max = Math.Max(1L, days.Max(d => Math.Abs(d.Profit)));
            float slot = 1f / days.Count;
            for (int i = 0; i < days.Count; i++)
            {
                DayProfit day = days[i];
                bool inRange = range.Contains(day.Day);
                Color color = day.Profit >= 0 ? RPHUi.Green : RPHUi.Red;
                color.a = inRange ? 1f : 0.35f;
                float height = 0.5f * day.Profit / max;
                float x0 = slot * (i + 0.2f);
                float x1 = slot * (i + 0.8f);
                Image bar = RPHUi.Box(area, "Bar", color);
                RPHUi.Place(bar.rectTransform, new Vector2(x0, Math.Min(0.5f, 0.5f + height)), new Vector2(x1, Math.Max(0.5f, 0.5f + height)), Vector2.zero, new Vector2(0f, 1f));

                Text value = RPHUi.Label(area, day.Profit == 0 ? string.Empty : MoneyFormat.ShortMoney(day.Profit), 10, inRange ? Color.white : RPHUi.DimLightTextColor, day.Profit >= 0 ? TextAnchor.LowerCenter : TextAnchor.UpperCenter);
                float y = 0.5f + height;
                RPHUi.Place(value.rectTransform, new Vector2(x0 - slot, y), new Vector2(x1 + slot, y), day.Profit >= 0 ? new Vector2(0f, 2f) : new Vector2(0f, -16f), day.Profit >= 0 ? new Vector2(0f, 16f) : new Vector2(0f, -2f));
                Text label = RPHUi.Label(area, day.Day.ToString(), 11, inRange ? Color.white : RPHUi.DimLightTextColor, TextAnchor.UpperCenter, inRange ? FontStyle.Bold : FontStyle.Normal);
                RPHUi.Place(label.rectTransform, new Vector2(x0, 0f), new Vector2(x1, 0f), new Vector2(-10f, -20f), new Vector2(10f, -4f));
            }
        }

        private HorizontalLayoutGroup TwoColumns()
        {
            HorizontalLayoutGroup row = RPHUi.Row(m_content, -1f, 24f);
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = false;
            for (int i = 0; i < 2; i++)
            {
                RPHUi.Size(RPHUi.Column(row.transform, 2f), -1f, -1f, 1f);
            }
            return row;
        }

        /// <summary>Dark heading bar with white text, like the game's "Patients" / "Workloads" headings.</summary>
        private static void Section(Transform parent, string title)
        {
            Image bar = RPHUi.Box(parent, "Heading", RPHUi.HeadingColor);
            RPHUi.Size(bar, -1f, 26f, 1f);
            Text text = RPHUi.FillLabel(bar.transform, title, 14, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            text.rectTransform.offsetMin = new Vector2(10f, 0f);
        }

        private void Note(string text)
        {
            Text note = RPHUi.Label(m_content, text, 12, RPHUi.MutedColor, TextAnchor.UpperLeft);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            RPHUi.Size(note, -1f, -1f, 1f);
        }

        /// <summary>Category row with share of the group and a bar, like the game's budget tab.</summary>
        private static void CategoryRows(Transform parent, LedgerReport report, CategoryGroup group)
        {
            long groupTotal = group == CategoryGroup.Income ? report.MoneyIn : report.MoneyOut;
            foreach (var category in report.Lines.Where(l => l.Group == group).GroupBy(l => l.Category)
                .Select(g => new { Category = g.Key, Amount = g.Sum(l => l.Amount), Count = g.Sum(l => l.Count) })
                .OrderByDescending(c => Math.Abs(c.Amount)))
            {
                HeaderRow(parent, null, RPHGameData.CategoryName(category.Category), category.Amount, groupTotal, -1);
            }
        }

        private static void HeaderRow(Transform parent, Sprite icon, string label, long amount, long groupTotal, int count)
        {
            HorizontalLayoutGroup row = RPHUi.Row(parent, 26f, 8f);
            row.gameObject.AddComponent<Image>().color = RPHUi.RowAltColor;
            row.padding = new RectOffset(6, 6, 0, 0);
            Icon(row.transform, icon);
            Cell(row.transform, label, -1f, RPHUi.TextColor, TextAnchor.MiddleLeft);
            if (count >= 0)
            {
                Cell(row.transform, count > 0 ? "×" + count : string.Empty, 60f, RPHUi.MutedColor, TextAnchor.MiddleRight);
            }
            MoneyCell(row.transform, amount, ValueWidth, RPHUi.MoneyColor(amount), false);
            Cell(row.transform, MoneyFormat.Percent(amount, groupTotal), PercentWidth, RPHUi.MutedColor, TextAnchor.MiddleRight);
            Bar(row.transform, groupTotal == 0 ? 0f : (float)amount / groupTotal);
        }

        private static void DetailRow(Transform parent, Sprite icon, string label, long amount, int count)
        {
            HorizontalLayoutGroup row = RPHUi.Row(parent, 22f, 8f);
            row.padding = new RectOffset(28, 6, 0, 0);
            Icon(row.transform, icon);
            Cell(row.transform, label, -1f, RPHUi.TextColor, TextAnchor.MiddleLeft);
            Cell(row.transform, count > 0 ? "×" + count : string.Empty, 60f, RPHUi.MutedColor, TextAnchor.MiddleRight);
            MoneyCell(row.transform, amount, ValueWidth, RPHUi.MoneyColor(amount), false);
            Cell(row.transform, string.Empty, PercentWidth + BarWidth + 8f, RPHUi.MutedColor, TextAnchor.MiddleRight);
        }

        private static void ValueRow(Transform parent, Sprite icon, string label, long amount, Color color)
        {
            HorizontalLayoutGroup row = RPHUi.Row(parent, RowHeight, 8f);
            row.padding = new RectOffset(6, 6, 0, 0);
            Icon(row.transform, icon);
            Cell(row.transform, label, -1f, RPHUi.TextColor, TextAnchor.MiddleLeft);
            MoneyCell(row.transform, amount, ValueWidth, color, true);
        }

        /// <summary>A money value in a dark gray cell with pale green / red / yellow text, like the game's budget tab.</summary>
        private static void MoneyCell(Transform row, long amount, float width, Color color, bool bold)
        {
            Image cell = RPHUi.Box(row, "Money", RPHUi.CellColor);
            RPHUi.Size(cell, width, -1f, 0f);
            Text text = RPHUi.FillLabel(cell.transform, MoneyFormat.SignedMoney(amount), 14, color, TextAnchor.MiddleRight, bold ? FontStyle.Bold : FontStyle.Normal);
            text.rectTransform.offsetMax = new Vector2(-6f, 0f);
        }

        /// <summary>The game's own close button (cloned from the hospital management panel), or a plain one.</summary>
        private void CloseButton(RectTransform title, HospitalManagementPanelController panel)
        {
            if (panel != null && panel.m_closeButton != null)
            {
                GameObject clone = Instantiate(panel.m_closeButton, title, false);
                IconButtonController controller = clone.GetComponent<IconButtonController>();
                if (controller != null)
                {
                    controller.RemoveOnClickDelegate();
                    controller.SetOnClickedDelegate(Close);
                    RectTransform rect = (RectTransform)clone.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
                    rect.anchoredPosition = new Vector2(-26f, 0f);
                    return;
                }
                Destroy(clone);
            }
            Button close = RPHUi.Button(title, "X", false, Close);
            RPHUi.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-54f, -16f), new Vector2(-14f, 16f));
        }

        private static void Cell(Transform row, string text, float width, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            Text label = RPHUi.Label(row, text, 14, color, anchor, style);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            RPHUi.Size(label, width, -1f, width < 0f ? 1f : 0f);
        }

        private static void Icon(Transform row, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }
            Image image = RPHUi.Box(row, "Icon", Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            RPHUi.Size(image, 20f, 20f, 0f);
        }

        private static void Bar(Transform row, float fraction)
        {
            RectTransform slot = RPHUi.Rect(row, "BarSlot");
            RPHUi.Size(slot, BarWidth, -1f, 0f);
            RPHUi.Place(RPHUi.Box(slot, "Track", new Color(0f, 0f, 0f, 0.08f)).rectTransform, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f), Vector2.zero, Vector2.zero);
            fraction = Mathf.Clamp01(Math.Abs(fraction));
            RPHUi.Place(RPHUi.Box(slot, "Fill", RPHUi.CellColor).rectTransform, new Vector2(0f, 0.3f), new Vector2(fraction, 0.7f), Vector2.zero, Vector2.zero);
        }

        private static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}
