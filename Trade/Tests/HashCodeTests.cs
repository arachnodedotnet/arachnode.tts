using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Polygon2;

namespace Trade.Tests
{
    [TestClass]
    public class HashCodeTests
    {
        [TestMethod][TestCategory("Core")]
        public void Combine_ThreeInts_ReturnsConsistentHash()
        {
            int a = 1, b = 2, c = 3;
            int hash1 = HashCode.Combine(a, b, c);
            int hash2 = HashCode.Combine(a, b, c);
            Assert.AreEqual(hash1, hash2);
        }

        [TestMethod][TestCategory("Core")]
        public void Combine_NullValues_DoesNotThrow()
        {
            string a = null, b = "test", c = null;
            int hash = HashCode.Combine(a, b, c);
            Assert.IsInstanceOfType(hash, typeof(int));
        }

        [TestMethod][TestCategory("Core")]
        public void Combine_DifferentValues_ProducesDifferentHashes()
        {
            int hash1 = HashCode.Combine(1, 2, 3);
            int hash2 = HashCode.Combine(3, 2, 1);
            Assert.AreNotEqual(hash1, hash2);
        }
    }
}
