using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    public class LeagueCBATest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            // Constants
            AssertEqual(140_588_000L, LeagueCBA.SALARY_CAP, "Salary cap = $140.588M");
            AssertEqual(170_814_000L, LeagueCBA.LUXURY_TAX_LINE, "Luxury tax = $170.814M");
            Assert(LeagueCBA.FIRST_APRON > LeagueCBA.LUXURY_TAX_LINE, "First apron > tax line");
            Assert(LeagueCBA.SECOND_APRON > LeagueCBA.FIRST_APRON, "Second apron > first apron");
            Assert(LeagueCBA.SALARY_CAP > LeagueCBA.SALARY_FLOOR, "Cap > floor");
            Assert(LeagueCBA.SALARY_FLOOR > 0, "Floor > 0");

            // Max salary by experience
            long max0 = LeagueCBA.GetMaxSalary(0);
            long max7 = LeagueCBA.GetMaxSalary(7);
            long max10 = LeagueCBA.GetMaxSalary(10);
            Assert(System.Math.Abs(max0 - (long)(LeagueCBA.SALARY_CAP * 0.25f)) <= 1, $"0-6 years = 25% of cap ({max0})");
            Assert(System.Math.Abs(max7 - (long)(LeagueCBA.SALARY_CAP * 0.30f)) <= 1, $"7-9 years = 30% of cap ({max7})");
            Assert(System.Math.Abs(max10 - (long)(LeagueCBA.SALARY_CAP * 0.35f)) <= 1, $"10+ years = 35% of cap ({max10})");
            Assert(max10 > max7 && max7 > max0, "Max salary increases with experience");

            // Min salary
            long min0 = LeagueCBA.GetMinimumSalary(0);
            Assert(min0 > 0, "Minimum salary > 0");
            Assert(min0 < max0, "Min salary < max salary");

            // Contract rules
            AssertEqual(5, LeagueCBA.MAX_CONTRACT_YEARS_BIRD, "Bird max years = 5");
            AssertEqual(4, LeagueCBA.MAX_CONTRACT_YEARS_OTHER, "Other team max years = 4");
            Assert(LeagueCBA.ANNUAL_RAISE_BIRD > LeagueCBA.ANNUAL_RAISE_OTHER, "Bird raise > other raise");

            // Exceptions
            Assert(LeagueCBA.NON_TAXPAYER_MLE > LeagueCBA.TAXPAYER_MLE, "Non-taxpayer MLE > taxpayer MLE");
            Assert(LeagueCBA.NON_TAXPAYER_MLE_MAX_YEARS > LeagueCBA.TAXPAYER_MLE_MAX_YEARS, "Non-tax MLE longer");

            return (_passed, _failed);
        }
    }
}
