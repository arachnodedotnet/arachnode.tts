using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trade.Form4
{
    sealed class ExposureSnapshot
    {
        public DateTime Date { get; set; }
        public int VooPositions { get; set; }
        public int Form4Positions { get; set; }
        public double VooValue { get; set; }
        public double Form4Value { get; set; }
        public double Cash { get; set; }
        public double Total => Cash + VooValue + Form4Value;
    }
}
