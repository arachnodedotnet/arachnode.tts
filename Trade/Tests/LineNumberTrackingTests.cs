using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Trade.IVPreCalc2;

namespace Trade.Tests
{
    [TestClass]
    public class LineNumberTrackingTests
    {
        private static string WriteTempFile(string dirPrefix, string fileName, string content)
        {
            var dir = Path.Combine(Path.GetTempPath(), dirPrefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, content, Encoding.UTF8);
            return path;
        }

        private static void CaptureConsole(out StringWriter writer, out TextWriter original)
        {
            original = Console.Out;
            writer = new StringWriter();
            Console.SetOut(writer);
        }

        private static void RestoreConsole(TextWriter original)
        {
            Console.SetOut(original);
        }
        
        [TestMethod][TestCategory("Core")]
        public async Task TestLineNumberPrecision_ControlledData()
        {
            // IMPORTANT: filename must start with date for tracer to load it.
            var content = @"ticker,volume,open,close,high,low,window_start,transactions
O:TEST250919C00100000,1,1.0,1.0,1.0,1.0,1758202980000000000,1
O:TEST250919C00100000,1,1.1,1.1,1.1,1.1,1758206280000000000,1
O:TEST250919C00100000,1,1.2,1.2,1.2,1.2,1758216300000000000,1
O:TEST250919C00200000,1,2.0,2.0,2.0,2.0,1758202980000000000,1
O:TEST250919C00200000,1,2.1,2.1,2.1,2.1,1758206280000000000,1";
            var path = WriteTempFile("LineNumberCtrl", "2025-09-18_us_options_opra_minute_aggs.csv", content);
            var dir = Path.GetDirectoryName(path);
            StringWriter sw; TextWriter orig;
            CaptureConsole(out sw, out orig);
            try
            {
                using (var tracer = new BulkFileContractTracer(dir))
                {
                    var date = new DateTime(2025, 9, 18);
                    var r1 = await tracer.TraceContractBackwardsAsync("O:TEST250919C00100000", date, date);
                    Assert.IsNotNull(r1);
                    Assert.AreEqual(3, r1.Prices[0].RecordCount);
                    var r2 = await tracer.TraceContractBackwardsAsync("O:TEST250919C00200000", date, date);
                    Assert.IsNotNull(r2);
                    Assert.AreEqual(2, r2.Prices[0].RecordCount);
                }
            }
            finally { RestoreConsole(orig); }
            var output = sw.ToString();
            StringAssert.Contains(output, "Lines 2-5"); // 3 records + lookahead
            StringAssert.Contains(output, "Lines 5-7"); // 2 records + EOF
            StringAssert.Contains(output, "Found: 3 records");
            StringAssert.Contains(output, "Found: 2 records");
            try { Directory.Delete(dir, true); } catch { }
        }

        [TestMethod][TestCategory("Core")]
        public async Task TestUserScenario_A250919C00120000_Lines2to7()
        {
            var content = @"ticker,volume,open,close,high,low,window_start,transactions
O:A250919C00120000,1,7.4,7.4,7.4,7.4,1758202980000000000,1
O:A250919C00120000,1,8,8,8,8,1758206280000000000,1
O:A250919C00120000,5,8.08,8.09,8.09,8.08,1758216300000000000,3
O:A250919C00120000,1,7.96,7.96,7.96,7.96,1758220320000000000,1
O:A250919C00120000,1,7.6,7.6,7.6,7.6,1758225120000000000,1
O:A250919C00125000,1,3.05,3.05,3.05,3.05,1758205500000000000,1
O:A250919C00125000,1,3.35,3.35,3.35,3.35,1758206040000000000,1
O:A250919C00130000,1,0.3,0.3,0.3,0.3,1758203400000000000,1";
            var path = WriteTempFile("UserScenario", "2025-09-18_us_options_opra_minute_aggs.csv", content);
            var dir = Path.GetDirectoryName(path);
            StringWriter sw; TextWriter orig; CaptureConsole(out sw, out orig);
            try
            {
                using (var tracer = new BulkFileContractTracer(dir))
                {
                    var date = new DateTime(2025, 9, 18);
                    var r = await tracer.TraceContractBackwardsAsync("O:A250919C00120000", date, date);
                    Assert.AreEqual(5, r.Prices[0].RecordCount);
                }
            }
            finally { RestoreConsole(orig); }
            var output = sw.ToString();
            StringAssert.Contains(output, "Lines 2-7");
            StringAssert.Contains(output, "Found: 5 records");
            StringAssert.Contains(output, "Contract: O:A250919C00120000");
            try { Directory.Delete(dir, true); } catch { }
        }

        [TestMethod][TestCategory("Core")]
        public async Task ContractOnlyAtEnd_ValidatesFinalRange()
        {
            var content = @"ticker,volume,open,close,high,low,window_start,transactions
O:AAA250919C00100000,1,1,1,1,1,1758202980000000000,1
O:BBB250919C00100000,1,1,1,1,1,1758202980000000000,1
O:CCC250919C00100000,1,1,1,1,1,1758202980000000000,1
O:TARGET250919C00100000,1,2,2,2,2,1758202980000000000,1
O:TARGET250919C00100000,1,3,3,3,3,1758206280000000000,1";
            var path = WriteTempFile("EndOnly", "2025-09-18_us_options_opra_minute_aggs.csv", content);
            var dir = Path.GetDirectoryName(path);
            StringWriter sw; TextWriter orig; CaptureConsole(out sw, out orig);
            try
            {
                using (var tracer = new BulkFileContractTracer(dir))
                {
                    var date = new DateTime(2025, 9, 18);
                    var r = await tracer.TraceContractBackwardsAsync("O:TARGET250919C00100000", date, date);
                    Assert.AreEqual(2, r.Prices[0].RecordCount);
                }
            }
            finally { RestoreConsole(orig); }
            StringAssert.Contains(sw.ToString(), "Found: 2 records");
            try { Directory.Delete(dir, true); } catch { }
        }

        [TestMethod][TestCategory("Core")]
        public async Task ContractAbsent_ReturnsEmpty()
        {
            var content = @"ticker,volume,open,close,high,low,window_start,transactions
O:AAA250919C00100000,1,1,1,1,1,1758202980000000000,1
O:BBB250919C00100000,1,1,1,1,1,1758202980000000000,1";
            var path = WriteTempFile("Absent", "2025-09-18_us_options_opra_minute_aggs.csv", content);
            var dir = Path.GetDirectoryName(path);
            StringWriter sw; TextWriter orig; CaptureConsole(out sw, out orig);
            try
            {
                using (var tracer = new BulkFileContractTracer(dir))
                {
                    var date = new DateTime(2025, 9, 18);
                    var r = await tracer.TraceContractBackwardsAsync("O:NOTFOUND250919C00100000", date, date);
                    Assert.AreEqual(0, r.Prices.Count);
                }
            }
            finally { RestoreConsole(orig); }
            StringAssert.Contains(sw.ToString(), "Found: 0 records");
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}