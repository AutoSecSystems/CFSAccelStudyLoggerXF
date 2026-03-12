namespace AccelStudyLoggerXF.MovementTest
{
    public class MoveStartOptions
    {
        public int QuietDelta { get; set; } = 120;
        public int WakeDelta { get; set; } = 220;
        public int Delta250 { get; set; } = 250;
        public int Delta350 { get; set; } = 350;
        public int CandidateTargetWindowMs { get; set; } = 3000;
        public int CandidateMaxWindowMs { get; set; } = 5000;
        public int CooldownMs { get; set; } = 15000;
        public double BaselineAlpha { get; set; } = 0.08d;
        public int MinMotionSamples { get; set; } = 3;
        public int MinPersistence { get; set; } = 2;
        public double EnergyThreshold { get; set; } = 300000d;
        public int MinActiveDurationMs { get; set; } = 220;
        public int OrientationShiftThreshold { get; set; } = 260;

        public MoveStartOptions Clone()
        {
            return (MoveStartOptions)MemberwiseClone();
        }

        public static MoveStartOptions Defaults() => new MoveStartOptions();
    }
}
