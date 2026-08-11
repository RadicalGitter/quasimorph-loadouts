using System.Collections.Generic;
using System.Linq;

namespace QuasimorphLoadouts
{
    internal sealed class OperationResult
    {
        internal int EquipmentChanged { get; set; }
        internal int QuantityMoved { get; set; }
        internal List<string> Problems { get; } = new List<string>();

        internal string ToDisplayText()
        {
            string summary = $"Loadout applied: {EquipmentChanged} equipment change(s), {QuantityMoved} item(s) moved.";
            if (Problems.Count == 0)
            {
                return summary + "\n\nEverything in the preset was available.";
            }

            return summary + "\n\nCould not complete:\n" + string.Join("\n", Problems.Select(problem => "• " + problem).ToArray());
        }
    }
}
