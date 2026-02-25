using System;
using System.Security.Cryptography;
using System.Text;

namespace AccelStudyLoggerXF.MovementTest
{
    public class EventKeyGenerator
    {
        public string Generate(string tagMac, string gatewayMac, DateTime startUtc, DateTime endUtc)
        {
            var normalizedTag = MacAddressNormalizer.NormalizeNoSeparator(tagMac);
            var normalizedGateway = MacAddressNormalizer.NormalizeNoSeparator(gatewayMac);
            var payload = $"{normalizedTag}|{normalizedGateway}|{startUtc:O}|{endUtc:O}";
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }

                return sb.ToString().ToUpperInvariant();
            }
        }
    }
}
