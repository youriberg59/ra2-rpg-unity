using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RA2RPG.RA2
{
    public sealed class IniDocument
    {
        private readonly Dictionary<string, Dictionary<string, string>> sections =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> SectionNames => sections.Keys;

        public static IniDocument Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            string text = Encoding.UTF8.GetString(data);
            return Parse(text);
        }

        public static IniDocument Parse(string text)
        {
            var doc = new IniDocument();
            string currentSection = null;

            using var reader = new StringReader(text ?? string.Empty);
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith(";"))
                    continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    doc.GetOrCreateSection(currentSection);
                    continue;
                }

                int eq = trimmed.IndexOf('=');
                if (eq <= 0 || currentSection == null)
                    continue;

                string key = trimmed.Substring(0, eq).Trim();
                string value = trimmed.Substring(eq + 1).Trim();

                int comment = value.IndexOf(';');
                if (comment >= 0)
                    value = value.Substring(0, comment).Trim();

                doc.GetOrCreateSection(currentSection)[key] = value;
            }

            return doc;
        }

        public bool HasSection(string section) => sections.ContainsKey(section);

        public string Get(string section, string key, string fallback = null)
        {
            if (!sections.TryGetValue(section, out var values))
                return fallback;

            return values.TryGetValue(key, out string value) ? value : fallback;
        }

        public IReadOnlyDictionary<string, string> GetSection(string section)
        {
            return sections.TryGetValue(section, out var values) ? values : null;
        }

        private Dictionary<string, string> GetOrCreateSection(string name)
        {
            if (!sections.TryGetValue(name, out var values))
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[name] = values;
            }

            return values;
        }
    }
}
