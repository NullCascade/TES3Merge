using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TES3Merge.Tests
{
    internal static class Utility
    {
        internal static TES3Lib.Base.Record? FindRecord(this TES3Lib.TES3 plugin, string id)
        {
            return plugin.Records.FirstOrDefault(r => r.GetEditorId() == $"{id}\0");
        }

    }
}
