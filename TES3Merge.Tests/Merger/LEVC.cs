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
public class LEVC : RecordTest<TES3Lib.Records.LEVC>
{
    internal TES3Lib.Records.LEVC Merged__aa_sitters_bardrink_telmora;

    private static readonly string[] FriendsAndFoesMasters = new string[] { "F&F_base.esm", "F&F_NoSitters.ESP", "F&F_scarce.ESP" };

    public LEVC()
    {
        Merged__aa_sitters_bardrink_telmora = CreateMergedRecord("_aa_sitters_bardrink_telmora", FriendsAndFoesMasters);

        _logger = _host.Services.GetRequiredService<ILogger<CREA>>();
    }

    [TestMethod]
    public void EditorId()
    {
        LogRecords("NAME.EditorId", Merged__aa_sitters_bardrink_telmora, FriendsAndFoesMasters);

        Assert.AreEqual("_aa_sitters_bardrink_telmora\0", Merged__aa_sitters_bardrink_telmora.NAME.EditorId);
    }
}
