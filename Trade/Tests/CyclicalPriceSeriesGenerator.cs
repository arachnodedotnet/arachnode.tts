using System;
using System.Collections.Generic;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Configurable cyclical price series generator for deterministic testing of
    /// delta-based trade event counts. Produces a repeating Up leg followed by a Down leg.
    /// </summary>
    public class CyclicalPriceSeriesConfig
    {
        public int Cycles { get; set; } = 10;              // Number of full (up+down) cycles
        public int HalfCycleDays { get; set; } = 5;         // Days per rising (or falling) half-cycle
        public double StartPrice { get; set; } = 100.0;     // Price at start of each cycle
        public double MaxPrice { get; set; } = 110.0;       // Peak price mid-cycle
        public DateTime StartDate { get; set; } = new DateTime(2000, 1, 1);
        public int Seed { get; set; } = 123;                // RNG seed (only used if NoisePct > 0)
        public double NoisePct { get; set; } = 0.0;         // Optional random noise fraction (e.g. 0.001 = 0.1%)
        public bool IncludeDownLeg { get; set; } = true;    // If false, only rising legs are produced
        public long BaseVolume { get; set; } = 1_000_000;   // Base volume per bar
        public bool SymmetricDownLeg { get; set; } = true;  // Use symmetric linear descent

        /// <summary>
        /// Total days generated = Cycles * (HalfCycleDays * (IncludeDownLeg ? 2 : 1)).
        /// </summary>
        public int TotalDays
        {
            get { return Cycles * HalfCycleDays * (IncludeDownLeg ? 2 : 1); }
        }
    }

    public static class CyclicalPriceSeriesGenerator
    {
        /// <summary>
        /// Generate deterministic daily PriceRecords following a linear up then linear down pattern per cycle.
        /// Close price is the canonical series. Open/high/low are derived with small offsets; volume constant-ish.
        /// </summary>
        public static PriceRecord[] GenerateDaily(CyclicalPriceSeriesConfig cfg)
        {
            if (cfg.HalfCycleDays < 2)
                throw new ArgumentException("HalfCycleDays must be >= 2 for slope changes.");
            if (cfg.MaxPrice <= cfg.StartPrice)
                throw new ArgumentException("MaxPrice must be greater than StartPrice.");

            var list = new List<PriceRecord>(cfg.TotalDays);
            var rng = cfg.NoisePct > 0 ? new Random(cfg.Seed) : null;
            double priceRange = cfg.MaxPrice - cfg.StartPrice;
            int fullCycleDays = cfg.HalfCycleDays * (cfg.IncludeDownLeg ? 2 : 1);

            for (int cycle = 0; cycle < cfg.Cycles; cycle++)
            {
                // Up leg
                for (int d = 0; d < cfg.HalfCycleDays; d++)
                {
                    double progress = (double)d / (cfg.HalfCycleDays - 1); // 0 .. 1
                    double basePrice = cfg.StartPrice + priceRange * progress;
                    AppendBar(list, cfg, cycle, fullCycleDays, d, basePrice, rng);
                }

                if (!cfg.IncludeDownLeg) continue;

                // Down leg
                for (int d = 0; d < cfg.HalfCycleDays; d++)
                {
                    double progress = (double)d / (cfg.HalfCycleDays - 1); // 0 .. 1
                    double basePrice;
                    if (cfg.SymmetricDownLeg)
                        basePrice = cfg.MaxPrice - priceRange * progress; // linear descent
                    else
                        basePrice = cfg.MaxPrice - priceRange * Math.Pow(progress, 1.2); // slight curve
                    AppendBar(list, cfg, cycle, fullCycleDays, cfg.HalfCycleDays + d, basePrice, rng);
                }
            }
            return list.ToArray();
        }

        private static void AppendBar(List<PriceRecord> list, CyclicalPriceSeriesConfig cfg,
            int cycle, int fullCycleDays, int dayInCycle, double basePrice, Random rng)
        {
            var dayIndex = cycle * fullCycleDays + dayInCycle;
            var date = cfg.StartDate.AddDays(dayIndex);
            double noise = 0.0;
            if (rng != null && cfg.NoisePct > 0)
                noise = (rng.NextDouble() - 0.5) * cfg.NoisePct * basePrice;

            double close = Math.Round(basePrice + noise, 4);
            double open = Math.Round(close * (1 + 0.0005), 4);
            double high = Math.Round(Math.Max(open, close) * 1.001, 4);
            double low = Math.Round(Math.Min(open, close) * 0.999, 4);
            long volume = cfg.BaseVolume;
            list.Add(new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume));
        }

        /// <summary>
        /// Expected number of direction switches (slope changes) in the close price series.
        /// Each full cycle (Up+Down) introduces 2 segments; slope changes: first non-zero + one change per boundary.
        /// For delta logic that: (a) opens on first slope; (b) on each change closes then opens same bar; (c) final close at end,
        /// expected trades (stock) = If IncludeDownLeg: 2 * Cycles (entries) and 2 * Cycles (exits) + 1 final exit? (depends on implementation).
        /// This helper returns raw switch count (including initial) for verification.
        /// </summary>
        public static int GetDirectionSwitchCount(CyclicalPriceSeriesConfig cfg)
        {
            if (!cfg.IncludeDownLeg)
                return cfg.Cycles; // Only rising segments: one initial per cycle (no reversal inside cycle)
            return cfg.Cycles * 2; // Up, Down per cycle
        }
    }
}
