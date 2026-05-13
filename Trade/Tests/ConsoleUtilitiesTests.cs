using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class ConsoleUtilitiesTests
    {
        [TestInitialize]
        public void TestInit()
        {
            // Always enable before each test
            ConsoleUtilities.Enabled = true;
        }

        //[TestMethod]
        //public void WriteLine_WritesMessage_WhenEnabled()
        //{
        //    ConsoleUtilities.Enabled = true;
        //    using (var sw = new StringWriter())
        //    {
        //        Console.SetOut(sw);
        //        ConsoleUtilities.WriteLine("Test message");
        //        var output = sw.ToString();
        //        Assert.IsTrue(output.Contains("Test message"));
        //    }
        //}

        //[TestMethod]
        //public void WriteLine_DoesNotWrite_WhenDisabled()
        //{
        //    ConsoleUtilities.Enabled = false;
        //    using (var sw = new StringWriter())
        //    {
        //        Console.SetOut(sw);
        //        ConsoleUtilities.WriteLine("Should not appear");
        //        var output = sw.ToString();
        //        Assert.AreEqual(string.Empty, output);
        //    }
        //}

        //[TestMethod]
        //public void WriteLine_Int_OutputsNumber()
        //{
        //    using (var sw = new StringWriter())
        //    {
        //        Console.SetOut(sw);
        //        ConsoleUtilities.WriteLine(12345);
        //        var output = sw.ToString();
        //        Assert.IsTrue(output.Contains("12345"));
        //    }
        //}

        //[TestMethod]
        //public void WriteLine_NoArgs_WritesNewline()
        //{
        //    using (var sw = new StringWriter())
        //    {
        //        Console.SetOut(sw);
        //        ConsoleUtilities.WriteLine();
        //        var output = sw.ToString();
        //        Assert.IsTrue(output.Contains("\n") || output.Contains("\r\n"));
        //    }
        //}

        //[TestMethod]
        //public void Write_WritesMessage_WhenEnabled()
        //{
        //    using (var sw = new StringWriter())
        //    {
        //        Console.SetOut(sw);
        //        ConsoleUtilities.Write("abc");
        //        var output = sw.ToString();
        //        Assert.IsTrue(output.Contains("abc"));
        //    }
        //}

        //[TestMethod]
        //public void Write_Char_WritesChar_WhenEnabled()
        //{
        //    using (var sw = new StringWriter())
        //    {
        //        Console.SetOut(sw);
        //        ConsoleUtilities.Write('Z');
        //        var output = sw.ToString();
        //        Assert.IsTrue(output.Contains("Z"));
        //    }
        //}

        [TestMethod]
        [TestCategory("Core")]
        public void Write_WithColor_DoesNotThrow()
        {
            // Just ensure no exception is thrown
            ConsoleUtilities.Write("color", ConsoleColor.Red);
            ConsoleUtilities.Write('X', ConsoleColor.Green);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Enabled_CanBeToggled()
        {
            ConsoleUtilities.Enabled = false;
            Assert.IsFalse(ConsoleUtilities.Enabled);
            ConsoleUtilities.Enabled = true;
            Assert.IsTrue(ConsoleUtilities.Enabled);
        }
    }
}