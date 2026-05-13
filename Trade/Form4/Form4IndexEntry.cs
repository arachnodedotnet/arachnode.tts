using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trade.Form4
{
    /// <summary>
    /// Represents a parsed Form 4 filing entry from the daily index
    /// </summary>
    public class Form4IndexEntry
    {
        public string CIK { get; set; }
        public string CompanyName { get; set; }
        public string FormType { get; set; }
        public DateTime FilingDate { get; set; }
        public string EdgarUrl { get; set; }
        public string AccessionNumber { get; set; }

        public override string ToString()
        {
            return $"{FilingDate:yyyy-MM-dd} | {CompanyName} (CIK:{CIK}) | {FormType} | {AccessionNumber}";
        }
    }
}
