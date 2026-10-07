using System.Linq;
using RPH.Core.Accounting;
using Xunit;

namespace RPH.Core.Tests;

public class LedgerRecorderTests
{
    [Fact]
    public void BalanceChangeOutsideScopeIsBookedHospitalWide()
    {
        var recorder = new LedgerRecorder(new Ledger());
        recorder.StartDay(3, 100_000, 0f);

        recorder.OnBalanceChanged(-2_500, LedgerCategory.HiringAgency, WorkShift.Day);

        LedgerLine line = recorder.Ledger.GetDay(3)!.Lines.Single();
        Assert.Equal(LedgerCategory.HiringAgency, line.Category);
        Assert.Equal(string.Empty, line.Department);
        Assert.Equal(-2_500, line.Amount);
        Assert.Equal(1, line.Count);
    }

    [Fact]
    public void ScopeAddsDepartmentAndSource()
    {
        var recorder = new LedgerRecorder(new Ledger());
        recorder.StartDay(1, 0, 0f);

        recorder.BeginScope("DPT_EMERGENCY", "DIAG_FLU");
        recorder.OnBalanceChanged(1_200, LedgerCategory.InsuranceClinic, WorkShift.Day);
        recorder.EndScope(WorkShift.Day, null);

        LedgerLine line = recorder.Ledger.GetDay(1)!.Lines.Single();
        Assert.Equal("DPT_EMERGENCY", line.Department);
        Assert.Equal("DIAG_FLU", line.Source);
        Assert.Equal(LedgerCategory.InsuranceClinic, line.Category);
        Assert.Equal(1_200, line.Amount);
    }

    [Fact]
    public void ScopeCategoryOverrideWins()
    {
        var recorder = new LedgerRecorder(new Ledger());
        recorder.StartDay(1, 0, 0f);

        recorder.BeginScope("DPT_CARDIOLOGY", "DOCTOR");
        recorder.OnBalanceChanged(-300, LedgerCategory.Wages, WorkShift.Night);
        recorder.EndScope(WorkShift.Night, LedgerCategory.WagesHospitalization);

        Assert.Equal(LedgerCategory.WagesHospitalization, recorder.Ledger.GetDay(1)!.Lines.Single().Category);
    }

    [Fact]
    public void EmptyScopeBooksNothing()
    {
        var recorder = new LedgerRecorder(new Ledger());
        recorder.StartDay(1, 0, 0f);

        recorder.BeginScope("DPT_EMERGENCY", "");
        recorder.EndScope(WorkShift.Day, null);

        Assert.Empty(recorder.Ledger.GetDay(1)!.Lines);
    }

    [Fact]
    public void SameKeyIsAggregated()
    {
        var recorder = new LedgerRecorder(new Ledger());
        recorder.StartDay(1, 0, 0f);

        recorder.Book(LedgerCategory.Building, "", "", WorkShift.Day, -500, 1);
        recorder.Book(LedgerCategory.Building, "", "", WorkShift.Day, -700, 1);

        LedgerLine line = recorder.Ledger.GetDay(1)!.Lines.Single();
        Assert.Equal(-1_200, line.Amount);
        Assert.Equal(2, line.Count);
    }

    [Fact]
    public void CloseDayReconcilesEveryDollar()
    {
        var recorder = new LedgerRecorder(new Ledger());
        recorder.StartDay(2, 10_000, 0f);
        recorder.OnBalanceChanged(5_000, LedgerCategory.InsuranceClinic, WorkShift.Day);
        recorder.OnBalanceChanged(-1_000, LedgerCategory.Building, WorkShift.Day);
        recorder.OnBalanceChanged(20_000, LedgerCategory.LoanTaken, WorkShift.Day);

        recorder.CloseDay(34_000);

        Assert.Equal(0, recorder.Ledger.GetDay(2)!.ReconciliationError);
    }

