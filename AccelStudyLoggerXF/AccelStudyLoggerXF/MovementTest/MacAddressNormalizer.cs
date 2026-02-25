using System.Linq;

namespace AccelStudyLoggerXF.MovementTest
{
    public static class MacAddressNormalizer
    {
        public static string NormalizeNoSeparator(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return string.Empty;
            var chars = mac.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars).ToUpperInvariant();
        }
    }
}
