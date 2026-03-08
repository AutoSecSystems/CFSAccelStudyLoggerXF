using System.Collections.Generic;

namespace AccelStudyLoggerXF.MovementTest
{
    public class MovementOptions
    {
        public int QuietDelta { get; set; } = 120;
        public int WakeDelta { get; set; } = 200;
        public int ConfirmPeak { get; set; } = 500;
        public int ConfirmDur250Ms { get; set; } = 2000;
        public int Delta250 { get; set; } = 250;
        public int Delta350 { get; set; } = 350;
        public int SettleDelta { get; set; } = 180;
        public int SettleQuietSamples { get; set; } = 3;
        public int EndQuietSamples { get; set; } = 4;
        public int NoMessageEndMs { get; set; } = 3000;
        public int CandidateMaxWindowSec { get; set; } = 10;
        public int CooldownSec { get; set; } = 20;
        public int PostShiftThreshold { get; set; } = 100;
        public int StrongNoPostShiftPeak { get; set; } = 600;
        public int StrongNoPostShiftDur250Ms { get; set; } = 3000;
        public bool SendNotRealEvents { get; set; } = false;
        public List<int> AllowedObjTypes { get; set; } = new List<int> { 1 };

        public MovementOptions Clone()
        {
            return new MovementOptions
            {
                QuietDelta = QuietDelta,
                WakeDelta = WakeDelta,
                ConfirmPeak = ConfirmPeak,
                ConfirmDur250Ms = ConfirmDur250Ms,
                Delta250 = Delta250,
                Delta350 = Delta350,
                SettleDelta = SettleDelta,
                SettleQuietSamples = SettleQuietSamples,
                EndQuietSamples = EndQuietSamples,
                NoMessageEndMs = NoMessageEndMs,
                CandidateMaxWindowSec = CandidateMaxWindowSec,
                CooldownSec = CooldownSec,
                PostShiftThreshold = PostShiftThreshold,
                StrongNoPostShiftPeak = StrongNoPostShiftPeak,
                StrongNoPostShiftDur250Ms = StrongNoPostShiftDur250Ms,
                SendNotRealEvents = SendNotRealEvents,
                AllowedObjTypes = new List<int>(AllowedObjTypes ?? new List<int>())
            };
        }

        public static MovementOptions ProductionPreset() => new MovementOptions();
    }
}
