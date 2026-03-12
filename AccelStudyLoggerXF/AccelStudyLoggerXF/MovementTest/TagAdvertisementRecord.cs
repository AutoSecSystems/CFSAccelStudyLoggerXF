using System;

namespace AccelStudyLoggerXF.MovementTest
{
    public class TagAdvertisementRecord
    {
        public string TagMac { get; set; } = string.Empty;
        public DateTime EventTimeUtc { get; set; }
        public int Rssi { get; set; }
        public int AccX_mg { get; set; }
        public int AccY_mg { get; set; }
        public int AccZ_mg { get; set; }
    }
}
