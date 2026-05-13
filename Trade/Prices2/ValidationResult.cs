using System;
using System.Collections.Generic;

namespace Trade.Prices2
{
    /// <summary>
    ///     Represents the result of a data validation operation, containing validation status,
    ///     error messages, warnings, and metadata about the validated records.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        ///     Gets or sets a value indicating whether the validation passed successfully.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        ///     Gets or sets the list of error messages encountered during validation.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the list of warning messages encountered during validation.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        ///     Gets or sets the total number of records that were validated.
        /// </summary>
        public int TotalRecords { get; set; }

        /// <summary>
        ///     Gets or sets the date/time of the first record in the validated dataset.
        /// </summary>
        public DateTime? FirstRecord { get; set; }

        /// <summary>
        ///     Gets or sets the date/time of the last record in the validated dataset.
        /// </summary>
        public DateTime? LastRecord { get; set; }

        /// <summary>
        ///     Returns a string representation of the validation result including status,
        ///     record count, and error/warning counts.
        /// </summary>
        /// <returns>A formatted string describing the validation result</returns>
        public override string ToString()
        {
            var validationStatus = IsValid ? "VALID" : "INVALID";
            return
                $"Validation: {validationStatus}, {TotalRecords} records, {Errors.Count} errors, {Warnings.Count} warnings";
        }
    }
}