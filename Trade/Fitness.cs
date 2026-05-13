using System;

namespace Trade
{
    [Serializable]
    public class Fitness
    {
        public Fitness(double dollarGain, double percentGain, double fitnessScore = 0)
        {
            DollarGain = dollarGain;
            PercentGain = percentGain;
            FitnessScore = fitnessScore;
        }

        public Fitness()
        {
        }

        public double DollarGain { get; set; }
        public double PercentGain { get; set; }
        public double? FitnessScore { get; set; }

        // Robustness metrics (optional)
        public double Sharpe { get; set; }
        public double CAGR { get; set; }
        public double MaxDrawdown { get; set; }
    }
}