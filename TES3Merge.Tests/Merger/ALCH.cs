using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System;

namespace TES3Merge.Tests.Merger
{
    /// <summary>
    /// Special cases to consider for this record:
    /// - Effects can be added and removed.
    /// - Effects can be changed, and effect data can be made strange if merged dumbly.
    /// </summary>
    [TestClass]
    public class ALCH
    {
        TES3Lib.Records.ALCH pluginBaseRecord;
        TES3Lib.Records.ALCH pluginEditAllRecord;
        TES3Lib.Records.ALCH pluginAddEffectsRecord;
        TES3Lib.Records.ALCH pluginMinorTweaksRecord;

        TES3Lib.Records.ALCH pluginDefaultMerged;

        public ALCH()
        {
            pluginBaseRecord = FileLoader.FindRecord("merge_base.esp", "merge_alchemy") as TES3Lib.Records.ALCH ?? throw new Exception("Plugin did not have required record.");
            pluginEditAllRecord = FileLoader.FindRecord("merge_edit_all.esp", "merge_alchemy") as TES3Lib.Records.ALCH ?? throw new Exception("Plugin did not have required record.");
            pluginAddEffectsRecord = FileLoader.FindRecord("merge_add_effects.esp", "merge_alchemy") as TES3Lib.Records.ALCH ?? throw new Exception("Plugin did not have required record.");
            pluginMinorTweaksRecord = FileLoader.FindRecord("merge_minor_tweaks.esp", "merge_alchemy") as TES3Lib.Records.ALCH ?? throw new Exception("Plugin did not have required record.");

            pluginDefaultMerged = new TES3Lib.Records.ALCH(pluginMinorTweaksRecord.SerializeRecord());
            RecordMerger.Merge(pluginDefaultMerged, pluginBaseRecord, pluginAddEffectsRecord);
            RecordMerger.Merge(pluginDefaultMerged, pluginBaseRecord, pluginEditAllRecord);
        }

        [TestMethod]
        public void EditorId()
        {
            Logger.LogMessage($"merge_base.esp : {pluginBaseRecord.NAME.EditorId}");
            Logger.LogMessage($"merge_edit_all.esp : {pluginEditAllRecord.NAME.EditorId}");
            Logger.LogMessage($"merge_minor_tweaks.esp : {pluginMinorTweaksRecord.NAME.EditorId}");
            Logger.LogMessage($"merged.esp : {pluginDefaultMerged.NAME.EditorId}");

            Assert.AreEqual(pluginDefaultMerged.NAME.EditorId, pluginMinorTweaksRecord.NAME.EditorId);
        }

        [TestMethod]
        public void ModelPath()
        {
            Logger.LogMessage($"merge_base.esp : {pluginBaseRecord.MODL.ModelPath}");
            Logger.LogMessage($"merge_edit_all.esp : {pluginEditAllRecord.MODL.ModelPath}");
            Logger.LogMessage($"merge_minor_tweaks.esp : {pluginMinorTweaksRecord.MODL.ModelPath}");
            Logger.LogMessage($"merged.esp : {pluginDefaultMerged.MODL.ModelPath}");

            Assert.AreEqual(pluginDefaultMerged.MODL.ModelPath, pluginEditAllRecord.MODL.ModelPath);
        }

        [TestMethod]
        public void IconPath()
        {
            Logger.LogMessage($"merge_base.esp : {pluginBaseRecord.TEXT.IconPath}");
            Logger.LogMessage($"merge_edit_all.esp : {pluginEditAllRecord.TEXT.IconPath}");
            Logger.LogMessage($"merge_minor_tweaks.esp : {pluginMinorTweaksRecord.TEXT.IconPath}");
            Logger.LogMessage($"merged.esp : {pluginDefaultMerged.TEXT.IconPath}");

            Assert.AreEqual(pluginDefaultMerged.TEXT.IconPath, pluginEditAllRecord.TEXT.IconPath);
        }

        [TestMethod]
        public void DisplayName()
        {
            Logger.LogMessage($"merge_base.esp : {pluginBaseRecord.FNAM.FileName}");
            Logger.LogMessage($"merge_edit_all.esp : {pluginEditAllRecord.FNAM.FileName}");
            Logger.LogMessage($"merge_minor_tweaks.esp : {pluginMinorTweaksRecord.FNAM.FileName}");
            Logger.LogMessage($"merged.esp : {pluginDefaultMerged.FNAM.FileName}");

            Assert.AreEqual(pluginDefaultMerged.FNAM.FileName, pluginMinorTweaksRecord.FNAM.FileName);
        }

