using System;
using System.Collections.Generic;

namespace Trade.Polygon2
{
    [Serializable]
    public class TreeComparisonResult
    {
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public int TotalChecks { get; set; } = 0;
        public int SuccessfulMatches { get; set; } = 0;
        public int Mismatches { get; set; } = 0;
        public long ComparisonTimeMs { get; set; } = 0;
        public bool IsFullySuccessful { get; set; } = false;
    
        public void AddError(string category, string message)
        {
            Errors.Add($"[{category}] {message}");
            TotalChecks++;
        }
    
        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }
    
        public void IncrementSuccess(string message = null)
        {
            SuccessfulMatches++;
            TotalChecks++;
        }
    
        public void CompareProperty<T>(string propertyName, T original, T deserialized, double allowedDifference = 0) where T : IComparable<T>
        {
            TotalChecks++;
        
            if (original == null && deserialized == null)
            {
                SuccessfulMatches++;
                return;
            }
        
            if (original == null || deserialized == null)
            {
                Mismatches++;
                AddError("PROPERTY", $"{propertyName}: null mismatch - original={original}, deserialized={deserialized}");
                return;
            }
        
            // Handle numeric comparisons with tolerance
            if (allowedDifference > 0 && original is double origDouble && deserialized is double deserDouble)
            {
                if (Math.Abs(origDouble - deserDouble) <= allowedDifference)
                {
                    SuccessfulMatches++;
                    return;
                }
                else
                {
                    Mismatches++;
                    AddError("PROPERTY", $"{propertyName}: value mismatch - original={origDouble:F6}, deserialized={deserDouble:F6}, difference={Math.Abs(origDouble - deserDouble):F6}");
                    return;
                }
            }
        
            // Handle long comparisons with tolerance
            if (allowedDifference > 0 && original is long origLong && deserialized is long deserLong)
            {
                if (Math.Abs(origLong - deserLong) <= allowedDifference)
                {
                    SuccessfulMatches++;
                    return;
                }
                else
                {
                    Mismatches++;
                    AddError("PROPERTY", $"{propertyName}: value mismatch - original={origLong}, deserialized={deserLong}, difference={Math.Abs(origLong - deserLong)}");
                    return;
                }
            }
        
            // Standard equality comparison
            if (original.CompareTo(deserialized) == 0)
            {
                SuccessfulMatches++;
            }
            else
            {
                Mismatches++;
                AddError("PROPERTY", $"{propertyName}: value mismatch - original={original}, deserialized={deserialized}");
            }
        }
    
        public void CompareProperty(string propertyName, DateTime original, DateTime deserialized, double allowedDifferenceMs = 1000)
        {
            TotalChecks++;
        
            var differenceMs = Math.Abs((original - deserialized).TotalMilliseconds);
            if (differenceMs <= allowedDifferenceMs)
            {
                SuccessfulMatches++;
            }
            else
            {
                Mismatches++;
                AddError("PROPERTY", $"{propertyName}: DateTime mismatch - original={original:yyyy-MM-dd HH:mm:ss.fff}, deserialized={deserialized:yyyy-MM-dd HH:mm:ss.fff}, difference={differenceMs:F0}ms");
            }
        }
    
        public void CompareProperty(string propertyName, bool original, bool deserialized)
        {
            TotalChecks++;
        
            if (original == deserialized)
            {
                SuccessfulMatches++;
            }
            else
            {
                Mismatches++;
                AddError("PROPERTY", $"{propertyName}: boolean mismatch - original={original}, deserialized={deserialized}");
            }
        }
    }
}