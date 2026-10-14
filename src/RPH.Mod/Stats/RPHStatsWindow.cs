using System;
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
    /// "Advanced statistics" window: one sidebar entry per domain, each with its own tabs. Budget: income statement,
    /// then income and spending by department and category, for any period. Read-only view of the ledger.
    /// Opened with F9 or the extra tab button of the hospital management panel.
    /// </summary>
    internal sealed class RPHStatsWindow : MonoBehaviour
    {
        /// <summary>Sidebar entries. Each new domain (HR, research...) adds its entry and its tabs here.</summary>
        private enum Domain
        {
            Budget,
        }

        private enum BudgetTab
        {
            Summary,
            Income,
            Spending,
        }

        private const KeyCode Hotkey = KeyCode.F9;
        private const float Width = 1000f;
        private const float Height = 660f;
        private const float SidebarWidth = 170f;
        private const float ValueWidth = 110f;
        private const float PercentWidth = 48f;
        private const float BarWidth = 60f;
        // Share and bar of a department row; left empty on the rows around it so the amounts line up.
        private const float ShareWidth = PercentWidth + 8f + BarWidth;
        private const float RowHeight = 24f;

        public static RPHStatsWindow Instance { get; private set; }

        private RectTransform m_window;
        private RectTransform m_domains;
        private RectTransform m_tabs;
        private RectTransform m_ranges;
        private RectTransform m_content;
        private Text m_clock;
        private ScrollRect m_scroll;

        private Domain m_domain = Domain.Budget;
        private BudgetTab m_tab = BudgetTab.Summary;
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

            Image sidebar = RPHUi.Box(m_window, "Sidebar", RPHUi.SubPanelColor);
            RPHUi.Place(sidebar.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(SidebarWidth, -56f));
            m_domains = RPHUi.Place(RPHUi.Rect(sidebar.transform, "Domains"), Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));

            float left = SidebarWidth + 16f;
            m_tabs = RPHUi.Place(RPHUi.Rect(m_window, "Tabs"), new Vector2(0f, 1f), Vector2.one, new Vector2(left, -96f), new Vector2(-16f, -62f));
            m_ranges = RPHUi.Place(RPHUi.Rect(m_window, "Ranges"), new Vector2(0f, 1f), Vector2.one, new Vector2(left, -132f), new Vector2(-16f, -102f));

            RectTransform viewport = RPHUi.Place(RPHUi.Rect(m_window, "Viewport"), Vector2.zero, Vector2.one, new Vector2(left, 12f), new Vector2(-16f, -140f));
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
            BuildDomains();
            BuildTabs(range, today);
            BuildRanges(range, today);

            for (int i = m_content.childCount - 1; i >= 0; i--)
            {
                Destroy(m_content.GetChild(i).gameObject);
            }
            var report = new LedgerReport(RPHLedgerService.Ledger, range.From, range.To);
            switch (m_tab)
            {
                case BudgetTab.Summary:
                    BuildSummary(report);
                    break;
                case BudgetTab.Income:
                    BuildByDepartment(report, CategoryGroup.Income);
                    break;
                case BudgetTab.Spending:
                    BuildByDepartment(report, CategoryGroup.Expense);
                    break;
            }
            BuildNotes(report);
        }

        private void BuildDomains()
        {
            Clear(m_domains);
            VerticalLayoutGroup column = RPHUi.Column(m_domains, 6f);
            RPHUi.Place((RectTransform)column.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            foreach (Domain domain in Enum.GetValues(typeof(Domain)))
            {
                Domain target = domain;
                Button button = RPHUi.Button(column.transform, RPHText.Get("RPH_DOMAIN_" + domain.ToString().ToUpperInvariant()), domain == m_domain, () =>
                {
                    m_domain = target;
                    m_scroll.verticalNormalizedPosition = 1f;
                    Rebuild();
                });
                RPHUi.Size(button, -1f, 36f, 1f);
            }
        }

        private void BuildTabs(DayRange range, int today)
        {
            Clear(m_tabs);
            HorizontalLayoutGroup row = RPHUi.Row(m_tabs, 34f, 6f);
            RPHUi.Place((RectTransform)row.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            foreach (BudgetTab tab in Enum.GetValues(typeof(BudgetTab)))
            {
                BudgetTab target = tab;
                Button button = RPHUi.Button(row.transform, RPHText.Get("RPH_TAB_" + tab.ToString().ToUpperInvariant()), tab == m_tab, () =>
                {
                    m_tab = target;
                    m_scroll.verticalNormalizedPosition = 1f;
                    Rebuild();
                });
                RPHUi.Size(button, 150f, -1f, 0f);
            }

            Text label = RPHUi.Label(row.transform, RangeTitle(range, today), 15, RPHUi.TextColor, TextAnchor.MiddleRight, FontStyle.Bold);
            RPHUi.Size(label, -1f, -1f, 1f);
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

        // ---------------------------------------------------------------- budget tabs

        /// <summary>Income statement: income and spending by category, the result, then the hospital's cash now.</summary>
        private void BuildSummary(LedgerReport report)
        {
            Heading(RPHText.Get("RPH_TAB_INCOME"), MoneyFormat.SignedMoney(report.MoneyIn), RPHUi.MoneyColor(report.MoneyIn), 0f);
            StatementCategories(report, CategoryGroup.Income);
            Heading(RPHText.Get("RPH_TAB_SPENDING"), MoneyFormat.SignedMoney(report.MoneyOut), RPHUi.MoneyColor(report.MoneyOut), 0f);
            StatementCategories(report, CategoryGroup.Expense);
            Heading(RPHText.Get("RPH_BUDGET_RESULT"), MoneyFormat.SignedMoney(report.Profit), RPHUi.MoneyColor(report.Profit), 0f);
            if (report.DaysWithData > 1)
            {
                long perDay = report.Profit / report.DaysWithData;
                StatementLine(RPHText.Get("RPH_BUDGET_PER_DAY"), MoneyFormat.SignedMoney(perDay), RPHUi.MoneyColor(perDay));
            }

            Budget budget = Hospital.Instance.m_state.m_budget;
            Heading(RPHText.Get("RPH_BUDGET_CASH"), string.Empty, Color.white, 0f);
            StatementLine(RPHText.Get("RPH_BUDGET_BALANCE"), MoneyFormat.Money(budget.m_currentBalance), RPHUi.MoneyColor(budget.m_currentBalance));
            StatementLine(RPHText.Get("RPH_BUDGET_LOAN"), MoneyFormat.Money(budget.m_loan), RPHUi.LightTextColor);
        }

        private void StatementCategories(LedgerReport report, CategoryGroup group)
        {
            foreach (var category in report.Lines.Where(l => l.Group == group).GroupBy(l => l.Category)
                .Select(g => new { Category = g.Key, Amount = g.Sum(l => l.Amount) })
                .OrderByDescending(c => Math.Abs(c.Amount)))
            {
                StatementLine(RPHGameData.CategoryName(category.Category), MoneyFormat.SignedMoney(category.Amount), RPHUi.MoneyColor(category.Amount));
            }
        }

        /// <summary>Income or spending by department, each split by category.</summary>
        private void BuildByDepartment(LedgerReport report, CategoryGroup group)
        {
            long groupTotal = group == CategoryGroup.Income ? report.MoneyIn : report.MoneyOut;
            Heading(RPHText.Get(group == CategoryGroup.Income ? "RPH_TAB_INCOME" : "RPH_TAB_SPENDING"), MoneyFormat.SignedMoney(groupTotal), RPHUi.MoneyColor(groupTotal), ShareWidth);
            foreach (LedgerTotal department in report.GroupBy(l => l.Department, l => l.Group == group).OrderByDescending(t => Math.Abs(t.Net)))
            {
                DepartmentRow(department.Key, department.Net, groupTotal);
                string id = department.Key;
                foreach (var category in report.Lines.Where(l => l.Group == group && l.Department == id).GroupBy(l => l.Category)
                    .Select(g => new { Category = g.Key, Amount = g.Sum(l => l.Amount), Count = g.Sum(l => l.Count) })
                    .OrderByDescending(c => Math.Abs(c.Amount)))
                {
                    DetailRow(RPHGameData.CategoryName(category.Category), category.Amount, category.Count);
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

        /// <summary>Dark heading bar with white text and a total, like the game's "Patients" / "Workloads" headings.</summary>
        private void Heading(string title, string value, Color color, float trailing)
        {
            HorizontalLayoutGroup row = RPHUi.Row(m_content, 28f, 8f);
            row.gameObject.AddComponent<Image>().color = RPHUi.HeadingColor;
            row.padding = new RectOffset(10, 6, 0, 0);
            Cell(row.transform, title, -1f, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            MoneyCell(row.transform, value, color, true);
            if (trailing > 0f)
            {
                Cell(row.transform, string.Empty, trailing, RPHUi.MutedColor, TextAnchor.MiddleRight);
            }
        }

        private void StatementLine(string label, string value, Color color)
        {
            HorizontalLayoutGroup row = RPHUi.Row(m_content, RowHeight, 8f);
            row.padding = new RectOffset(28, 6, 0, 0);
            Cell(row.transform, label, -1f, RPHUi.TextColor, TextAnchor.MiddleLeft);
            MoneyCell(row.transform, value, color, false);
        }

        /// <summary>Department total with its share of the group and a bar.</summary>
        private void DepartmentRow(string department, long amount, long groupTotal)
        {
            HorizontalLayoutGroup row = RPHUi.Row(m_content, 28f, 8f);
            row.gameObject.AddComponent<Image>().color = RPHUi.SubPanelColor;
            row.padding = new RectOffset(6, 6, 2, 2);
            Icon(row.transform, RPHGameData.DepartmentIcon(department));
            Cell(row.transform, RPHGameData.DepartmentName(department), -1f, RPHUi.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            MoneyCell(row.transform, MoneyFormat.SignedMoney(amount), RPHUi.MoneyColor(amount), true);
            Cell(row.transform, MoneyFormat.Percent(amount, groupTotal), PercentWidth, RPHUi.MutedColor, TextAnchor.MiddleRight);
            Bar(row.transform, groupTotal == 0 ? 0f : (float)amount / groupTotal);
        }

        private void DetailRow(string label, long amount, int count)
        {
            HorizontalLayoutGroup row = RPHUi.Row(m_content, 22f, 8f);
            row.padding = new RectOffset(34, 6, 0, 0);
            Cell(row.transform, label, -1f, RPHUi.TextColor, TextAnchor.MiddleLeft);
            Cell(row.transform, count > 0 ? "×" + count : string.Empty, 60f, RPHUi.MutedColor, TextAnchor.MiddleRight);
            MoneyCell(row.transform, MoneyFormat.SignedMoney(amount), RPHUi.MoneyColor(amount), false);
            Cell(row.transform, string.Empty, ShareWidth, RPHUi.MutedColor, TextAnchor.MiddleRight);
        }

        private void Note(string text)
        {
            Text note = RPHUi.Label(m_content, text, 12, RPHUi.MutedColor, TextAnchor.UpperLeft);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            RPHUi.Size(note, -1f, -1f, 1f);
        }

        /// <summary>A money value in a dark gray cell with pale green / red text, like the game's budget tab.</summary>
        private static void MoneyCell(Transform row, string text, Color color, bool bold)
        {
            Image cell = RPHUi.Box(row, "Money", RPHUi.CellColor);
            RPHUi.Size(cell, ValueWidth, -1f, 0f);
            Text label = RPHUi.FillLabel(cell.transform, text, 14, color, TextAnchor.MiddleRight, bold ? FontStyle.Bold : FontStyle.Normal);
            label.rectTransform.offsetMax = new Vector2(-6f, 0f);
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