    [Fact]
    public void CloseDayReportsMissingMoney()
    {
        var recorder = new LedgerRecorder(new Ledger());
        recorder.StartDay(2, 10_000, 0f);
        recorder.OnBalanceChanged(5_000, LedgerCategory.InsuranceClinic, WorkShift.Day);

        recorder.CloseDay(14_000);

        Assert.Equal(-1_000, recorder.Ledger.GetDay(2)!.ReconciliationError);
    }

    [Fact]
    public void ResumingADayKeepsItsStartBalance()
    {
        var recorder = new LedgerRecorder(new Ledger());
        recorder.StartDay(4, 50_000, 0f);
        recorder.StartDay(4, 60_000, 13f);

        DayBook book = recorder.Ledger.GetDay(4)!;
        Assert.Equal(50_000, book.StartBalance);
        Assert.False(book.IsPartial);
    }

    [Fact]
    public void DayStartedMidwayIsPartial()
    {
        var recorder = new LedgerRecorder(new Ledger());
        recorder.StartDay(4, 50_000, 13.5f);

        Assert.True(recorder.Ledger.GetDay(4)!.IsPartial);
    }
}

public class LedgerReportTests
{
    private static Ledger SampleLedger()
    {
        var ledger = new Ledger();
        DayBook d1 = ledger.GetOrCreateDay(1);
        d1.Add(LedgerCategory.InsuranceClinic, "DPT_EMERGENCY", "A", WorkShift.Day, 10_000, 4);
        d1.Add(LedgerCategory.WagesClinic, "DPT_EMERGENCY", "DOCTOR", WorkShift.Day, -3_000, 2);
        d1.Add(LedgerCategory.Building, "", "", WorkShift.Day, -5_000, 3);
        d1.Add(LedgerCategory.LoanTaken, "", "", WorkShift.Day, 20_000, 1);
        DayBook d2 = ledger.GetOrCreateDay(2);
        d2.Add(LedgerCategory.InsuranceClinic, "DPT_EMERGENCY", "A", WorkShift.Day, 6_000, 2);
        d2.Add(LedgerCategory.InsuranceHospitalization, "DPT_CARDIOLOGY", "B", WorkShift.Night, 8_000, 1);
        d2.AddActivity(ActivityKind.Examination, "EXAM_CT", "DPT_RADIOLOGY", 3);
        return ledger;
    }

    [Fact]
    public void TotalsExcludeFinancing()
    {
        var report = new LedgerReport(SampleLedger(), 1, 2);

        Assert.Equal(24_000, report.MoneyIn);
        Assert.Equal(-8_000, report.MoneyOut);
        Assert.Equal(16_000, report.Profit);
        Assert.Equal(20_000, report.Financing);
        Assert.Equal(2, report.DaysWithData);
    }

    [Fact]
    public void LinesAreMergedAcrossDays()
    {
        var report = new LedgerReport(SampleLedger(), 1, 2);

        LedgerLine clinic = report.Lines.Single(l => l.Category == LedgerCategory.InsuranceClinic);
        Assert.Equal(16_000, clinic.Amount);
        Assert.Equal(6, clinic.Count);
    }

    [Fact]
    public void RangeFiltersDays()
    {
        var report = new LedgerReport(SampleLedger(), 2, 2);

        Assert.Equal(14_000, report.MoneyIn);
        Assert.Equal(0, report.MoneyOut);
        Assert.Single(report.Activities);
    }

    [Fact]
    public void GroupByDepartmentGivesInOutAndNet()
    {
        var report = new LedgerReport(SampleLedger(), 1, 2);

        var byDepartment = report.GroupBy(l => l.Department, null).ToDictionary(t => t.Key);

        Assert.Equal(16_000, byDepartment["DPT_EMERGENCY"].In);
        Assert.Equal(-3_000, byDepartment["DPT_EMERGENCY"].Out);
        Assert.Equal(13_000, byDepartment["DPT_EMERGENCY"].Net);
        Assert.Equal(-5_000, byDepartment[""].Net);
        Assert.False(byDepartment.ContainsKey("LOAN"));
    }

    [Fact]
    public void DailyProfitIgnoresLoans()
    {
        var days = LedgerReport.DailyProfit(SampleLedger(), 1, 2);

        Assert.Equal(new long[] { 2_000, 14_000 }, days.Select(d => d.Profit).ToArray());
    }
}

