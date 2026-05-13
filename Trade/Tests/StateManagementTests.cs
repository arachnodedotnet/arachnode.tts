using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class StateManagementTests
    {
        [TestMethod][TestCategory("Core")]
        public void IndicatorState_InitializesCorrectly()
        {
            // Arrange & Act
            var state = new IndicatorState();

            // Assert - Default values
            Assert.IsFalse(state.HoldingStock, "Should not be holding stock initially");
            Assert.AreEqual(0, state.OpenStockIndex, "OpenStockIndex should be 0");
            Assert.AreEqual(0.0, state.OpenStockPrice, "OpenStockPrice should be 0");
            Assert.AreEqual(0.0, state.StockPosition, "StockPosition should be 0");
            
            Assert.IsFalse(state.HoldingOption, "Should not be holding option initially");
            Assert.AreEqual(0, state.OpenOptionIndex, "OpenOptionIndex should be 0");
            Assert.AreEqual(0.0, state.OpenOptionPrice, "OpenOptionPrice should be 0");
            Assert.AreEqual(0.0, state.OptionPosition, "OptionPosition should be 0");
            Assert.AreEqual(0.0, state.OptionStrike, "OptionStrike should be 0");
            Assert.IsFalse(state.IsCallOption, "IsCallOption should be false");
            Assert.IsNull(state.PriceRecordForOpen, "PriceRecordForOpen should be null");
            
            Assert.AreEqual(0, state.PrevDir, "PrevDir should be 0");
        }

        [TestMethod][TestCategory("Core")]
        public void IndicatorState_CanBeModified()
        {
            // Arrange
            var state = new IndicatorState();
            var priceRecord = new PriceRecord(DateTime.Now.Date, TimeFrame.D1, 100, 101, 99, 100.5, volume: 1000);

            // Act - Modify stock position
            state.HoldingStock = true;
            state.OpenStockIndex = 5;
            state.OpenStockPrice = 100.5;
            state.StockPosition = 10.0;

            // Act - Modify option position
            state.HoldingOption = true;
            state.OpenOptionIndex = 3;
            state.OpenOptionPrice = 2.5;
            state.OptionPosition = 5.0;
            state.OptionStrike = 105.0;
            state.IsCallOption = true;
            state.PriceRecordForOpen = priceRecord;

            // Act - Modify direction tracking
            state.PrevDir = 1;

            // Assert
            Assert.IsTrue(state.HoldingStock, "HoldingStock should be set");
            Assert.AreEqual(5, state.OpenStockIndex, "OpenStockIndex should be updated");
            Assert.AreEqual(100.5, state.OpenStockPrice, "OpenStockPrice should be updated");
            Assert.AreEqual(10.0, state.StockPosition, "StockPosition should be updated");
            
            Assert.IsTrue(state.HoldingOption, "HoldingOption should be set");
            Assert.AreEqual(3, state.OpenOptionIndex, "OpenOptionIndex should be updated");
            Assert.AreEqual(2.5, state.OpenOptionPrice, "OpenOptionPrice should be updated");
            Assert.AreEqual(5.0, state.OptionPosition, "OptionPosition should be updated");
            Assert.AreEqual(105.0, state.OptionStrike, "OptionStrike should be updated");
            Assert.IsTrue(state.IsCallOption, "IsCallOption should be updated");
            Assert.AreEqual(priceRecord, state.PriceRecordForOpen, "PriceRecordForOpen should be updated");
            
            Assert.AreEqual(1, state.PrevDir, "PrevDir should be updated");
        }

        [TestMethod][TestCategory("Core")]
        public void PortfolioState_InitializesCorrectly()
        {
            // Arrange & Act
            var state = new PortfolioState();

            // Assert - Default values
            Assert.IsFalse(state.HoldingStock, "Should not be holding stock initially");
            Assert.AreEqual(0, state.OpenStockIndex, "OpenStockIndex should be 0");
            Assert.AreEqual(0.0, state.OpenStockPrice, "OpenStockPrice should be 0");
            Assert.AreEqual(0.0, state.StockPosition, "StockPosition should be 0");
            
            Assert.IsFalse(state.HoldingOption, "Should not be holding option initially");
            Assert.AreEqual(0, state.OpenOptionIndex, "OpenOptionIndex should be 0");
            Assert.AreEqual(0.0, state.OpenOptionPrice, "OpenOptionPrice should be 0");
            Assert.AreEqual(0.0, state.OptionPosition, "OptionPosition should be 0");
            Assert.AreEqual(0.0, state.OptionStrike, "OptionStrike should be 0");
            Assert.IsFalse(state.IsCallOption, "IsCallOption should be false");
            Assert.IsNull(state.PriceRecordForOpen, "PriceRecordForOpen should be null");
        }

        [TestMethod][TestCategory("Core")]
        public void PortfolioState_CanTrackSinglePosition()
        {
            // Arrange
            var state = new PortfolioState();
            var priceRecord = new PriceRecord(DateTime.Now.Date, TimeFrame.D1, 100, 101, 99, 100.5, volume: 1000);

            // Act - Portfolio holds one position at a time
            state.HoldingStock = true;
            state.OpenStockIndex = 10;
            state.OpenStockPrice = 100.5;
            state.StockPosition = 50.0; // Larger position for portfolio

            // Assert
            Assert.IsTrue(state.HoldingStock, "Portfolio should be holding stock");
            Assert.AreEqual(10, state.OpenStockIndex, "Portfolio open index should be correct");
            Assert.AreEqual(100.5, state.OpenStockPrice, "Portfolio open price should be correct");
            Assert.AreEqual(50.0, state.StockPosition, "Portfolio position size should be correct");
        }

        [TestMethod][TestCategory("Core")]
        public void StateArrays_CanTrackMultipleIndicators()
        {
            // Arrange
            const int numIndicators = 3;
            var indicatorStates = new IndicatorState[numIndicators];
            
            // Initialize each indicator state
            for (int i = 0; i < numIndicators; i++)
            {
                indicatorStates[i] = new IndicatorState { PrevDir = 0 };
            }

            // Act - Each indicator gets its own state
            indicatorStates[0].HoldingStock = true;
            indicatorStates[0].StockPosition = 10.0;
            indicatorStates[0].PrevDir = 1;

            indicatorStates[1].HoldingStock = true;
            indicatorStates[1].StockPosition = -5.0; // Short position
            indicatorStates[1].PrevDir = -1;

            indicatorStates[2].HoldingOption = true;
            indicatorStates[2].OptionPosition = 2.0;
            indicatorStates[2].PrevDir = 1;

            // Assert - Each indicator maintains independent state
            Assert.IsTrue(indicatorStates[0].HoldingStock, "Indicator 0 should hold stock");
            Assert.AreEqual(10.0, indicatorStates[0].StockPosition, "Indicator 0 position should be long");
            Assert.AreEqual(1, indicatorStates[0].PrevDir, "Indicator 0 direction should be bullish");

            Assert.IsTrue(indicatorStates[1].HoldingStock, "Indicator 1 should hold stock");
            Assert.AreEqual(-5.0, indicatorStates[1].StockPosition, "Indicator 1 position should be short");
            Assert.AreEqual(-1, indicatorStates[1].PrevDir, "Indicator 1 direction should be bearish");

            Assert.IsTrue(indicatorStates[2].HoldingOption, "Indicator 2 should hold option");
            Assert.AreEqual(2.0, indicatorStates[2].OptionPosition, "Indicator 2 option position should be set");
            Assert.AreEqual(1, indicatorStates[2].PrevDir, "Indicator 2 direction should be bullish");

            // Assert - Independence: changing one doesn't affect others
            indicatorStates[0].PrevDir = 0;
            Assert.AreEqual(-1, indicatorStates[1].PrevDir, "Other indicators should be unaffected");
            Assert.AreEqual(1, indicatorStates[2].PrevDir, "Other indicators should be unaffected");
        }

        [TestMethod][TestCategory("Core")]
        public void StateStructures_AreValueTypes()
        {
            // Arrange
            var state1 = new IndicatorState { PrevDir = 1, StockPosition = 10.0 };
            
            // Act - Assignment creates a copy (value type behavior)
            var state2 = state1;
            state2.PrevDir = -1;
            state2.StockPosition = 20.0;

            // Assert - Original should be unchanged
            Assert.AreEqual(1, state1.PrevDir, "Original state PrevDir should be unchanged");
            Assert.AreEqual(10.0, state1.StockPosition, "Original state StockPosition should be unchanged");
            
            Assert.AreEqual(-1, state2.PrevDir, "Copy should have new PrevDir");
            Assert.AreEqual(20.0, state2.StockPosition, "Copy should have new StockPosition");
        }

        [TestMethod][TestCategory("Core")]
        public void IndicatorState_CanTrackDirectionChanges()
        {
            // Test the PrevDir tracking which is critical for delta calculation
            
            // Arrange
            var state = new IndicatorState { PrevDir = 0 };

            // Act & Assert - Simulate direction changes over time
            
            // Initial state
            Assert.AreEqual(0, state.PrevDir, "Should start with neutral direction");
            
            // First signal: bullish
            state.PrevDir = 1;
            Assert.AreEqual(1, state.PrevDir, "Should track bullish direction");
            
            // Continue bullish (no switch)
            // PrevDir stays 1
            Assert.AreEqual(1, state.PrevDir, "Should maintain bullish direction");
            
            // Switch to bearish
            state.PrevDir = -1;
            Assert.AreEqual(-1, state.PrevDir, "Should track bearish direction");
            
            // Back to bullish (switch again)
            state.PrevDir = 1;
            Assert.AreEqual(1, state.PrevDir, "Should track direction switches correctly");
        }

        [TestMethod][TestCategory("Core")]
        public void StateManagement_SupportsCompleteTradeLifecycle()
        {
            // Test a complete trade lifecycle using state management
            
            // Arrange
            var state = new IndicatorState();
            var priceRecord = new PriceRecord(DateTime.Now.Date, TimeFrame.D1, 100, 101, 99, 100.5, volume: 1000);

            // Act & Assert - Complete trade lifecycle

            // 1. Initial state
            Assert.IsFalse(state.HoldingStock, "Should start with no position");
            Assert.AreEqual(0.0, state.StockPosition, "Should start with zero position");

            // 2. Open position
            state.HoldingStock = true;
            state.OpenStockIndex = 5;
            state.OpenStockPrice = 100.5;
            state.StockPosition = 10.0;
            state.PrevDir = 1; // Bullish signal

            Assert.IsTrue(state.HoldingStock, "Should be holding position after open");
            Assert.AreEqual(10.0, state.StockPosition, "Should have correct position size");
            Assert.AreEqual(100.5, state.OpenStockPrice, "Should track open price");

            // 3. Hold position (direction unchanged)
            // PrevDir remains 1, position unchanged
            Assert.AreEqual(1, state.PrevDir, "Should maintain direction");
            Assert.AreEqual(10.0, state.StockPosition, "Position should be unchanged");

            // 4. Close position
            state.HoldingStock = false;
            state.StockPosition = 0.0;
            state.PrevDir = -1; // Signal changed

            Assert.IsFalse(state.HoldingStock, "Should not be holding after close");
            Assert.AreEqual(0.0, state.StockPosition, "Position should be zero after close");
            Assert.AreEqual(-1, state.PrevDir, "Direction should reflect signal change");

            // 5. Ready for next trade
            Assert.AreEqual(100.5, state.OpenStockPrice, "Open price can remain for record keeping");
            Assert.AreEqual(5, state.OpenStockIndex, "Open index can remain for record keeping");
        }

        [TestMethod][TestCategory("Core")]
        public void MultipleIndicatorStates_CanOperateIndependently()
        {
            // Simulate real concurrent processing scenario
            
            // Arrange
            var numIndicators = 2;
            var states = new IndicatorState[numIndicators];
            
            for (int i = 0; i < numIndicators; i++)
            {
                states[i] = new IndicatorState { PrevDir = 0 };
            }

            // Act - Simulate concurrent bar processing

            // Bar 1: Both indicators get bullish signals
            states[0].PrevDir = 1; // Indicator 0: neutral -> bullish (switch)
            states[1].PrevDir = 1; // Indicator 1: neutral -> bullish (switch)
            
            // Both should enter positions
            states[0].HoldingStock = true;
            states[0].StockPosition = 10.0;
            
            states[1].HoldingStock = true;
            states[1].StockPosition = 8.0; // Different position size

            // Bar 2: Indicator 0 continues bullish, Indicator 1 turns bearish
            // states[0].PrevDir stays 1 (no switch)
            states[1].PrevDir = -1; // Switch from +1 to -1

            // Indicator 1 should exit, Indicator 0 should hold
            states[1].HoldingStock = false;
            states[1].StockPosition = 0.0;

            // Assert final states
            Assert.IsTrue(states[0].HoldingStock, "Indicator 0 should still hold position");
            Assert.AreEqual(10.0, states[0].StockPosition, "Indicator 0 position unchanged");
            Assert.AreEqual(1, states[0].PrevDir, "Indicator 0 still bullish");

            Assert.IsFalse(states[1].HoldingStock, "Indicator 1 should have exited");
            Assert.AreEqual(0.0, states[1].StockPosition, "Indicator 1 position should be zero");
            Assert.AreEqual(-1, states[1].PrevDir, "Indicator 1 now bearish");
        }
    }
}