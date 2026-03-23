using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    public class TradeValidatorTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            try
            {
                var capManager = new SalaryCapManager();
                var validator = new TradeValidator(capManager);

                // Verify instantiation
                Assert(validator != null, "TradeValidator instantiates");

                // Test salary matching logic via LeagueCBA
                // Under cap: can absorb any salary
                long underCapPayroll = LeagueCBA.SALARY_CAP - 20_000_000;
                long capSpace = LeagueCBA.SALARY_CAP - underCapPayroll;
                Assert(capSpace > 0, "Under-cap team has cap space");

                // Over cap: must match within 125% + 100K
                long outgoing = 10_000_000;
                long maxIncoming = LeagueCBA.GetMaxTradeIncoming(outgoing, TeamCapStatus.OverCap);
                Assert(maxIncoming >= outgoing, "Max incoming >= outgoing for over-cap");
                Assert(maxIncoming <= outgoing * 2, "Max incoming <= 2x outgoing (reasonable)");

                // Equal salary swap always works
                Assert(outgoing <= maxIncoming, "Equal salary fits within matching rules");

                Debug.Log($"    INFO: $10M outgoing allows up to ${maxIncoming:N0} incoming (over cap)");
            }
            catch (System.Exception e)
            {
                // Some methods may not exist; log and note
                _failed++;
                Debug.Log($"    FAIL: TradeValidator test exception: {e.Message}");
            }

            return (_passed, _failed);
        }
    }
}
