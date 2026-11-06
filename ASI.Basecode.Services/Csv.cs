using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ASI.Basecode.Services
{
    /// <summary>
    /// Minimal RFC 4180 CSV builder. Every field is quoted, quotes are doubled, and values that a
    /// spreadsheet would run as a formula (leading = + - @ tab CR) are prefixed with an apostrophe.
    /// </summary>
    public class Csv
    {
        private static readonly char[] FormulaPrefixes = { '=', '+', '-', '@', '\t', '\r' };
        private readonly StringBuilder _sb = new();

        public Csv(params string[] header)
        {
            this.Row(header);
        }

        public Csv Row(IEnumerable<object> values)
        {
            this._sb.AppendLine(string.Join(",", values.Select(Field)));
            return this;
        }

        public Csv Row(params object[] values) => this.Row((IEnumerable<object>)values);

        public override string ToString() => this._sb.ToString();

        public static string Field(object value)
        {
            var text = value switch
            {
                null => string.Empty,
                System.DateTime dt => dt.ToString("O"),
                _ => value.ToString() ?? string.Empty,
            };

            if (text.Length > 0 && FormulaPrefixes.Contains(text[0]) && value is string)
            {
                text = "'" + text;
            }

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
