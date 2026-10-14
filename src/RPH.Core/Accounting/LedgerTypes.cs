namespace RPH.Core.Accounting
{
    /// <summary>What a money movement was for. Amounts are signed: money in is positive.</summary>
    public enum LedgerCategory
    {
        Unknown = 0,

        // Income
        InsuranceClinic,
        InsuranceHospitalization,
        InsuranceInpatientDays,
        InsuranceTransport,
        Food,
        InsuranceObjectives,
        EventIncome,

        // Expenses (building refunds are booked as positive Building amounts)
        Building,
        HiringAgency,
        LoanInterest,
        Ambulances,
        Penalties,
        EventExpense,
        WagesClinic,
        WagesHospitalization,
        Wages,
        Training,
        Maintenance,
        InpatientCare,
        Overdraft,
        Handover,

        // Financing: moves the balance but is not profit or loss
        LoanTaken,
        LoanRepaid,
    }

    public enum CategoryGroup
    {
        Income,
        Expense,
        Financing,
    }

    /// <summary>Staff shift when the money moved. Unknown for days imported from the game's totals.</summary>
    public enum WorkShift
    {
        Unknown = 0,
        Day,
        Night,
    }

    public enum ActivityKind
    {
        Examination,
        LabExamination,
        Treatment,
        Surgery,
    }

    public static class CategoryInfo
    {
        public static CategoryGroup GetGroup(LedgerCategory category)
        {
            switch (category)
            {
                case LedgerCategory.InsuranceClinic:
                case LedgerCategory.InsuranceHospitalization:
                case LedgerCategory.InsuranceInpatientDays:
                case LedgerCategory.InsuranceTransport:
                case LedgerCategory.Food:
                case LedgerCategory.InsuranceObjectives:
                case LedgerCategory.EventIncome:
                    return CategoryGroup.Income;
                case LedgerCategory.LoanTaken:
                case LedgerCategory.LoanRepaid:
                    return CategoryGroup.Financing;
                default:
                    return CategoryGroup.Expense;
            }
        }
    }
}
