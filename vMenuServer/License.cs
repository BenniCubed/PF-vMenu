using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using vMenuShared;

namespace vMenuServer
{
    public class License
    {
        public string Unprefixed { get; private set; }
        public string Prefixed => $"{LicensePrefix}{Unprefixed}";

        public License(string license, bool? expectHasPrefix = null)
        {
            if (string.IsNullOrWhiteSpace(license))
            {
                throw new ArgumentException("License cannot be null or empty.", nameof(license));
            }

            license = license.ToLower();

            var hasPrefix = license.StartsWith(LicensePrefix);
            Unprefixed = hasPrefix ? license.Substring(LicensePrefix.Length) : license;

            var nonHexDigitCount = Unprefixed
                .Where(c => !isHexDigit(c))
                .Count();
            if (nonHexDigitCount > 0 || Unprefixed.Length != 40)
            {
                throw new ArgumentException($"License must be a 40-character hexadecimal string, optionally prefixed with '{LicensePrefix}', but was '{license}'.", nameof(license));
            }

            if (expectHasPrefix is bool expectHasPrefixVal && expectHasPrefixVal != hasPrefix)
            {
                throw new ArgumentException($"Expected license prefix = {expectHasPrefixVal}, but has prefix = {hasPrefix}.", nameof(license));
            }
        }

        private static bool isHexDigit(char c)
        {
            return char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private const string LicensePrefix = "license:";
    }
}
