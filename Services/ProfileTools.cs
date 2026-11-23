using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using XMRiGUI.Models;

namespace XMRiGUI.Services
{
    internal static class ProfileTools
    {
        public static bool ValidPool(string value)
        {
            var pool = value?.Trim() ?? "";
            if (!pool.Contains("://")) pool = "stratum+tcp://" + pool;
            return !pool.Any(char.IsWhiteSpace) && !pool.Contains('\\') &&
                Regex.IsMatch(pool, @"^stratum\+(?:tcp|ssl)://(?:\[[^\]]+\]|[^:/@?#]+):[0-9]+/?$", RegexOptions.IgnoreCase) &&
                Uri.TryCreate(pool, UriKind.Absolute, out var uri) && uri.HostNameType != UriHostNameType.Unknown &&
                uri.Port is > 0 and <= 65535;
        }

        public static List<string> SplitArguments(string input)
        {
            var result = new List<string>();
            var token = new StringBuilder();
            char quote = '\0';
            bool started = false;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '\\' && quote != '\'')
                {
                    if (++i == input.Length) throw new FormatException("Incomplete escape.");
                    if (quote == '"' && input[i] != '"' && input[i] != '\\') token.Append(c);
                    token.Append(input[i]);
                }
                else if (c == quote && quote != '\0') quote = '\0';
                else if (quote == '\0' && (c == '\'' || c == '"')) quote = c;
                else if (quote == '\0' && char.IsWhiteSpace(c))
                {
                    if (started) result.Add(token.ToString());
                    token.Clear();
                    started = false;
                    continue;
                }
                else token.Append(c);
                started = true;
            }
            if (quote != '\0') throw new FormatException("Unclosed quote.");
            if (started) result.Add(token.ToString());
            return result;
        }

        public static IEnumerable<string> Secrets(ProfileConfig config)
        {
            var args = config.args ?? "";
            var values = new List<string> { config.pool, config.pool?.Trim(), config.wallet, config.password, args };
            IEnumerable<string> tokens;
            try { tokens = SplitArguments(args); }
            catch (FormatException) { tokens = Regex.Split(args, @"\s+"); }
            foreach (var token in tokens)
            {
                values.Add(token.Trim('"', '\''));
                int equals = token.IndexOf('=');
                if (equals >= 0) values.Add(token.Substring(equals + 1).Trim('"', '\''));
            }
            return values.Where(value => !string.IsNullOrEmpty(value));
        }

        public static string Redact(string text, IEnumerable<string> secrets)
        {
            text = Regex.Replace(text, "(?<![\\w-])(--(?:user|pass|url)|-[upo])(?:\\s*=\\s*|\\s+)(\"[^\"\\r\\n]*\"|'[^'\\r\\n]*'|[^\\s]+)", "$1=[REDACTED]", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b[48][1-9A-HJ-NP-Za-km-z]{94}(?:[1-9A-HJ-NP-Za-km-z]{11})?\b", "[REDACTED]");
            var pattern = string.Join("|", secrets.Distinct().OrderByDescending(s => s.Length).Select(Regex.Escape));
            return pattern.Length == 0 ? text : Regex.Replace(text, pattern, "[REDACTED]");
        }
    }
}
