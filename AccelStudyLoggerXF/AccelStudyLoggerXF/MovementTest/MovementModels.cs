using System;

namespace AccelStudyLoggerXF.MovementTest
{
    public class TagAdvertisementRecord
    {
        public string TagMac { get; set; } = string.Empty;
        public string GatewayMac { get; set; } = string.Empty;
        public DateTime EventTimeUtc { get; set; }
        public int Rssi { get; set; }
        public int AccX_mg { get; set; }
        public int AccY_mg { get; set; }
        public int AccZ_mg { get; set; }
        public int ObjType { get; set; } = 1;
    }

    public class InternalMoveFromSensorRequest
    {
        public string EventKey { get; set; } = string.Empty;
        public string TagMac { get; set; } = string.Empty;
        public string GatewayMac { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int PeakDelta { get; set; }
        public int Duration250Ms { get; set; }
        public int Duration350Ms { get; set; }
        public int PostShift { get; set; }
        public string Classification { get; set; } = string.Empty;
    }

    public enum MovementClassification
    {
        REAL_MOVE,
        NOT_REAL
    }

    public class MovementDetectionResult
    {
        public MovementClassification Classification { get; set; }
        public string TagMac { get; set; } = string.Empty;
        public string GatewayMac { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int PeakDelta { get; set; }
        public int Duration250Ms { get; set; }
        public int Duration350Ms { get; set; }
        public int PostShift { get; set; }
        public string EventKey { get; set; } = string.Empty;
        public InternalMoveFromSensorRequest ToRequest()
        {
            return new InternalMoveFromSensorRequest
            {
                EventKey = EventKey,
                TagMac = TagMac,
                GatewayMac = GatewayMac,
                StartUtc = StartUtc,
                EndUtc = EndUtc,
                PeakDelta = PeakDelta,
                Duration250Ms = Duration250Ms,
                Duration350Ms = Duration350Ms,
                PostShift = PostShift,
                Classification = Classification.ToString()
            };
        }
    }
}
