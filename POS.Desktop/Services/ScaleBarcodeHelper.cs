using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Desktop.Services
{
    public sealed record ScaleBarcodeResult(
        bool IsScaleBarcode,
        string PluCode,
        decimal WeightInKg,
        List<string> CandidateCodes,
        string? RawBarcode = null,
        string? ErrorMessage = null);

    /// <summary>
    /// مفسر باركود ميزان الأوزان الإلكتروني (EAN-13 Scale Barcode Parser)
    /// يدعم موازين السوبر ماركت الشائعة (Rongta, CAS, Dibal, Bizerba, Aclas, Digi)
    /// نمط الباركود القياسي (13 خانة):
    /// [20 / 21 / 99] + [كود الصنف PLU: 4 أو 5 أرقام] + [الوزن بالجرام: 5 أرقام] + [Check Digit: خانة]
    /// </summary>
    public static class ScaleBarcodeHelper
    {
        private static readonly HashSet<string> KnownScalePrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "99"
        };

        /// <summary>
        /// يتحقق ما إذا كان الباركود يطابق مواصفات باركود ميزان الأوزان
        /// </summary>
        public static bool IsScaleBarcode(string? barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return false;

            var clean = barcode.Trim();
            if (clean.Length != 13 || !clean.All(char.IsDigit))
                return false;

            var prefix = clean.Substring(0, 2);
            return KnownScalePrefixes.Contains(prefix);
        }

        /// <summary>
        /// يفك شفرة باركود الميزان ويستخرج كود الصنف (PLU) والوزن بالكيلوجرام وقائمة الأكواد المرشحة للبحث
        /// </summary>
        public static ScaleBarcodeResult Parse(string? barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return new ScaleBarcodeResult(false, string.Empty, 0, new List<string>(), barcode, "الباركود فارغ");

            var clean = barcode.Trim();
            if (!IsScaleBarcode(clean))
                return new ScaleBarcodeResult(false, string.Empty, 0, new List<string>(), clean, "ليس باركود ميزان قياسي (EAN-13)");

            var prefix = clean.Substring(0, 2);

            // النمط الأول الأكثر انتشاراً: بادئة رقمين + كود صنف 5 أرقام + وزن 5 أرقام (جرام) + خانة تحقق
            // مثال: 20 00101 00350 4 -> كود 101 و وزن 350 جرام (0.350 كجم)
            var plu5 = clean.Substring(2, 5);
            var weightPart5 = clean.Substring(7, 5);

            // النمط الثاني: بادئة رقمين + كود صنف 4 أرقام + وزن 5 أرقام + خانتين
            // مثال: 20 0101 00350 X -> كود 101 و وزن 350 جرام
            var plu4 = clean.Substring(2, 4);
            var weightPart4 = clean.Substring(6, 5);

            decimal weightInKg = 0;
            if (int.TryParse(weightPart5, out int grams5) && grams5 > 0)
            {
                weightInKg = grams5 / 1000m;
            }
            else if (int.TryParse(weightPart4, out int grams4) && grams4 > 0)
            {
                weightInKg = grams4 / 1000m;
            }

            if (weightInKg <= 0)
            {
                weightInKg = 1m; // قيمة افتراضية لتفادي القسمة على صفر أو الخطأ
            }

            // تجهيز كافة الاحتمالات الممكنة لتطابق كود الصنف في قاعدة البيانات:
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // كود الـ 5 خانات بشكله الخام ومجرد من الأصفار
            candidates.Add(plu5);
            var trimmed5 = plu5.TrimStart('0');
            if (!string.IsNullOrEmpty(trimmed5))
                candidates.Add(trimmed5);

            // كود الـ 4 خانات بشكله الخام ومجرد من الأصفار
            candidates.Add(plu4);
            var trimmed4 = plu4.TrimStart('0');
            if (!string.IsNullOrEmpty(trimmed4))
                candidates.Add(trimmed4);

            // الباركود المدمج بالبادئة (في حال قام المستخدم بحفظ الصنف كـ 200101 أو 2000101)
            candidates.Add(prefix + plu4);
            candidates.Add(prefix + plu5);
            if (!string.IsNullOrEmpty(trimmed4))
                candidates.Add(prefix + trimmed4);

            // الكود الأصلي بالكامل
            candidates.Add(clean);

            var primaryPlu = !string.IsNullOrEmpty(trimmed5) ? trimmed5 : (!string.IsNullOrEmpty(trimmed4) ? trimmed4 : plu5);

            return new ScaleBarcodeResult(
                IsScaleBarcode: true,
                PluCode: primaryPlu,
                WeightInKg: weightInKg,
                CandidateCodes: candidates.ToList(),
                RawBarcode: clean);
        }

        /// <summary>
        /// يولد كود صنف ميزان (PLU) منسق تلقائياً، مثلاً "0101" أو "200101"
        /// </summary>
        public static string FormatPluCode(int pluNumber, bool includeScalePrefix = false, string prefix = "20")
        {
            var padded = pluNumber.ToString("D4"); // 0101
            return includeScalePrefix ? $"{prefix}{padded}" : padded;
        }
    }
}
