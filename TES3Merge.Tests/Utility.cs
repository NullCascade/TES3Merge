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

        internal static void LogEffects(List<TES3Lib.Subrecords.Shared.Castable.ENAM>? effects)
        {
            if (effects == null) return;

            foreach (var effect in effects)
            {
                Logger.LogMessage($"  - Effect: {effect.MagicEffect}; Skill: {effect.Skill}; Attribute: {effect.Attribute}; Magnitude: {effect.Magnitude}; Duration: {effect.Duration}");
            }
        }
    }
}
