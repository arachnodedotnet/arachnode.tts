using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trade.Form4
{

    /// <summary>
    /// Represents parsed Form 4 transaction data
    /// </summary>
    public class Form4Transaction
    {
        public bool Cached { get; set; }
        public string XmlUrl { get; set; }
        public string IssuerName { get; set; }
        public string IssuerCIK { get; set; }
        public string IssuerTicker { get; set; }

        public string ReportingOwnerName { get; set; }
        public string ReportingOwnerCIK { get; set; }
        public bool IsDirector { get; set; }
        public bool IsOfficer { get; set; }
        public bool IsTenPercentOwner { get; set; }
        public bool IsOther { get; set; }
        public string OfficerTitle { get; set; }
        public bool Aff10b5One { get; set; }

        public DateTime? TransactionDate { get; set; }
        public DateTime? PeriodOfReport { get; set; }  // Period end date for the Form 4 filing
        public string SecurityTitle { get; set; }
        public string TransactionCode { get; set; } // P=Purchase, S=Sale, A=Award, etc.
        public decimal? SharesTransacted { get; set; }
        public decimal? PricePerShare { get; set; }
        public decimal? SharesOwnedAfter { get; set; }
        public bool IsDirectOwnership { get; set; }

        public string AccessionNumber { get; set; }
        public DateTime FilingDate { get; set; }

        public string AcquiredDisposedCode { get; set; } // "A" or "D"
        public string DirectOrIndirectOwnership { get; set; } // "D" or "I"
        public string NatureOfOwnership { get; set; } // free text when provided

        public string TransactionCodeNorm => (TransactionCode ?? "").Trim().ToUpperInvariant();
        public string AcquiredDisposedCodeNorm => (AcquiredDisposedCode ?? "").Trim().ToUpperInvariant();
        public string DirectOrIndirectOwnershipNorm => (DirectOrIndirectOwnership ?? "").Trim().ToUpperInvariant();

        public bool IsAcquisition => AcquiredDisposedCodeNorm == "A";
        public bool IsDisposition => AcquiredDisposedCodeNorm == "D";
        public bool IsOpenMarketPurchase => TransactionCodeNorm == "P" && IsAcquisition && !Aff10b5One;  // your core Form-4 long signal

        public bool IsDerivative => (SecurityTitle ?? "").StartsWith("[DERIVATIVE]", StringComparison.OrdinalIgnoreCase);

        public override string ToString()
        {
            var action = GetTransactionDescription();

            var sharesText = SharesTransacted.HasValue ? SharesTransacted.Value.ToString("N0") : "?";
            var priceText = PricePerShare.HasValue ? PricePerShare.Value.ToString("F2") : "?";

            decimal value = 0m;
            if (SharesTransacted.HasValue && PricePerShare.HasValue)
                value = SharesTransacted.Value * PricePerShare.Value;

            var valueText = value > 0 ? value.ToString("N0") : "?";
            var ownsText = SharesOwnedAfter.HasValue ? SharesOwnedAfter.Value.ToString("N0") : "?";

            return $"{FilingDate:yyyy-MM-dd} | {IssuerTicker ?? IssuerName} | {ReportingOwnerName} ({OfficerTitle ?? "Owner"}) | " +
                   $"{action} {sharesText} shares @ ${priceText} (${valueText}) | Owns: {ownsText} after";
        }

        public string GetTransactionDescription()
        {
            switch (TransactionCode)
            {
                case "P": return "PURCHASE";
                case "S": return "SALE";
                case "A": return "AWARD";
                case "D": return "DISPOSITION";
                case "F": return "TAX PAYMENT";
                case "G": return "GIFT";
                case "M": return "EXERCISE";
                case "C": return "CONVERSION";
                case "J": return "OTHER";
                default: return TransactionCode ?? "UNKNOWN";
            }
        }
    }
}
