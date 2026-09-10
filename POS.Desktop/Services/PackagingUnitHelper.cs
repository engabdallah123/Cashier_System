using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace POS.Desktop.Services
{
    public record PackagingConfig(int PiecesPerBox, int BoxesPerCarton)
    {
        public int TotalPiecesPerCarton => Math.Max(1, PiecesPerBox) * Math.Max(1, BoxesPerCarton);
        public int PiecesPerCarton => TotalPiecesPerCarton;
        public bool HasMultiUnits => PiecesPerBox > 1 || BoxesPerCarton > 1;
        public bool HasCustomUnits => HasMultiUnits;
    }

    public static class PackagingUnitHelper
    {
        public static PackagingConfig Parse(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return new PackagingConfig(1, 1);

            var match = Regex.Match(description, @"\[UNITS:piecesPerBox=(\d+);boxesPerCarton=(\d+)\]");
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out var ppb) && int.TryParse(match.Groups[2].Value, out var bpc))
                {
                    return new PackagingConfig(Math.Max(1, ppb), Math.Max(1, bpc));
                }
            }

            return new PackagingConfig(1, 1);
        }

        public static string Serialize(string? userDescription, PackagingConfig config)
        {
            return Serialize(config.PiecesPerBox, config.BoxesPerCarton, userDescription);
        }

        public static string Serialize(int piecesPerBox, int boxesPerCarton, string? userDescription)
        {
            var cleanDesc = CleanDescription(userDescription);
            var tag = $"[UNITS:piecesPerBox={Math.Max(1, piecesPerBox)};boxesPerCarton={Math.Max(1, boxesPerCarton)}]";
            if (string.IsNullOrWhiteSpace(cleanDesc))
                return tag;

            return $"{cleanDesc} {tag}";
        }

        public static string CleanDescription(string? description)
        {
            if (string.IsNullOrWhiteSpace(description)) return string.Empty;
            return Regex.Replace(description, @"\[UNITS:[^\]]*\]", "").Trim();
        }

        public static string FormatStockBreakdown(decimal totalPieces, string? descriptionOrUnit, string? unitOrDescription = "قطعة")
        {
            string? description = descriptionOrUnit;
            string? unitSymbol = unitOrDescription;

            // Automatically detect if arguments were swapped
            if (!string.IsNullOrEmpty(unitOrDescription) && unitOrDescription.Contains("[UNITS:"))
            {
                description = unitOrDescription;
                unitSymbol = descriptionOrUnit;
            }

            unitSymbol = CleanDescription(unitSymbol);
            if (string.IsNullOrWhiteSpace(unitSymbol)) unitSymbol = "قطعة";

            var config = Parse(description);
            return FormatStockBreakdown(totalPieces, config.PiecesPerBox, config.BoxesPerCarton, unitSymbol);
        }

        public static string FormatStockBreakdown(decimal totalPieces, int piecesPerBox, int boxesPerCarton, string? unitSymbol = "قطعة")
        {
            unitSymbol = CleanDescription(unitSymbol);
            if (string.IsNullOrWhiteSpace(unitSymbol)) unitSymbol = "قطعة";

            var config = new PackagingConfig(piecesPerBox, boxesPerCarton);
            if (!config.HasMultiUnits || totalPieces <= 0)
                return $"{totalPieces:N0} {unitSymbol}";

            var totalInt = (long)Math.Floor(totalPieces);
            var cartonPieces = config.TotalPiecesPerCarton;
            var boxPieces = Math.Max(1, config.PiecesPerBox);

            var cartons = totalInt / cartonPieces;
            var remAfterCartons = totalInt % cartonPieces;
            var boxes = remAfterCartons / boxPieces;
            var pieces = remAfterCartons % boxPieces;

            var parts = new List<string>();
            if (cartons > 0) parts.Add($"{cartons} كرتونة");
            if (boxes > 0) parts.Add($"{boxes} علبة");
            if (pieces > 0) parts.Add($"{pieces} قطعة");

            if (parts.Count == 0)
                return $"{totalPieces:N0} {unitSymbol}";

            return $"{totalPieces:N0} {unitSymbol} ({string.Join(" و ", parts)})";
        }

        public static string FormatTwoTierStockBreakdown(decimal totalPieces, int conversionFactor, string? baseUnit = "قطعة", string? parentUnit = "كرتونة")
        {
            baseUnit = string.IsNullOrWhiteSpace(baseUnit) ? "قطعة" : baseUnit;
            parentUnit = string.IsNullOrWhiteSpace(parentUnit) ? "كرتونة" : parentUnit;

            if (conversionFactor <= 1 || totalPieces <= 0)
                return $"{totalPieces:N0} {baseUnit}";

            var totalInt = (long)Math.Floor(totalPieces);
            var cartons = totalInt / conversionFactor;
            var pieces = totalInt % conversionFactor;

            if (cartons > 0 && pieces > 0)
                return $"{cartons} {parentUnit} + {pieces} {baseUnit} ({totalPieces:N0} {baseUnit})";
            if (cartons > 0)
                return $"{cartons} {parentUnit} ({totalPieces:N0} {baseUnit})";
            return $"{pieces} {baseUnit}";
        }
    }
}
