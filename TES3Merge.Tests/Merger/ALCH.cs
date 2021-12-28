using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using static TES3Merge.Tests.Utility;

namespace TES3Merge.Tests.Merger
{
    /// <summary>
    /// Special cases to consider for this record:
    /// - Effects can be added and removed.
    /// - Effects can be changed, and effect data can be made strange if merged dumbly.
    /// </summary>
    [TestClass]
    public class ALCH : RecordTest<TES3Lib.Records.ALCH>
    {
        internal TES3Lib.Records.ALCH MergedDefault;

        static readonly string[] BasicMergeMasters = new string[] { "merge_base.esp", "merge_edit_all.esp", "merge_minor_tweaks.esp" };
        static readonly string[] AddedEffectsMergeMasters = new string[] { "merge_base.esp", "merge_edit_all.esp", "merge_add_effects.esp", "merge_minor_tweaks.esp" };

        public ALCH()
        {
            MergedDefault = CreateMergedRecord("merge_alchemy", AddedEffectsMergeMasters);
        }

        internal override void LogRecordsEffects(TES3Lib.Records.ALCH merged, params string[] plugins)
        {
            foreach (var parent in plugins)
            {
                var plugin = RecordCache[parent];
                Logger.LogMessage($"{plugin} : {plugin.ENAM?.Count}");
                LogEffects(plugin.ENAM);
            }
            Logger.LogMessage($"{MergedObjectsPluginName} : {merged.ENAM?.Count}");
            LogEffects(merged.ENAM);
        }

        [TestMethod]
        public void EditorId()
        {
            LogRecords("NAME.EditorId", MergedDefault, BasicMergeMasters);

            Assert.AreEqual(MergedDefault.NAME.EditorId, GetCached("merge_minor_tweaks.esp").NAME.EditorId);
        }

        [TestMethod]
        public void ModelPath()
        {
            LogRecords("MODL.ModelPath", MergedDefault, BasicMergeMasters);

            Assert.AreEqual(MergedDefault.MODL.ModelPath, GetCached("merge_edit_all.esp").MODL.ModelPath);
        }

        [TestMethod]
        public void IconPath()
        {
            LogRecords("TEXT.IconPath", MergedDefault, BasicMergeMasters);

            Assert.AreEqual(MergedDefault.TEXT.IconPath, GetCached("merge_edit_all.esp").TEXT.IconPath);
        }

        [TestMethod]
        public void DisplayName()
        {
            LogRecords("FNAM.FileName", MergedDefault, BasicMergeMasters);

            Assert.AreEqual(MergedDefault.FNAM.FileName, GetCached("merge_minor_tweaks.esp").FNAM.FileName);
        }

        [TestMethod]
        public void Value()
        {
            LogRecords("ALDT.Value", MergedDefault, BasicMergeMasters);

            Assert.AreEqual(MergedDefault.ALDT.Value, GetCached("merge_edit_all.esp").ALDT.Value);
        }

        [TestMethod]
        public void Weight()
        {
            LogRecords("ALDT.Weight", MergedDefault, BasicMergeMasters);

            Assert.AreEqual(MergedDefault.ALDT.Weight, GetCached("merge_edit_all.esp").ALDT.Weight);
        }

        [TestMethod]
        public void ScriptName()
        {
            LogRecords("SCRI.ScriptName", MergedDefault, BasicMergeMasters);

            Assert.AreEqual(MergedDefault.SCRI?.ScriptName, GetCached("merge_edit_all.esp").SCRI?.ScriptName);
        }

        [TestMethod]
        public void Effects()
        {
            LogRecordsEffects(MergedDefault, AddedEffectsMergeMasters);

            // Ensure we have the right number of effects.
            Assert.IsNotNull(MergedDefault.ENAM);
            Assert.IsNotNull(GetCached("merge_edit_all.esp").ENAM);
            Assert.IsNotNull(GetCached("merge_add_effects.esp").ENAM);
            Assert.AreEqual(MergedDefault.ENAM.Count, GetCached("merge_add_effects.esp").ENAM.Count);

            // Make sure we ended up with the right first effect.
            Assert.AreEqual(MergedDefault.ENAM[0].MagicEffect, TES3Lib.Enums.MagicEffect.BoundCuirass);

            // Make sure all the properties were respected from the changed effect.
            // We don't want a changed effect to end up with a bunch of invalid properties.
            Assert.AreEqual(MergedDefault.ENAM[0].Skill, GetCached("merge_edit_all.esp").ENAM[0].Skill);
            Assert.AreEqual(MergedDefault.ENAM[0].Attribute, GetCached("merge_edit_all.esp").ENAM[0].Attribute);
            Assert.AreEqual(MergedDefault.ENAM[0].Magnitude, GetCached("merge_edit_all.esp").ENAM[0].Magnitude);
            Assert.AreEqual(MergedDefault.ENAM[0].Duration, GetCached("merge_edit_all.esp").ENAM[0].Duration);

            // Ensure that we carried over the right second effect.
            Assert.AreEqual(MergedDefault.ENAM[1], GetCached("merge_add_effects.esp").ENAM[1]);
        }
    }
}