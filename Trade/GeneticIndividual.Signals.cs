using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Trade.Caching;
using Trade.Prices2;

namespace Trade
{
    public partial class GeneticIndividual
    {
        private PriceRecord[] GetSafeTimeFrameData(PriceRecord[] priceRecords, TimeFrame targetTimeFrame)
        {
            if (Prices == null || !priceRecords.Any())
                return priceRecords;

            try
            {
                var startDate = priceRecords.First().DateTime;
                var lastInputRecord = priceRecords.Last();

                // For daily input records, we want ALL intraday data for those complete days
                var endBoundary = targetTimeFrame == TimeFrame.D1
                    ? lastInputRecord.DateTime
                    : lastInputRecord.DateTime.Date.AddDays(1).AddTicks(-1); // End of last day

                // Get expanded timeframe data
                var expandedData = Prices.GetRange(startDate, lastInputRecord.DateTime.AddDays(1), targetTimeFrame, 0, false, true).ToArray();

                // CRITICAL: Filter to prevent any future data beyond the input scope
                var filteredData = expandedData
                    .Where(r => r.DateTime >= startDate && r.DateTime <= endBoundary)
                    .ToArray();

                return filteredData.Any() ? filteredData : priceRecords;
            }
            catch (Exception)
            {
                return priceRecords; // Safe fallback
            }
        }

        /// <summary>
        /// Calculate signals and indicator values for each time step
        /// </summary>
        public void CalculateSignals(PriceRecord[] priceRecords, List<List<double>> signals,
            List<List<double>> indicatorValues,
            Func<DateTime, DateTime, DateTime, IndicatorParams, PriceRecord[], int, PriceRecord[]>
                preprocessHistoricalData = null)
        {
            if (priceRecords == null || signals == null || indicatorValues == null)
                throw new ArgumentNullException(nameof(priceRecords));

            //TODO: We should check that all PriceRecord instances are TimeFrame.D1...

            for (var k = 0; k < Indicators.Count; k++)
            {
                var ind = Indicators[k];

                PriceRecord[] historicalRecordsAllStart = null;

                //this is a special case... Prices will ALWAYS return a range with a maximum less than the Last().DateTime;...
                double signal = 0;
                //var priceRecordsFromTimeFrame = Prices != null && priceRecords.Any() ? Prices.GetRange(priceRecords.First().DateTime, priceRecords.Last().DateTime.AddDays(1), ind.TimeFrame, 0, false, true).ToArray() : priceRecords;

                //priceRecordsFromTimeFrame = priceRecordsFromTimeFrame.Where(r => r.DateTime <= priceRecords.Last().DateTime.Date.AddDays(1)).ToArray();

                var priceRecordsFromTimeFrame = GetSafeTimeFrameData(priceRecords, ind.TimeFrame);

                for (var i = 0; i < priceRecordsFromTimeFrame.Length; i++)
                {
                    // Use real historical data from Prices when available
                    double[] openPrices;
                    double[] highPrices;
                    double[] lowPrices;
                    double[] closePrices;
                    double[] volumes;
                    double[] priceBuffer;

                    if (Prices != null)
                    {
                        // Get the current price record's DateTime to use as reference
                        var currentDate = priceRecordsFromTimeFrame[i].DateTime;

                        // Get historical data going back the required number of periods
                        var startDate = currentDate.AddDays(-ind.Period);
                        var endDate = currentDate.AddDays(0); // Make end exclusive, do NOT include current date

                        PriceRecord[] historicalRecords = null;

                        if (ind.Period == 0) //HACK: MIKE!
                        {
                            historicalRecords = priceRecords;
                        }
                        else
                            historicalRecords =
                                Prices.GetRange(startDate, endDate, ind.TimeFrame, ind.Period).ToArray();

                        // Apply preprocessing if provided
                        if (preprocessHistoricalData != null)
                            historicalRecords = preprocessHistoricalData(startDate, endDate, currentDate, ind,
                                historicalRecords, k);

                        if (historicalRecords != null && historicalRecords.Length == ind.Period)
                        {
                            // We have enough historical data - use it for more accurate indicator calculation
                            openPrices = historicalRecords.Select(r => r.Open).ToArray();
                            highPrices = historicalRecords.Select(r => r.High).ToArray();
                            lowPrices = historicalRecords.Select(r => r.Low).ToArray();
                            closePrices = historicalRecords.Select(r => r.Close).ToArray();
                            volumes = historicalRecords.Select(r => r.Volume).ToArray();
                            //HACK:...
                            priceBuffer = historicalRecords.Select(r => r.Close).ToArray();

                            //HACK: this is for testing...
                            if (ind.Type == 0) priceBuffer = new double[i + 1];
                        }
                        else
                        {
                            // CRITICAL: If we have a Prices system but insufficient historical data, this is a configuration error
                            throw new InvalidOperationException(
                                "Insufficient historical data for indicator calculation. " +
                                $"Required: {ind.Period} periods, Available: {historicalRecords.Length} periods. " +
                                $"Date: {currentDate:yyyy-MM-dd}, TimeFrame: {ind.TimeFrame}, " +
                                $"Indicator Type: {ind.Type}. " +
                                "Ensure historical data is properly populated before running genetic algorithm.");
                        }
                    }
                    else
                    {
                        // Fallback when no Prices system is available (legacy mode)
                        openPrices = new double[i + 1];
                        highPrices = new double[i + 1];
                        lowPrices = new double[i + 1];
                        closePrices = new double[i + 1];
                        volumes = new double[i + 1];
                        priceBuffer = new double[i + 1];
                        for (var j = 0; j <= i; j++) closePrices[j] = priceRecords[j].Close;

                        openPrices = highPrices = lowPrices = closePrices = priceBuffer;
                    }

                    var value = CalculateIndicatorValue(ind, openPrices, highPrices, lowPrices, closePrices, volumes,
                        priceBuffer, priceRecords.Length, null);
                    
                    indicatorValues[k].Add(value);
                    signals[k].Add(signal);
                }
            }
        }

