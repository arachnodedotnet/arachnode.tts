using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trade.Form4
{
    sealed class PendingEntry
    {
        public IndividualForm4Signal Signal { get; set; }
        public DateTime EntryDate { get; set; } // scheduled/validated entry session (at open)
    }
}