        [TestMethod]
        public void Value()
        {
            Logger.LogMessage($"merge_base.esp : {pluginBaseRecord.ALDT.Value}");
            Logger.LogMessage($"merge_edit_all.esp : {pluginEditAllRecord.ALDT.Value}");
            Logger.LogMessage($"merge_minor_tweaks.esp : {pluginMinorTweaksRecord.ALDT.Value}");
            Logger.LogMessage($"merged.esp : {pluginDefaultMerged.ALDT.Value}");

            Assert.AreEqual(pluginDefaultMerged.ALDT.Value, pluginEditAllRecord.ALDT.Value);
        }

        [TestMethod]
        public void Weight()
        {
            Logger.LogMessage($"merge_base.esp : {pluginBaseRecord.ALDT.Weight}");
            Logger.LogMessage($"merge_edit_all.esp : {pluginEditAllRecord.ALDT.Weight}");
            Logger.LogMessage($"merge_minor_tweaks.esp : {pluginMinorTweaksRecord.ALDT.Weight}");
            Logger.LogMessage($"merged.esp : {pluginDefaultMerged.ALDT.Weight}");

            Assert.AreEqual(pluginDefaultMerged.ALDT.Weight, pluginEditAllRecord.ALDT.Weight);
        }

        [TestMethod]
        public void ScriptName()
        {
            Logger.LogMessage($"merge_base.esp : {pluginBaseRecord.SCRI?.ScriptName}");
            Logger.LogMessage($"merge_edit_all.esp : {pluginEditAllRecord.SCRI?.ScriptName}");
            Logger.LogMessage($"merge_minor_tweaks.esp : {pluginMinorTweaksRecord.SCRI?.ScriptName}");
            Logger.LogMessage($"merged.esp : {pluginDefaultMerged.SCRI?.ScriptName}");

            Assert.AreEqual(pluginDefaultMerged.SCRI?.ScriptName, pluginEditAllRecord.SCRI?.ScriptName);
        }

        internal static void LogEffects(List<TES3Lib.Subrecords.ALCH.ENAM>? effects)
        {
            if (effects == null) return;

            foreach (var effect in effects)
            {
                Logger.LogMessage($"  - Effect: {effect.MagicEffect}; Skill: {effect.Skill}; Attribute: {effect.Attribute}; Magnitude: {effect.Magnitude}; Duration: {effect.Duration}");
            }
        }

        [TestMethod]
        public void Effects()
        {
            Logger.LogMessage($"merge_base.esp : {pluginBaseRecord.ENAM?.Count} effects");
            LogEffects(pluginBaseRecord.ENAM);
            Logger.LogMessage($"merge_edit_all.esp : {pluginEditAllRecord.ENAM?.Count} effects");
            LogEffects(pluginEditAllRecord.ENAM);
            Logger.LogMessage($"merge_add_effects.esp : {pluginAddEffectsRecord.ENAM?.Count} effects");
            LogEffects(pluginAddEffectsRecord.ENAM);
            Logger.LogMessage($"merge_minor_tweaks.esp : {pluginMinorTweaksRecord.ENAM?.Count} effects");
            LogEffects(pluginMinorTweaksRecord.ENAM);
            Logger.LogMessage($"merged.esp : {pluginDefaultMerged.ENAM?.Count} effects");
            LogEffects(pluginDefaultMerged.ENAM);

            // Ensure we have the right number of effects.
            Assert.IsNotNull(pluginDefaultMerged.ENAM);
            Assert.IsNotNull(pluginEditAllRecord.ENAM);
            Assert.IsNotNull(pluginAddEffectsRecord.ENAM);
            Assert.AreEqual(pluginDefaultMerged.ENAM.Count, pluginAddEffectsRecord.ENAM.Count);

            // Make sure we ended up with the right first effect.
            Assert.AreEqual(pluginDefaultMerged.ENAM[0].MagicEffect, TES3Lib.Enums.MagicEffect.BoundCuirass);

            // Make sure all the properties were respected from the changed effect.
            // We don't want a changed effect to end up with a bunch of invalid properties.
            Assert.AreEqual(pluginDefaultMerged.ENAM[0].Skill, pluginEditAllRecord.ENAM[0].Skill);
            Assert.AreEqual(pluginDefaultMerged.ENAM[0].Attribute, pluginEditAllRecord.ENAM[0].Attribute);
            Assert.AreEqual(pluginDefaultMerged.ENAM[0].Magnitude, pluginEditAllRecord.ENAM[0].Magnitude);
            Assert.AreEqual(pluginDefaultMerged.ENAM[0].Duration, pluginEditAllRecord.ENAM[0].Duration);

            // Ensure that we carried over the right second effect.
            Assert.AreEqual(pluginDefaultMerged.ENAM[1], pluginAddEffectsRecord.ENAM[1]);
        }
    }
}