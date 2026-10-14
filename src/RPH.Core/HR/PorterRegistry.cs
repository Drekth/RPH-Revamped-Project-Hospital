using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RPH.Core.HR
{
    /// <summary>
    /// The employees that are porters, by entity ID. A porter is an ordinary nurse for the game and its save: this list is what
    /// makes him one, and lives in a sidecar file next to the save, so removing the mod leaves the save loadable.
    /// <code>
    /// RPH-PORTERS  1
    /// P  entityId
    /// </code>
    /// </summary>
    public sealed class PorterRegistry
    {
        private const string Header = "RPH-PORTERS";
        private const int FormatVersion = 1;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly HashSet<uint> m_ids = new HashSet<uint>();

        public int Count
        {
            get { return m_ids.Count; }
        }

        public IEnumerable<uint> Ids
        {
            get { return m_ids; }
        }

        public bool Contains(uint id)
        {
            return m_ids.Contains(id);
        }

        public void Add(uint id)
        {
            m_ids.Add(id);
        }

        public void Remove(uint id)
        {
            m_ids.Remove(id);
        }

        /// <summary>Forgets the IDs for which <paramref name="exists"/> is false (fired and gone, or from another save).</summary>
        public void KeepOnly(Func<uint, bool> exists)
        {
            m_ids.RemoveWhere(id => !exists(id));
        }

        public string WriteToString()
        {
            var builder = new StringBuilder();
            builder.Append(Header).Append('\t').Append(FormatVersion.ToString(Inv)).Append('\n');
            var sorted = new List<uint>(m_ids);
            sorted.Sort();
            foreach (uint id in sorted)
            {
                builder.Append("P\t").Append(id.ToString(Inv)).Append('\n');
            }
            return builder.ToString();
        }

        /// <exception cref="FormatException">The text is not a porters file, or a newer format.</exception>
        public static PorterRegistry ReadFromString(string text)
        {
            using (var reader = new StringReader(text))
            {
                string first = reader.ReadLine();
                string[] header = first != null ? first.Split('\t') : new string[0];
                int version;
                if (header.Length < 2 || header[0] != Header || !int.TryParse(header[1], NumberStyles.Integer, Inv, out version))
                {
                    throw new FormatException("Not an RPH porters file.");
                }
                if (version > FormatVersion)
                {
                    throw new FormatException("Porters format " + version + " is newer than this mod (" + FormatVersion + ").");
                }
                var registry = new PorterRegistry();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] f = line.Split('\t');
                    uint id;
                    if (f.Length >= 2 && f[0] == "P" && uint.TryParse(f[1], NumberStyles.Integer, Inv, out id))
                    {
                        registry.Add(id);
                    }
                }
                return registry;
            }
        }
    }
}
