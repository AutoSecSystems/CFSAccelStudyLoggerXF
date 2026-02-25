using System.Linq;
using System.Text;

namespace AccelStudyLoggerXF.MovementTest
{
    public static class MacAddressNormalizer
    {
        public static string NormalizeNoSeparator(string mac)
        {

            if (string.IsNullOrWhiteSpace(mac))
                return string.Empty;

            var trimmedUpper = mac.Trim().ToUpperInvariant();
            var sb = new StringBuilder(12);

            foreach (var c in trimmedUpper)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
            }

            // Canonical DB format: 12 hex chars without separators.
            if (sb.Length == 12)
                return sb.ToString();

            // Fallback: preserve trimmed input in uppercase for debugging/traceability.
            return trimmedUpper;
        }
    }
}
