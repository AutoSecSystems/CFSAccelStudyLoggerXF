using System;

namespace AccelStudyLoggerXF.MovementTest
{
    public enum MoveStartClassification
    {
        REAL_MOVE,
        NOT_REAL
    }

    public class MoveStartDetectionResult
    {
        public bool IsRealMove => Classification == MoveStartClassification.REAL_MOVE;
        public MoveStartClassification Classification { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string TagMac { get; set; } = string.Empty;
        public DateTime DetectedAtUtc { get; set; }
        public int PeakDelta { get; set; }
        public double Energy { get; set; }
        public int ActiveDurationMs { get; set; }
        public int MotionSampleCount { get; set; }
        public int MaxPersistence { get; set; }
        public int OrientationShift { get; set; }
        public int LastRssi { get; set; }
    }
}
