using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AccelStudyLoggerXF
{
    public class SessionMeta
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public string ContainerNo { get; set; } = "";
        public string Location { get; set; } = "";
        public string Notes { get; set; } = "";
        public string TagMac { get; set; } = "";
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    }

    public class SessionRow
    {
        public long TimestampMs { get; set; }
        public string Mac { get; set; } = "";
        public int Rssi { get; set; }
        public string AdvHex { get; set; } = "";
        public string EventLabel { get; set; } = "";

        public int? BatteryMv { get; set; }
        public double? TempC { get; set; }
        public double? Humidity { get; set; }
        public short? AxMg { get; set; }
        public short? AyMg { get; set; }
        public short? AzMg { get; set; }
    }

    public class CsvSessionLogger
    {
        private readonly object _lock = new object();
        private readonly List<SessionRow> _rows = new List<SessionRow>();
        public SessionMeta Meta { get; }

        public CsvSessionLogger(SessionMeta meta) => Meta = meta;

        public void AddPacket(BleAdvPacket p, KSensorData ks)
        {
            lock (_lock)
            {
                _rows.Add(new SessionRow
                {
                    TimestampMs = p.TimestampMs,
                    Mac = p.Mac,
                    Rssi = p.Rssi,
                    AdvHex = ToHex(p.RawScanRecord),
                    EventLabel = "",

                    BatteryMv = ks?.BatteryMv,
                    TempC = ks?.TemperatureC,
                    Humidity = ks?.Humidity,
                    AxMg = ks?.AxMg,
                    AyMg = ks?.AyMg,
                    AzMg = ks?.AzMg
                });
            }
        }


        public void MarkEvent(string label)
        {
            lock (_lock)
            {
                _rows.Add(new SessionRow
                {
                    TimestampMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Mac = Meta.TagMac,
                    Rssi = 0,
                    AdvHex = "",
                    EventLabel = label
                });
            }
        }

        public string BuildCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("session_id,container_no,location,notes,tag_mac,timestamp_ms,mac,rssi,adv_hex,event_label,battery_mv,temp_c,humidity,ax_mg,ay_mg,az_mg");


            List<SessionRow> snapshot;
            lock (_lock) snapshot = _rows.ToList();

            foreach (var r in snapshot)
            {
                sb.Append(E(Meta.SessionId)).Append(',')
                  .Append(E(Meta.ContainerNo)).Append(',')
                  .Append(E(Meta.Location)).Append(',')
                  .Append(E(Meta.Notes)).Append(',')
                  .Append(E(Meta.TagMac)).Append(',')
                  .Append(r.TimestampMs).Append(',')
                  .Append(E(r.Mac)).Append(',')
                  .Append(r.Rssi).Append(',')
                  .Append(E(r.AdvHex)).Append(',')
                  .Append(E(r.EventLabel)).Append(',')
  .Append(E(r.BatteryMv?.ToString() ?? "")).Append(',')
  .Append(E(r.TempC?.ToString() ?? "")).Append(',')
  .Append(E(r.Humidity?.ToString() ?? "")).Append(',')
  .Append(E(r.AxMg?.ToString() ?? "")).Append(',')
  .Append(E(r.AyMg?.ToString() ?? "")).Append(',')
  .Append(E(r.AzMg?.ToString() ?? ""))
  .AppendLine();

            }
            return sb.ToString();
        }

        static string E(string s)
            => (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                ? $"\"{s.Replace("\"", "\"\"")}\""
                : s;

        static string ToHex(byte[] bytes)
        {
            const string hex = "0123456789ABCDEF";
            var c = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                c[i * 2] = hex[(bytes[i] >> 4) & 0xF];
                c[i * 2 + 1] = hex[bytes[i] & 0xF];
            }
            return new string(c);
        }
    }
}
