using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using System;
using System.Collections.Generic;

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
        internal Dictionary<string, TES3Lib.Records.ALCH> RecordCache = new();

        internal TES3Lib.Records.ALCH MergedDefault;

        public ALCH()
        {
            MergedDefault = Utility.CreateMergedRecord("merge_alchemy", RecordCache, "merge_base.esp", "merge_edit_all.esp", "merge_add_effects.esp", "merge_minor_tweaks.esp");
        }

        internal TES3Lib.Records.ALCH? GetCachedRecord(string plugin)
        {
            return RecordCache[plugin];
        }

        [TestMethod]
        public void EditorId()
        {
            Utility.LogRecords(RecordCache, "NAME.EditorId", MergedDefault, "merge_base.esp", "merge_edit_all.esp", "merge_minor_tweaks.esp");

            Assert.AreEqual(MergedDefault.NAME.EditorId, RecordCache["merge_minor_tweaks.esp"].NAME.EditorId);
        }

        [TestMethod]
        public void ModelPath()
        {
            Utility.LogRecords(RecordCache, "MODL.ModelPath", MergedDefault, "merge_base.esp", "merge_edit_all.esp", "merge_minor_tweaks.esp");

            Assert.AreEqual(MergedDefault.MODL.ModelPath, RecordCache["merge_edit_all.esp"].MODL.ModelPath);
        }

        [TestMethod]
        public void IconPath()
        {
            Utility.LogRecords(RecordCache, "TEXT.IconPath", MergedDefault, "merge_base.esp", "merge_edit_all.esp", "merge_minor_tweaks.esp");

            Assert.AreEqual(MergedDefault.TEXT.IconPath, RecordCache["merge_edit_all.esp"].TEXT.IconPath);
        }

        [TestMethod]
        public void DisplayName()
        {
            Utility.LogRecords(RecordCache, "FNAM.FileName", MergedDefault, "merge_base.esp", "merge_edit_all.esp", "merge_minor_tweaks.esp");

            Assert.AreEqual(MergedDefault.FNAM.FileName, RecordCache["merge_minor_tweaks.esp"].FNAM.FileName);
        }

        [TestMethod]
        public void Value()
        {
            Utility.LogRecords(RecordCache, "ALDT.Value", MergedDefault, "merge_base.esp", "merge_edit_all.esp", "merge_minor_tweaks.esp");

            Assert.AreEqual(MergedDefault.ALDT.Value, RecordCache["merge_edit_all.esp"].ALDT.Value);
        }

        [TestMethod]
        public void Weight()
        {
            Utility.LogRecords(RecordCache, "ALDT.Weight", MergedDefault, "merge_base.esp", "merge_edit_all.esp", "merge_minor_tweaks.esp");

            Assert.AreEqual(MergedDefault.ALDT.Weight, RecordCache["merge_edit_all.esp"].ALDT.Weight);
        }

        [TestMethod]
        public void ScriptName()
        {
            Utility.LogRecords(RecordCache, "SCRI.ScriptName", MergedDefault, "merge_base.esp", "merge_edit_all.esp", "merge_minor_tweaks.esp");

            Assert.AreEqual(MergedDefault.SCRI?.ScriptName, RecordCache["merge_edit_all.esp"].SCRI?.ScriptName);
        }

        [TestMethod]
        public void Effects()
        {
            Utility.LogRecordValue(RecordCache, "ENAM.Count", "merge_base.esp");
            Utility.LogEffects(RecordCache["merge_base.esp"].ENAM);
            Utility.LogRecordValue(RecordCache, "ENAM.Count", "merge_edit_all.esp");
            Utility.LogEffects(RecordCache["merge_edit_all.esp"].ENAM);
            Utility.LogRecordValue(RecordCache, "ENAM.Count", "merge_add_effects.esp");
            Utility.LogEffects(RecordCache["merge_add_effects.esp"].ENAM);
            Utility.LogRecordValue(RecordCache, "ENAM.Count", "merge_minor_tweaks.esp");
            Utility.LogEffects(RecordCache["merge_minor_tweaks.esp"].ENAM);
            Utility.LogRecordValue(MergedDefault, "ENAM.Count");
            Utility.LogEffects(MergedDefault.ENAM);

            // Ensure we have the right number of effects.
            Assert.IsNotNull(MergedDefault.ENAM);
            Assert.IsNotNull(RecordCache["merge_edit_all.esp"].ENAM);
            Assert.IsNotNull(RecordCache["merge_add_effects.esp"].ENAM);
            Assert.AreEqual(MergedDefault.ENAM.Count, RecordCache["merge_add_effects.esp"].ENAM.Count);

            // Make sure we ended up with the right first effect.
            Assert.AreEqual(MergedDefault.ENAM[0].MagicEffect, TES3Lib.Enums.MagicEffect.BoundCuirass);

            // Make sure all the properties were respected from the changed effect.
            // We don't want a changed effect to end up with a bunch of invalid properties.
            Assert.AreEqual(MergedDefault.ENAM[0].Skill, RecordCache["merge_edit_all.esp"].ENAM[0].Skill);
            Assert.AreEqual(MergedDefault.ENAM[0].Attribute, RecordCache["merge_edit_all.esp"].ENAM[0].Attribute);
            Assert.AreEqual(MergedDefault.ENAM[0].Magnitude, RecordCache["merge_edit_all.esp"].ENAM[0].Magnitude);
            Assert.AreEqual(MergedDefault.ENAM[0].Duration, RecordCache["merge_edit_all.esp"].ENAM[0].Duration);

            // Ensure that we carried over the right second effect.
            Assert.AreEqual(MergedDefault.ENAM[1], RecordCache["merge_add_effects.esp"].ENAM[1]);
        }
    }
}