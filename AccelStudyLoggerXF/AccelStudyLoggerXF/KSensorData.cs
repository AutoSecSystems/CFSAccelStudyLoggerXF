using System;

namespace AccelStudyLoggerXF
{
    public class KSensorData
    {
        public ushort ServiceUuid { get; set; }          // actual on-air bytes AA FE => UUID FEAA
        public byte FrameType { get; set; }              // 0x21
        public ushort SensorMask { get; set; }

        public ushort? BatteryMv { get; set; }           // bit0
        public double? TemperatureC { get; set; }        // bit1 (Fixed 8.8)
        public double? Humidity { get; set; }            // bit2 (Fixed 8.8)
        public short? AxMg { get; set; }                 // bit3
        public short? AyMg { get; set; }
        public short? AzMg { get; set; }
    }

    public static class KSensorParser
    {
        public static bool TryParseFromScanRecord(byte[] scanRecord, out KSensorData data)
        {
            data = null;
            if (scanRecord == null || scanRecord.Length < 2) return false;

            int i = 0;
            while (i < scanRecord.Length)
            {
                int len = scanRecord[i] & 0xFF;
                if (len == 0) break;

                int typeIndex = i + 1;
                int dataIndex = i + 2;
                int dataLen = len - 1;

                if (typeIndex >= scanRecord.Length) break;
                if (dataIndex + dataLen > scanRecord.Length) break;

                byte adType = scanRecord[typeIndex];

                // Service Data - 16-bit UUID (0x16)
                if (adType == 0x16 && dataLen >= 3)
                {
                    // UUID bytes are little-endian in BLE advertisements.
                    // Example bytes: AA FE => uuid = 0xFEAA
                    ushort uuid = ReadU16LE(scanRecord, dataIndex);

                    if (uuid == 0xFEAA)
                    {
                        int p = dataIndex + 2;
                        byte frameType = scanRecord[p++];

                        if (frameType == 0x21)
                        {
                            if (p + 2 > dataIndex + dataLen) return false;

                            // ✅ CHANGE #1: mask is BIG-ENDIAN for this device payload
                            ushort mask = ReadU16BE(scanRecord, p);
                            p += 2;

                            var d = new KSensorData
                            {
                                ServiceUuid = uuid,
                                FrameType = frameType,
                                SensorMask = mask
                            };

                            int end = dataIndex + dataLen;

                            // bit0: voltage (2 bytes, mV) - ✅ BE
                            if ((mask & (1 << 0)) != 0)
                            {
                                if (!TryReadU16BE(scanRecord, ref p, end, out var mv)) return false;
                                d.BatteryMv = mv;
                            }

                            // bit1: temperature (2 bytes, Fixed 8.8 signed) - ✅ BE
                            if ((mask & (1 << 1)) != 0)
                            {
                                if (!TryReadI16BE(scanRecord, ref p, end, out var rawT)) return false;
                                d.TemperatureC = Fixed88ToDouble(rawT);
                            }

                            // bit2: humidity (2 bytes, Fixed 8.8 signed) - ✅ BE
                            if ((mask & (1 << 2)) != 0)
                            {
                                if (!TryReadI16BE(scanRecord, ref p, end, out var rawH)) return false;
                                d.Humidity = Fixed88ToDouble(rawH);
                            }

                            // bit3: acc X,Y,Z (mg) - ✅ BE
                            if ((mask & (1 << 3)) != 0)
                            {
                                if (!TryReadI16BE(scanRecord, ref p, end, out var ax)) return false;
                                if (!TryReadI16BE(scanRecord, ref p, end, out var ay)) return false;
                                if (!TryReadI16BE(scanRecord, ref p, end, out var az)) return false;

                                d.AxMg = ax;
                                d.AyMg = ay;
                                d.AzMg = az;
                            }

                            data = d;
                            return true;
                        }
                    }
                }

                i += (len + 1);
            }

            return false;
        }

        // UUID is LE (keep this)
        static ushort ReadU16LE(byte[] b, int idx)
            => (ushort)(b[idx] | (b[idx + 1] << 8));

        // ✅ ADD: BE helpers for mask + all sensor fields
        static ushort ReadU16BE(byte[] b, int idx)
            => (ushort)((b[idx] << 8) | b[idx + 1]);

        static short ReadI16BE(byte[] b, int idx)
            => unchecked((short)ReadU16BE(b, idx));

        // ✅ CHANGE #2: Replace TryReadU16 / TryReadI16 with BE versions
        static bool TryReadU16BE(byte[] b, ref int p, int end, out ushort v)
        {
            v = 0;
            if (p + 2 > end) return false;
            v = ReadU16BE(b, p);
            p += 2;
            return true;
        }

        static bool TryReadI16BE(byte[] b, ref int p, int end, out short v)
        {
            v = 0;
            if (p + 2 > end) return false;
            v = ReadI16BE(b, p);
            p += 2;
            return true;
        }

        static double Fixed88ToDouble(short raw) => Math.Round(raw / 256.0, 2);
    }
}
