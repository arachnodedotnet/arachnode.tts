using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class PositionSizingResultTests
    {
        [TestMethod][TestCategory("Core")]
        public void DefaultConstructor_InitializesDefaults()
        {
            var result = new PositionSizingResult();

            Assert.AreEqual(0.0, result.PositionSize);
            Assert.AreEqual(0.0, result.PositionAmount);
            Assert.AreEqual(0.0, result.ShareQuantity);
            Assert.AreEqual(0.0, result.StopLoss);
            Assert.AreEqual(0.0, result.RiskAmount);
            Assert.AreEqual(0.0, result.ExpectedReturn);
            Assert.AreEqual(0.0, result.RiskRewardRatio);
            Assert.IsNull(result.PrimarySizingFactor);
            Assert.IsNotNull(result.AdjustmentFactors);
            Assert.AreEqual(0, result.AdjustmentFactors.Count);
            Assert.AreEqual(0.0, result.ConfidenceLevel);
            Assert.IsNull(result.RiskAssessment);
            Assert.IsFalse(result.HitMaxPositionLimit);
            Assert.IsFalse(result.HitMinPositionLimit);
            Assert.IsFalse(result.BlockedByCorrelation);
            Assert.IsFalse(result.ReducedByDrawdown);
        }

        [TestMethod][TestCategory("Core")]
        public void PropertyAssignment_WorksCorrectly()
        {
            var result = new PositionSizingResult
            {
                PositionSize = 0.15,
                PositionAmount = 15000,
                ShareQuantity = 100,
                StopLoss = 95.5,
                RiskAmount = 500,
                ExpectedReturn = 1000,
                RiskRewardRatio = 2.0,
                PrimarySizingFactor = "KellyOptimal",
                ConfidenceLevel = 0.8,
                RiskAssessment = "HIGH RISK",
                HitMaxPositionLimit = true,
                HitMinPositionLimit = false,
                BlockedByCorrelation = true,
                ReducedByDrawdown = true
            };
            result.AdjustmentFactors.Add("Kelly");
            result.AdjustmentFactors.Add("Volatility");

            Assert.AreEqual(0.15, result.PositionSize);
            Assert.AreEqual(15000, result.PositionAmount);
            Assert.AreEqual(100, result.ShareQuantity);
            Assert.AreEqual(95.5, result.StopLoss);
            Assert.AreEqual(500, result.RiskAmount);
            Assert.AreEqual(1000, result.ExpectedReturn);
            Assert.AreEqual(2.0, result.RiskRewardRatio);
            Assert.AreEqual("KellyOptimal", result.PrimarySizingFactor);
            Assert.AreEqual(0.8, result.ConfidenceLevel);
            Assert.AreEqual("HIGH RISK", result.RiskAssessment);
            Assert.IsTrue(result.HitMaxPositionLimit);
            Assert.IsFalse(result.HitMinPositionLimit);
            Assert.IsTrue(result.BlockedByCorrelation);
            Assert.IsTrue(result.ReducedByDrawdown);
            CollectionAssert.AreEqual(new List<string> { "Kelly", "Volatility" }, result.AdjustmentFactors);
        }

        [TestMethod][TestCategory("Core")]
        public void ToString_FormatsOutput()
        {
            var result = new PositionSizingResult
            {
                PositionSize = 0.2,
                PositionAmount = 20000,
                RiskAmount = 1000,
                RiskAssessment = "NORMAL RISK"
            };

            var str = result.ToString();
            Assert.IsTrue(str.Contains("20.00%"));
            Assert.IsTrue(str.Contains("$20000"));
            Assert.IsTrue(str.Contains("$1000"));
            Assert.IsTrue(str.Contains("NORMAL RISK"));
        }

        [TestMethod][TestCategory("Core")]
        public void Flags_AreSetAndReportedCorrectly()
        {
            var result = new PositionSizingResult();

            Assert.IsFalse(result.HitMaxPositionLimit);
            Assert.IsFalse(result.HitMinPositionLimit);
            Assert.IsFalse(result.BlockedByCorrelation);
            Assert.IsFalse(result.ReducedByDrawdown);

            result.HitMaxPositionLimit = true;
            result.HitMinPositionLimit = true;
            result.BlockedByCorrelation = true;
            result.ReducedByDrawdown = true;

            Assert.IsTrue(result.HitMaxPositionLimit);
            Assert.IsTrue(result.HitMinPositionLimit);
            Assert.IsTrue(result.BlockedByCorrelation);
            Assert.IsTrue(result.ReducedByDrawdown);
        }
    }
}