public class LedgerSerializerTests
{
    [Fact]
    public void RoundTripKeepsEverything()
    {
        var ledger = new Ledger();
        DayBook imported = ledger.GetOrCreateDay(1);
        imported.Imported = true;
        imported.Add(LedgerCategory.InsuranceClinic, "DPT_EMERGENCY", "", WorkShift.Unknown, 9_000, 0);
        DayBook tracked = ledger.GetOrCreateDay(2);
        tracked.StartBalance = 120_000;
        tracked.StartHour = 13.25f;
        tracked.Add(LedgerCategory.WagesHospitalization, "DPT_CARDIOLOGY", "NURSE", WorkShift.Night, -450, 3);
        tracked.AddActivity(ActivityKind.LabExamination, "EXAM_BLOOD", "DPT_LAB", 7);
        tracked.Close(119_550);

        Ledger copy = LedgerSerializer.ReadFromString(LedgerSerializer.WriteToString(ledger));

        Assert.Equal(LedgerSerializer.WriteToString(ledger), LedgerSerializer.WriteToString(copy));
        DayBook day2 = copy.GetDay(2)!;
        Assert.Equal(120_000, day2.StartBalance);
        Assert.Equal(13.25f, day2.StartHour);
        Assert.Equal(0, day2.ReconciliationError);
        Assert.Equal(7, day2.Activities.Single().Count);
        Assert.True(copy.GetDay(1)!.Imported);
        Assert.Null(copy.GetDay(1)!.StartBalance);
    }

    [Fact]
    public void TabsInIdsDoNotBreakTheFile()
    {
        var ledger = new Ledger();
        ledger.GetOrCreateDay(1).Add(LedgerCategory.Food, "DPT\tX", "a\nb", WorkShift.Day, 5, 1);

        Ledger copy = LedgerSerializer.ReadFromString(LedgerSerializer.WriteToString(ledger));

        Assert.Equal("DPT X", copy.GetDay(1)!.Lines.Single().Department);
    }

    [Fact]
    public void UnknownCategoryIsKeptAsUnknown()
    {
        string text = "RPH-LEDGER\t1\nDAY\t1\t0\t-\t0\t-\t-\nL\tSomethingNew\t\t\tDay\t42\t1\n";

        Ledger ledger = LedgerSerializer.ReadFromString(text);

        Assert.Equal(LedgerCategory.Unknown, ledger.GetDay(1)!.Lines.Single().Category);
    }

    [Fact]
    public void RejectsNewerFormat()
    {
        Assert.Throws<System.FormatException>(() => LedgerSerializer.ReadFromString("RPH-LEDGER\t99\n"));
    }

    [Fact]
    public void RejectsOtherFiles()
    {
        Assert.Throws<System.FormatException>(() => LedgerSerializer.ReadFromString("hello"));
    }
}

public class DayRangeTests
{
    [Theory]
    [InlineData(RangeKind.Today, 10, 1, 0, 10, 10)]
    [InlineData(RangeKind.Yesterday, 10, 1, 0, 9, 9)]
    [InlineData(RangeKind.Yesterday, 1, 1, 0, 1, 1)]
    [InlineData(RangeKind.Last7Days, 10, 1, 0, 4, 10)]
    [InlineData(RangeKind.Last7Days, 3, 1, 0, 1, 3)]
    [InlineData(RangeKind.Last30Days, 40, 1, 0, 11, 40)]
    [InlineData(RangeKind.AllDays, 40, 5, 0, 5, 40)]
    [InlineData(RangeKind.SingleDay, 40, 1, 12, 12, 12)]
    [InlineData(RangeKind.SingleDay, 40, 1, 99, 40, 40)]
    public void Resolve(RangeKind kind, int today, int first, int single, int from, int to)
    {
        DayRange range = DayRange.Resolve(kind, today, first, single);

        Assert.Equal(from, range.From);
        Assert.Equal(to, range.To);
    }
}
