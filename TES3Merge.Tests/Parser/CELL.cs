using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TES3Lib;
using TES3Lib.Base;
using static TES3Merge.Tests.Utility;

namespace TES3Merge.Tests.Parser
{
    [TestClass]
    public class CELL
    {
        [TestMethod]
        public void Parse()
        {
            // load esp
            var path = Path.Combine("Plugins", "F&F_base.esm");
            var file = TES3.TES3LoadSync(path, new() { "CELL" });

            // serialize CELL to bytes
            var errored = 0;
            foreach (var r in file.Records)
            {
                if (r is null)
                {
                    continue;
                }
                if (!r.Name.Equals("CELL"))
                {
                    continue;
                }

                var newRecord = Activator.CreateInstance(r.GetType(), new object[] { r.GetRawLoadedBytes() }) as TES3Lib.Records.CELL ?? throw new Exception("Could not create activator instance.");

                var newSerialized = newRecord.SerializeRecordForMerge();
                var lastSerialized = (r as TES3Lib.Records.CELL)!.SerializeRecordForMerge();

                var result = lastSerialized.SequenceEqual(newSerialized);
                if (!result)
                {
                    //var outdir = new FileInfo(path).Directory?.FullName;
                    //File.WriteAllBytes(Path.Combine(outdir!, "file1.bin"), lastSerialized);
                    //File.WriteAllBytes(Path.Combine(outdir!, "file2.bin"), newSerialized);

                    errored++;
                }
            }

            Assert.IsTrue(errored == 0);
        }
    }
}
