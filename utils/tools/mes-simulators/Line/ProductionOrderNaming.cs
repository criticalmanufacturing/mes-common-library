using System.Text.RegularExpressions;

namespace MESSimulator.Line
{
    /// <summary>
    /// The naming convention of the simulator's production orders (Line:Order:ProductionOrderNameFormat), e.g.
    /// "PO SC.{id}": <see cref="IdToken"/> becomes 8 random uppercase hex digits. The same format names new orders and
    /// decides which orders (and their lots) the simulator may terminate: only names that match it exactly.
    /// </summary>
    public static class ProductionOrderNaming
    {
        public const string IdToken = "{id}";

        /// <summary>Text before <see cref="IdToken"/> must be at least this long, so the MES query stays narrow.</summary>
        private const int minPrefixLength = 2;

        /// <summary>A new order name: the format with <see cref="IdToken"/> replaced by 8 random uppercase hex digits.</summary>
        public static string NewName(string format) =>
            format.Replace(IdToken, Guid.NewGuid().ToString("N")[..8].ToUpperInvariant());

        /// <summary>Whether <paramref name="orderName"/> is a name <see cref="NewName"/> gives for this format.</summary>
        public static bool Matches(string? orderName, string format) =>
            orderName != null && Regex.IsMatch(orderName, Pattern(format));

        /// <summary>The fixed text before <see cref="IdToken"/>: what the MES is queried with (StartsWith).</summary>
        public static string QueryPrefix(string format) => format[..format.IndexOf(IdToken, StringComparison.Ordinal)];

        /// <summary>Why the format can't be used, or null when it can.</summary>
        public static string? Validate(string? format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return "Line:Order:ProductionOrderNameFormat is required (e.g. \"PO SIM.{id}\").";
            }

            int first = format.IndexOf(IdToken, StringComparison.Ordinal);
            if (first < 0 || first != format.LastIndexOf(IdToken, StringComparison.Ordinal))
            {
                return $"Line:Order:ProductionOrderNameFormat '{format}' must contain {IdToken} exactly once.";
            }

            if (format[..first].Trim().Length < minPrefixLength)
            {
                return $"Line:Order:ProductionOrderNameFormat '{format}' must start with at least {minPrefixLength} characters before {IdToken}, so only the simulator's orders are matched.";
            }

            return null;
        }

        private static string Pattern(string format)
        {
            int index = format.IndexOf(IdToken, StringComparison.Ordinal);
            return $"^{Regex.Escape(format[..index])}[0-9A-F]{{8}}{Regex.Escape(format[(index + IdToken.Length)..])}$";
        }
    }
}
