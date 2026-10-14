using Lopital;
using RPH.Core.Accounting;
using RPH.Core.Economy;
using RPH.Mod.Accounting;

namespace RPH.Mod.Economy
{
    /// <summary>
    /// Loans: the daily interest rate follows the hospital's renown, a share of the loan is repaid every night,
    /// and a negative balance costs overdraft charges. Taking and repaying loans by hand stays as in the game.
    /// Rates are tweakables in mod/Database/Economy/Loans.xml.
    /// </summary>
    internal static class Loans
    {
        private const string EnabledId = "RPH_ECONOMY_LOANS_ENABLED";
        private const string RatePrefix = "RPH_ECONOMY_LOAN_RATE";
        private const string RepaymentPercentId = "RPH_ECONOMY_LOAN_REPAYMENT_PERCENT";
        private const string RepaymentMinimumId = "RPH_ECONOMY_LOAN_REPAYMENT_MIN";
        private const string OverdraftPercentId = "RPH_ECONOMY_OVERDRAFT_PERCENT";

        public static bool Enabled
        {
            get { return Tweakables.Int(EnabledId) != 0; }
        }

        /// <summary>Tonight's interest on the current loan (the game charges 1 % a day).</summary>
        public static int DailyInterest()
        {
            return EconomyMath.PercentCharge(Hospital.Instance.m_state.m_budget.m_loan, Renown.Ramp(RatePrefix));
        }

        /// <summary>Midnight, after the game charged the interest: scheduled repayment, then overdraft charges on what is left.</summary>
        public static void OnMidnight()
        {
            if (!Enabled)
            {
                return;
            }
            Budget budget = Hospital.Instance.m_state.m_budget;
            int loan = budget.m_loan;
            int interest = DailyInterest();
            int repayment = EconomyMath.Repayment(budget.m_loan, Tweakables.Float(RepaymentPercentId), Tweakables.Int(RepaymentMinimumId));
            if (repayment > 0)
            {
                budget.m_loan -= repayment;
                budget.m_currentBalance -= repayment;
                RPHLedgerService.Recorder.OnBalanceChanged(-repayment, LedgerCategory.LoanRepaid, RPHLedgerService.CurrentShift);
                // As after the game's own repayment (Budget.DecreaseLoan): some insurer goals need a paid-off loan.
                InsuranceManager.Instance.UpdateInsuranceCompanyRequirementsAndObjectives();
            }
            int overdraft = EconomyMath.PercentCharge(-budget.m_currentBalance, Tweakables.Float(OverdraftPercentId));
            if (overdraft > 0)
            {
                // The game has no expense category for bank charges: its Budget tab shows them with fines.
                RPHLedgerService.Spend(overdraft, ExpenseCategory.PENALTIES, string.Empty, string.Empty, LedgerCategory.Overdraft);
            }
            if (loan > 0 || overdraft > 0)
            {
                RPHLog.Balance(BalanceSection.Loans, "loan=" + loan + " stars=" + RPHLog.Num(Renown.Stars())
                    + " rate=" + RPHLog.Num(Renown.Ramp(RatePrefix)) + " interest=" + interest + " repaid=" + repayment
                    + " loan_after=" + budget.m_loan + " balance=" + budget.m_currentBalance + " overdraft=" + overdraft);
            }
        }
    }
}