        /// <summary>
        /// Reset and initialize state for a new process run (multi-timeframe support)
        /// Updated to handle multiple indicators with different timeframes
        /// </summary>
        private void ResetState(PriceRecord[] priceRecords, out List<List<double>> signals, out List<List<double>> indicatorValues)
        {
            Trades.Clear();
            TradeActions.Clear();
            SignalValues.Clear();
            IndicatorValues.Clear();
            FinalBalance = 0;

            // Pre-size trade actions based on the original daily price records
            TradeActions.Clear();
            for (var i = 0; i < priceRecords.Length; i++) TradeActions.Add(string.Empty);

            // Allocate collections for each indicator
            signals = new List<List<double>>(Indicators.Count);
            indicatorValues = new List<List<double>>(Indicators.Count);

            for (var k = 0; k < Indicators.Count; k++)
            {
                var ind = Indicators[k];

                // Calculate the timeframe-specific buffer length for this indicator
                int indicatorBufferLength;

                if (Prices != null)
                {
                    try
                    {
                        // Get the timeframe-specific data length for this indicator
                        var priceRecordsFromTimeFrame = Prices.GetRange(
                            priceRecords.First().DateTime,
                            priceRecords.Last().DateTime.AddDays(1), //this is a special case... Prices will ALWAYS return a range with a maximum less than the Last().DateTime;...
                            ind.TimeFrame).ToArray();
                        indicatorBufferLength = priceRecordsFromTimeFrame.Length;
                    }
                    catch (Exception)
                    {
                        // Fallback to daily length if timeframe data unavailable
                        indicatorBufferLength = priceRecords.Length;
                    }
                }
                else
                {
                    // Fallback to daily length when no Prices system available
                    indicatorBufferLength = priceRecords.Length;
                }
                
                // Create lists with the appropriate capacity for this indicator's timeframe
                signals.Add(new List<double>(indicatorBufferLength));
                var list = new List<double>(indicatorBufferLength);
                indicatorValues.Add(list);
                IndicatorValues.Add(list); // maintain instance reference for external access
            }
        }

        /// <summary>
        /// Main processing method for PriceRecord array (delta mode only)
        /// </summary>
        //[MemoizeAttribute]
        public Fitness Process(PriceRecord[] priceRecords, GeneticIndividual individual = null)
        {
            if (priceRecords == null) throw new ArgumentNullException(nameof(priceRecords));
            if (priceRecords.Length == 0) return new Fitness();

            Fitness Execute()
            {
                ResetState(priceRecords, out var signals, out var indicatorValues);
                CalculateSignals(priceRecords, signals, indicatorValues);
                // IndicatorValues already populated inside ResetState
                ExecuteTradesDeltaMode(priceRecords, indicatorValues);
                return CalculateFitness();
            }

            if (individual == null)
            {
                return Execute();
            }

            return (Fitness)MemoizationDispatcher.Invoke(this, MethodBase.GetCurrentMethod(),
                new object[] { priceRecords, individual, Indicators.Count, priceRecords.Length },
                args => { return Execute(); });
        }

        /// <summary>
        /// Legacy method for backward compatibility - converts double[] to PriceRecord[]
        /// </summary>
        public Fitness Process(double[] priceBuffer)
        {
            // Convert double[] to PriceRecord[] for backward compatibility
            var priceRecords = new PriceRecord[priceBuffer.Length];
            var baseDate = new DateTime(1900, 1, 1);

            for (var i = 0; i < priceBuffer.Length; i++)
            {
                var date = baseDate.AddDays(i);
                var price = priceBuffer[i];
                priceRecords[i] = new PriceRecord(date, TimeFrame.D1, price, price, price, price, volume: 1000, wap: price, count: 1);
            }

            return Process(priceRecords);
        }
    }
}