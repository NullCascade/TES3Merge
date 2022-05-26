using System.Reflection;

namespace TES3Merge.Merger;

internal static class CELL
{
    public static bool Merge(object currentParam, object firstParam, object nextParam)
    {
        // Get the values as their correct type.
        var current = currentParam as TES3Lib.Base.Record ?? throw new ArgumentException("Current record is of incorrect type.");
        var first = firstParam as TES3Lib.Base.Record ?? throw new ArgumentException("First record is of incorrect type.");
        var next = nextParam as TES3Lib.Base.Record ?? throw new ArgumentException("Next record is of incorrect type.");

        // Store modified state.
        var modified = false;

        // Ensure that the record type hasn't changed.
        if (!first.Name.Equals(next.Name))
        {
            throw new Exception("Record types differ!");
        }

        // Cover the base record flags.
        if (current.Flags.SequenceEqual(first.Flags) && !next.Flags.SequenceEqual(first.Flags))
        {
            current.Flags = next.Flags;
            modified = true;
        }

        /*
        
        Cell Name Patch (--cellnames)

        Creates a patch to ensure renamed cells are not accidentally reverted to
        their original name.

        This solves the following plugin conflict that causes bugs:
        * Master A names external CELL (1, 1) as: "".
        * Plugin B renames CELL (1, 1) to: "My City".
        * Plugin C modifies CELL (1, 1), using the original name "", reverting
            renaming done by plugin B.
        * References in plugin B (such as in scripts) that refer to "My City" break.

        This option works by scanning your currently active plugin load order for
        cell name reversions like those in the above example, and ensures whenever
        possible that cell renaming is properly maintained.

        */
        if (current is TES3Lib.Records.CELL cell)
        {
            // only check exterior cells for rename reversion problem
            if (!cell.DATA.Flags.Contains(TES3Lib.Enums.Flags.CellFlag.IsInteriorCell))
            {
                // var nam = cell.NAME.Name;
                //var coords = (cell.DATA.GridX, cell.DATA.GridY);


                //modified = true;
            }
        }



        return modified;
    }

}
