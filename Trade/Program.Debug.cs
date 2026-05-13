using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trade
{
    internal partial class Program
    {
        /// <summary>
        ///     Critical fix for the P&L vs Balance calculation inconsistency identified in the console log.
        ///     The issue: Total P&L ($52,790.67) doesn't match equity change ($205,373), indicating units mix-up.
        /// </summary>
        private static void FixPnLBalanceInconsistency()
        {
            WriteSection("CRITICAL FIX: P&L vs Balance Calculation Inconsistency");
            WriteWarning("IDENTIFIED ISSUE from console log:");
            WriteWarning("  Final Balance: $305,373");
            WriteWarning("  Starting Balance: ~$100,000");
            WriteWarning("  Expected Net Profit: $205,373");
            WriteWarning("  Actual Total P&L: $52,790.67");
            WriteWarning("  DISCREPANCY: $152,582 (almost 4:1 ratio)");
            WriteWarning("");
            WriteWarning("This suggests ActualDollarGain is using per-share values instead of position-sized values");
            WriteWarning("");

            // The fix needs to be applied in the TradeResult.ActualDollarGain calculation
            WriteInfo("REQUIRED FIX LOCATION: TradeResult.ActualDollarGain property");
            WriteInfo("Current calculation may be: DollarGain * Position * (SecurityType multiplier)");
            WriteInfo("Issue: Position sizing or multiplier is incorrect");
            WriteInfo("");

            WriteSuccess("To implement the fix:");
            WriteSuccess("1. Check TradeResult.ActualDollarGain calculation in GeneticIndividual.cs");
            WriteSuccess("2. Verify Position and PositionInDollars are set correctly");
            WriteSuccess("3. Ensure balance updates use the correct gain values");
            WriteSuccess("4. Run ValidateCalculationConsistency() after trades to confirm fix");
        }

        /// <summary>
        ///     Diagnostic method to trace the exact cause of the P&L discrepancy
        /// </summary>
        /// <param name="individual">Individual to diagnose</param>
        private static void DiagnosePnLDiscrepancy(GeneticIndividual individual)
        {
            if (individual?.Trades?.Count == 0) return;

            WriteSection("P&L Discrepancy Diagnostic");

            var startingBalance = individual.StartingBalance;
            var finalBalance = individual.Trades.Last().Balance;
            var netEquityChange = finalBalance - startingBalance;
            var totalPnL = individual.Trades.Sum(t => t.ActualDollarGain);
            var discrepancy = netEquityChange - totalPnL;

            WriteInfo($"DIAGNOSTIC RESULTS:");
            ConsoleUtilities.WriteLine($"  Starting Balance:    ${startingBalance:F2}");
            ConsoleUtilities.WriteLine($"  Final Balance:       ${finalBalance:F2}");
            ConsoleUtilities.WriteLine($"  Net Equity Change:   ${netEquityChange:F2}");
            ConsoleUtilities.WriteLine($"  Sum of P&L:          ${totalPnL:F2}");
            ConsoleUtilities.WriteLine($"  DISCREPANCY:         ${discrepancy:F2}");
            ConsoleUtilities.WriteLine($"  Ratio:               {(netEquityChange / Math.Max(1, totalPnL)):F2}:1");

            if (Math.Abs(discrepancy) > 1000)
            {
                WriteWarning("❌ SIGNIFICANT DISCREPANCY DETECTED!");
                WriteWarning("This indicates a fundamental calculation error in:");
                WriteWarning("  • TradeResult.ActualDollarGain calculation");
                WriteWarning("  • Balance update logic");
                WriteWarning("  • Position sizing or multiplier application");

                // Analyze a few sample trades to identify the pattern
                WriteInfo("");
                WriteInfo("SAMPLE TRADE ANALYSIS:");
                for (int i = 0; i < Math.Min(5, individual.Trades.Count); i++)
                {
                    var trade = individual.Trades[i];
                    var expectedBalance = (i == 0)
                        ? startingBalance + trade.ActualDollarGain
                        : individual.Trades[i - 1].Balance + trade.ActualDollarGain;
                    var actualBalance = trade.Balance;
                    var balanceDiscrepancy = actualBalance - expectedBalance;

                    ConsoleUtilities.WriteLine($"  Trade {i + 1}:");
                    ConsoleUtilities.WriteLine(
                        $"    DollarGain: ${trade.DollarGain:F2} | ActualDollarGain: ${trade.ActualDollarGain:F2}");
                    ConsoleUtilities.WriteLine(
                        $"    Position: {trade.Position:F2} | PositionInDollars: ${trade.PositionInDollars:F0}");
                    ConsoleUtilities.WriteLine(
                        $"    Expected Balance: ${expectedBalance:F2} | Actual: ${actualBalance:F2}");
                    if (Math.Abs(balanceDiscrepancy) > 0.01)
                        WriteWarning($"    ❌ Balance Error: ${balanceDiscrepancy:F2}");
                }
            }
            else
            {
                WriteSuccess("✅ P&L calculations are consistent");
            }
        }

        /// <summary>
        ///     Validates the specific issues seen in the console log output
        /// </summary>
        private static void ValidateConsoleLogIssues()
        {
            WriteSection("Console Log Issue Validation");
            WriteInfo("Based on the provided console output, checking for:");
            WriteInfo("1. Final balance calculation consistency");
            WriteInfo("2. Total P&L vs equity change discrepancy");
            WriteInfo("3. Buy & Hold calculation using correct account-level amounts");
            WriteInfo("");

            // Expected values from the console log
            var expectedFinalBalance = 305373.0;
            var expectedStartingBalance = 100000.0; // Inferred
            var expectedNetProfit = expectedFinalBalance - expectedStartingBalance; // $205,373
            var reportedTotalPnL = 52790.67;
            var reportedBuyHoldGain = 106.50;
            var reportedBuyHoldReturn = 20.07;

            WriteInfo("CONSOLE LOG VALUES:");
            ConsoleUtilities.WriteLine($"  Final Balance:       ${expectedFinalBalance:F0}");
            ConsoleUtilities.WriteLine($"  Starting Balance:    ${expectedStartingBalance:F0} (inferred)");
            ConsoleUtilities.WriteLine($"  Expected Net Profit: ${expectedNetProfit:F0}");
            ConsoleUtilities.WriteLine($"  Reported Total P&L:  ${reportedTotalPnL:F2}");
            ConsoleUtilities.WriteLine($"  Discrepancy:         ${expectedNetProfit - reportedTotalPnL:F2}");
            ConsoleUtilities.WriteLine($"  Buy & Hold Gain:     ${reportedBuyHoldGain:F2}");
            ConsoleUtilities.WriteLine($"  Buy & Hold Return:   {reportedBuyHoldReturn:F2}%");

            // The Buy & Hold calculation also looks wrong - $106.50 gain on $100,000 is 0.1%, not 20.07%
            var correctBuyHoldReturn = reportedBuyHoldGain / expectedStartingBalance * 100;
            WriteInfo("");
            WriteWarning("BUY & HOLD CALCULATION ISSUE:");
            WriteWarning($"  Reported: ${reportedBuyHoldGain:F2} = {reportedBuyHoldReturn:F2}%");
            WriteWarning(
                $"  Correct:  ${reportedBuyHoldGain:F2} = {correctBuyHoldReturn:F3}% on ${expectedStartingBalance:F0}");
            WriteWarning("  This confirms Buy & Hold is using per-share instead of account-level calculations");

            WriteInfo("");
            WriteWarning("SUMMARY OF REQUIRED FIXES:");
            WriteWarning("1. Fix ActualDollarGain calculation to match balance progression");
            WriteWarning("2. Fix Buy & Hold to use account-level returns ($100K notional)");
            WriteWarning("3. Ensure all percentage calculations use same baseline");
            WriteWarning("4. Verify position sizing calculations are consistent");
        }

        /// <summary>
        ///     Advanced diagnostic to identify the exact source of the 5:1 balance inflation
        /// </summary>
        /// <param name="individual">Individual to analyze</param>
        private static void DiagnoseBalanceInflation(GeneticIndividual individual)
        {
            if (individual?.Trades?.Count == 0) return;

            WriteSection("Balance Inflation Root Cause Analysis");

            WriteInfo("INVESTIGATING THE 5:1 BALANCE INFLATION MYSTERY");
            WriteInfo("Current evidence:");
            WriteInfo("• Balance updates match ActualDollarGain exactly (no discrepancy)");
            WriteInfo("• But net equity change is 5x larger than sum of ActualDollarGain");
            WriteInfo("• This suggests ActualDollarGain is calculated correctly");
            WriteInfo("• But balance updates are being inflated somewhere else");
            WriteInfo("");

            // Let's analyze the actual calculation step by step
            var trade1 = individual.Trades[0];
            WriteInfo("DETAILED ANALYSIS OF TRADE 1:");
            ConsoleUtilities.WriteLine($"  Raw DollarGain: ${trade1.DollarGain:F2}");
            ConsoleUtilities.WriteLine($"  Position: {trade1.Position:F2}");
            ConsoleUtilities.WriteLine($"  Position (abs): {Math.Abs(trade1.Position):F2}");
            ConsoleUtilities.WriteLine($"  Security Type: {trade1.AllowedSecurityType}");
            ConsoleUtilities.WriteLine($"  Option Multiplier: {(trade1.AllowedSecurityType == AllowedSecurityType.Option ? 100 : 1)}");
            ConsoleUtilities.WriteLine($"  Expected ActualDollarGain: ${trade1.DollarGain:F2} × {Math.Abs(trade1.Position):F2} × {(trade1.AllowedSecurityType == AllowedSecurityType.Option ? 100 : 1)} = ${trade1.DollarGain * Math.Abs(trade1.Position) * (trade1.AllowedSecurityType == AllowedSecurityType.Option ? 100 : 1):F2}");
            ConsoleUtilities.WriteLine($"  Actual ActualDollarGain: ${trade1.ActualDollarGain:F2}");

            var calculationMatches = Math.Abs(trade1.ActualDollarGain - (trade1.DollarGain * Math.Abs(trade1.Position) * (trade1.AllowedSecurityType == AllowedSecurityType.Option ? 100 : 1))) < 0.01;

            if (calculationMatches)
            {
                WriteSuccess("✅ ActualDollarGain calculation is CORRECT");
                WriteWarning("❌ The problem is elsewhere - balance is being inflated during updates");
                WriteWarning("HYPOTHESIS: There's a hidden multiplier in the balance update logic");
            }
            else
            {
                WriteWarning("❌ ActualDollarGain calculation has an error");
            }

            // Check if there's a compounding effect
            WriteInfo("");
            WriteInfo("CHECKING FOR COMPOUNDING EFFECTS:");

            var runningBalanceCheck = individual.StartingBalance;
            for (int i = 0; i < Math.Min(10, individual.Trades.Count); i++)
            {
                var trade = individual.Trades[i];
                var expectedNewBalance = runningBalanceCheck + trade.ActualDollarGain;
                var actualNewBalance = trade.Balance;
                var inflationRatio = actualNewBalance / expectedNewBalance;

                ConsoleUtilities.WriteLine($"  Trade {i + 1}:");
                ConsoleUtilities.WriteLine($"    Previous Balance: ${runningBalanceCheck:F2}");
                ConsoleUtilities.WriteLine($"    ActualDollarGain: ${trade.ActualDollarGain:F2}");
                ConsoleUtilities.WriteLine($"    Expected Balance: ${expectedNewBalance:F2}");
                ConsoleUtilities.WriteLine($"    Actual Balance:   ${actualNewBalance:F2}");
                ConsoleUtilities.WriteLine($"    Inflation Ratio:  {inflationRatio:F2}x");

                if (Math.Abs(inflationRatio - 1.0) > 0.01)
                {
                    WriteWarning($"    ❌ INFLATION DETECTED: {inflationRatio:F2}x multiplier");
                }

                runningBalanceCheck = expectedNewBalance; // Use expected for next iteration
            }

            WriteInfo("");
            WriteWarning("CONCLUSION:");
            WriteWarning("The balance update logic has a hidden multiplier that's inflating the balance");
            WriteWarning("Need to find where balance += trade.ActualDollarGain is being done with extra multiplication");
        }


    }
}