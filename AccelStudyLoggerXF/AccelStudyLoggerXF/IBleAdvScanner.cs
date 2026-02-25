using System;

namespace AccelStudyLoggerXF
{
    public interface IBleAdvScanner
    {
        bool IsSupported();
        void StartScan(Action<BleAdvPacket> onPacket);
        void StopScan();
    }

    public class BleAdvPacket
    {
        public long TimestampMs { get; set; }
        public string Mac { get; set; } = "";
        public int Rssi { get; set; }
        public byte[] RawScanRecord { get; set; } = Array.Empty<byte>();
    }
}
