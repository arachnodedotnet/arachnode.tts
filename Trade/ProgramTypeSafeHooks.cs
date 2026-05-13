namespace Trade
{
    /// <summary>
    /// Type-safe indirection to Program private helper without direct private access at compile time.
    /// </summary>
    internal static class ProgramTypeSafeHooks
    {
        public static void AssertPnLConsistency(GeneticIndividual ind)
        {
            //var mi = typeof(Program).GetMethod("AssertPnLConsistency", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            //if (mi != null)
            //    mi.Invoke(null, new object[] { ind, "CalculateFitness" });
        }
    }
}