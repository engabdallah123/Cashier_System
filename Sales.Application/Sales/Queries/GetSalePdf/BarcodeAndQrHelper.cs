using System.Text;
using QRCoder;

namespace Sales.Application.Sales.Queries.GetSalePdf
{
    public static class BarcodeAndQrHelper
    {
        /// <summary>
        /// Generates a PNG QR Code as byte array using QRCoder.
        /// </summary>
        public static byte[]? GenerateQrCodePng(string content, int pixelsPerModule = 10)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            try
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(content.Trim(), QRCodeGenerator.ECCLevel.M);
                var qrCode = new PngByteQRCode(data);
                return qrCode.GetGraphic(pixelsPerModule);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Generates a Code 128 B barcode as clean SVG markup.
        /// </summary>
        public static string? GenerateCode128Svg(string content, int height = 40, int moduleWidth = 2)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            try
            {
                var trimmed = content.Trim();
                // Filter only valid ASCII characters for Code 128 B (32 to 126)
                var validChars = new List<int>();
                foreach (var c in trimmed)
                {
                    if (c >= 32 && c <= 126)
                        validChars.Add(c - 32);
                }

                if (validChars.Count == 0) return null;

                // Start with Start Code B (index 104)
                var patterns = new List<string>();
                patterns.Add(Code128Patterns[104]);

                int checksum = 104;
                for (int i = 0; i < validChars.Count; i++)
                {
                    int val = validChars[i];
                    patterns.Add(Code128Patterns[val]);
                    checksum += (i + 1) * val;
                }

                int checkCode = checksum % 103;
                patterns.Add(Code128Patterns[checkCode]);

                // Stop code (index 106)
                patterns.Add(Code128Patterns[106]);

                // Calculate total modules (width)
                int quietZone = 10 * moduleWidth;
                int currentX = quietZone;

                var rects = new StringBuilder();

                foreach (var pattern in patterns)
                {
                    bool isBar = true;
                    for (int p = 0; p < pattern.Length; p++)
                    {
                        int width = (pattern[p] - '0') * moduleWidth;
                        if (isBar)
                        {
                            rects.Append($"<rect x=\"{currentX}\" y=\"0\" width=\"{width}\" height=\"{height}\" fill=\"black\"/>");
                        }
                        currentX += width;
                        isBar = !isBar;
                    }
                }

                int totalWidth = currentX + quietZone;

                return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {totalWidth} {height}\" width=\"100%\" height=\"{height}\" preserveAspectRatio=\"none\">{rects}</svg>";
            }
            catch
            {
                return null;
            }
        }

        private static readonly string[] Code128Patterns = new string[]
        {
            "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213", // 0-9
            "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132", // 10-19
            "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211", // 20-29
            "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313", // 30-39
            "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331", // 40-49
            "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111", // 50-59
            "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214", // 60-69
            "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111", // 70-79
            "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141", // 80-89
            "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141", // 90-99
            "114131", "311141", "411131", "211412", "211214", "211232", "2331112" // 100-106
        };
    }
}
