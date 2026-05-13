using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trade.Polygon2
{
    internal sealed class ContractKey
    {
        public string RawTicker;
        public string Underlying;
        public bool IsCall;
        public DateTime Expiration;
        public double Strike;

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}|{1}|{2:yyyy-MM-dd}|{3}",
                Underlying,
                IsCall ? "C" : "P",
                Expiration,
                Strike);
        }
    }

    internal sealed class ContractKeyComparer : IComparer<ContractKey>
    {
        public static readonly ContractKeyComparer Instance = new ContractKeyComparer();

        // Sort order: Underlying asc, Calls before Puts, Expiration asc, Strike asc, RawTicker tie-breaker
        // Note: When used in BulkDataSorter, additional sorting by Timestamp then OriginalIndex is applied
        public int Compare(ContractKey a, ContractKey b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            int cmp = string.CompareOrdinal(a.Underlying, b.Underlying);
            if (cmp != 0) return cmp;

            if (a.IsCall != b.IsCall)
                return a.IsCall ? -1 : 1; // Calls before puts

            cmp = a.Expiration.CompareTo(b.Expiration);
            if (cmp != 0) return cmp;

            cmp = a.Strike.CompareTo(b.Strike);
            if (cmp != 0) return cmp;

            return string.CompareOrdinal(a.RawTicker, b.RawTicker);
        }
    }
}
