using System;
using System.Text;

namespace S1FuelMod.Networking
{
    /// <summary>
    /// Minimal base message with JSON string payload for simplicity.
    /// We implement manual JSON to avoid extra dependencies.
    /// </summary>
    internal abstract class MiniP2PMessage
    {
        public abstract string MessageType { get; }

        public abstract string SerializeJson();
        public abstract void DeserializeJson(string json);

        protected static string Escape(string s)
        {
            return (s ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n");
        }

        protected static string Extract(string json, string key)
        {
            try
            {
                string needle = "\"" + key + "\":";
                int idx = json.IndexOf(needle, StringComparison.Ordinal);
                if (idx < 0) return string.Empty;
                idx += needle.Length;
                // skip spaces
                while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
                if (idx >= json.Length) return string.Empty;
                if (json[idx] == '"')
                {
                    idx++;
                    int end = idx;
                    bool esc = false;
                    for (; end < json.Length; end++)
                    {
                        char c = json[end];
                        if (c == '\\' && !esc) { esc = true; continue; }
                        if (c == '"' && !esc) break;
                        esc = false;
                    }
                    if (end >= json.Length) return string.Empty;
                    string raw = json.Substring(idx, end - idx);
                    return raw
                        .Replace("\\\"", "\"")
                        .Replace("\\\\", "\\")
                        .Replace("\\n", "\n");
                }
                else
                {
                    int end = idx;
                    while (end < json.Length && json[end] != ',' && json[end] != '}' && !char.IsWhiteSpace(json[end])) end++;
                    return json.Substring(idx, end - idx);
                }
            }
            catch { }
            return string.Empty;
        }
    }

    internal sealed class FuelUpdateMessage : MiniP2PMessage
    {
        public const string TYPE = "FUEL_UPDATE";
        public override string MessageType => TYPE;

        public string VehicleGuid = string.Empty;
        public float FuelLevel;
        public float MaxCapacity;

        public override string SerializeJson()
        {
            return "{" +
                   $"\"VehicleGuid\":\"{Escape(VehicleGuid)}\"," +
                   $"\"FuelLevel\":{FuelLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                   $"\"MaxCapacity\":{MaxCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                   "}";
        }

        public override void DeserializeJson(string json)
        {
            VehicleGuid = Extract(json, "VehicleGuid");
            float.TryParse(Extract(json, "FuelLevel"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out FuelLevel);
            float.TryParse(Extract(json, "MaxCapacity"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out MaxCapacity);
        }

        internal struct Item
        {
            public string VehicleGuid;
            public float FuelLevel;
            public float MaxCapacity;
        }
    }

    internal sealed class FuelSnapshotMessage : MiniP2PMessage
    {
        public const string TYPE = "FUEL_SNAPSHOT";
        public override string MessageType => TYPE;

        public FuelUpdateMessage.Item[] Items = Array.Empty<FuelUpdateMessage.Item>();

        public override string SerializeJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"Items\":[");
            for (int i = 0; i < Items.Length; i++)
            {
                var it = Items[i];
                sb.Append("{");
                sb.Append("\"VehicleGuid\":\"").Append(Escape(it.VehicleGuid)).Append("\",");
                sb.Append("\"FuelLevel\":").Append(it.FuelLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",");
                sb.Append("\"MaxCapacity\":").Append(it.MaxCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append("}");
                if (i < Items.Length - 1) sb.Append(",");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public override void DeserializeJson(string json)
        {
            // Minimal parser: split on { .. } items inside Items array
            int arrStart = json.IndexOf("[", StringComparison.Ordinal);
            int arrEnd = json.LastIndexOf("]", StringComparison.Ordinal);
            if (arrStart < 0 || arrEnd <= arrStart)
            {
                Items = Array.Empty<FuelUpdateMessage.Item>();
                return;
            }
            string arr = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            var chunks = SplitTopLevelObjects(arr);
            var list = new System.Collections.Generic.List<FuelUpdateMessage.Item>(chunks.Length);
            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk)) continue;
                var item = new FuelUpdateMessage.Item
                {
                    VehicleGuid = Extract(chunk, "VehicleGuid")
                };
                float.TryParse(Extract(chunk, "FuelLevel"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out item.FuelLevel);
                float.TryParse(Extract(chunk, "MaxCapacity"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out item.MaxCapacity);
                list.Add(item);
            }
            Items = list.ToArray();
        }

        private static string[] SplitTopLevelObjects(string s)
        {
            var parts = new System.Collections.Generic.List<string>();
            int depth = 0; int start = -1;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '{') { if (depth == 0) start = i; depth++; }
                else if (c == '}') { depth--; if (depth == 0 && start >= 0) { parts.Add(s.Substring(start, i - start + 1)); start = -1; } }
            }
            return parts.ToArray();
        }
    }

    internal sealed class FuelSnapshotRequestMessage : MiniP2PMessage
    {
        public const string TYPE = "FUEL_SNAPSHOT_REQ";
        public override string MessageType => TYPE;
        public override string SerializeJson() => "{}";
        public override void DeserializeJson(string json) {}
    }
}


