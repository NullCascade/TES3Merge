using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using TES3Lib.Subrecords.Shared;
using static TES3Merge.Tests.Utility;

namespace TES3Merge.Tests.Merger;

/// <summary>
/// Special cases to consider for this record:
/// </summary>
[TestClass]
public class NPC_ : RecordTest<TES3Lib.Records.NPC_>
{
    internal TES3Lib.Records.NPC_ MergedDefault;

    private static readonly string[] BasicMergeMasters = new string[] { "merge_base.esp", "merge_edit_all.esp", "merge_minor_tweaks.esp" };
    private static readonly string[] AddedMergeMasters = new string[] { "merge_base.esp", "merge_edit_all.esp", "merge_add_effects.esp", "merge_minor_tweaks.esp" };

    public NPC_()
    {
        MergedDefault = CreateMergedRecord("zennammu urshumusa", AddedMergeMasters);

        _logger = _host.Services.GetRequiredService<ILogger<NPC_>>();
    }

    [TestMethod]
    public void EditorId()
    {
        LogRecords("NAME.EditorId", MergedDefault, BasicMergeMasters);

        Assert.AreEqual(MergedDefault.NAME.EditorId, GetCached("merge_minor_tweaks.esp").NAME.EditorId);
    }

    [TestMethod]
    public void DisplayName()
    {
        LogRecords("FNAM.FileName", MergedDefault, BasicMergeMasters);

        Assert.AreEqual(MergedDefault.FNAM.FileName, GetCached("merge_minor_tweaks.esp").FNAM.FileName);
    }

    [TestMethod]
    public void ScriptName()
    {
        LogRecords("SCRI.ScriptName", MergedDefault, BasicMergeMasters);

        Assert.AreEqual(MergedDefault.SCRI?.ScriptName, GetCached("merge_edit_all.esp").SCRI?.ScriptName);
    }

    // TODO

    //"race": "Dark Elf",
    //"class": "Barbarian",
    //"faction": "",
    //"head": "b_n_dark elf_m_head_05",
    //"hair": "b_n_dark elf_m_hair_08",
    //"npc_flags": 24,
    //"data": 
    //"spells": [],
    //"ai_data":
    //"travel_destinations"

    [TestMethod]
    public void Inventory()
    {
        LogRecordsInventory(MergedDefault, AddedMergeMasters);

        // this is the load order
        var merge_base = GetCached("merge_base.esp").NPCO;
        var merge_edit_all = GetCached("merge_edit_all.esp").NPCO;
        var merge_add_effects = GetCached("merge_add_effects.esp").NPCO;
        var merge_minor_tweaks = GetCached("merge_minor_tweaks.esp").NPCO;

        // Ensure not null
        Assert.IsNotNull(merge_base);
        Assert.IsNotNull(merge_edit_all);
        Assert.IsNotNull(merge_add_effects);
        Assert.IsNotNull(merge_minor_tweaks);
        Assert.IsNotNull(MergedDefault.AIPackages);

        // TODO
        // make sure all the rest is inclusively merged

        // make sure all the rest is non-inclusively merged


        void LogRecordsInventory(TES3Lib.Records.NPC_ merged, params string[] plugins)
        {
            foreach (var parent in plugins)
            {
                var plugin = RecordCache[parent];
                _logger.LogInformation("{Plugin} : {Count} ({Parent})", plugin, plugin.NPCO?.Count, parent);
                LogRecordsEnumerable(plugin.NPCO);
            }
            _logger.LogInformation("{MergedObjectsPluginName} : {Count}", MergedObjectsPluginName, merged.NPCO?.Count);
            LogRecordsEnumerable(merged.NPCO);
        }
    }

    [TestMethod]
    public void AIPackages()
    {
        LogRecordsAIPackages(MergedDefault, AddedMergeMasters);

        // this is the load order
        var merge_base = GetCached("merge_base.esp").AIPackages;
        var merge_edit_all = GetCached("merge_edit_all.esp").AIPackages;
        var merge_add_effects = GetCached("merge_add_effects.esp").AIPackages;
        var merge_minor_tweaks = GetCached("merge_minor_tweaks.esp").AIPackages;

        // Ensure not null
        Assert.IsNotNull(merge_base);
        Assert.IsNotNull(merge_edit_all);
        Assert.IsNotNull(merge_add_effects);
        Assert.IsNotNull(merge_minor_tweaks);
        Assert.IsNotNull(MergedDefault.AIPackages);

        // wander packages are merged
        // distance is taken from merge_add_effects
        var distanceMerged = (MergedDefault.AIPackages.First().AIPackage as AI_W)?.Distance;
        var distanceCorrect = (merge_add_effects.First().AIPackage as AI_W)?.Distance;
        Assert.AreEqual(distanceMerged, distanceCorrect);

        // duration is taken from merge_minor_tweaks
        var durationMerged = (MergedDefault.AIPackages.First().AIPackage as AI_W)?.Duration;
        var durationCorrect = (merge_minor_tweaks.First().AIPackage as AI_W)?.Duration;
        Assert.AreEqual(durationMerged, durationCorrect);

        // other packages are taken by load order
        // last esp has the correct amount of packages since no merging is done
        Assert.AreEqual(MergedDefault.AIPackages.Count, merge_minor_tweaks.Count);

        // or inclusively merged
        // TODO tests

        void LogRecordsAIPackages(TES3Lib.Records.NPC_ merged, params string[] plugins)
        {
            foreach (var parent in plugins)
            {
                var plugin = RecordCache[parent];
                _logger.LogInformation("{Plugin} : {Count} ({Parent})", plugin, plugin.AIPackages?.Count, parent);
                LogRecordsEnumerable(plugin.AIPackages?.Select(x => x.AIPackage));
            }
            _logger.LogInformation("{MergedObjectsPluginName} : {Count}", MergedObjectsPluginName, merged.AIPackages?.Count);
            LogRecordsEnumerable(merged.AIPackages?.Select(x => x.AIPackage));
        }
    }
}
