using System;

namespace AccelStudyLoggerXF.MovementTest
{
    public enum MoveStartDetectorMode
    {
        Idle,
        Candidate,
        Triggered
    }

    public class MoveStartDetectorSnapshot
    {
        public MoveStartDetectorMode Mode { get; set; }
        public string TagMac { get; set; } = string.Empty;
        public int LastRssi { get; set; }
        public int AMag { get; set; }
        public int Baseline { get; set; }
        public int Delta { get; set; }
        public int PeakDelta { get; set; }
        public int ActiveDurationMs { get; set; }
        public int MotionSampleCount { get; set; }
        public int MaxPersistence { get; set; }
        public double Energy { get; set; }
        public int OrientationShift { get; set; }
        public DateTime LastPacketUtc { get; set; }
    }

    public class MoveStartDetector
    {
        private readonly MoveStartOptions _options;

        private MoveStartDetectorMode _mode = MoveStartDetectorMode.Idle;
        private DateTime _lastPacketUtc = DateTime.MinValue;
        private int _baseline;

        private DateTime _candidateStartUtc;
        private DateTime _cooldownUntilUtc = DateTime.MinValue;

        private int _startX;
        private int _startY;
        private int _startZ;

        private int _peakDelta;
        private int _activeDurationMs;
        private int _motionSampleCount;
        private int _totalSampleCount;
        private int _currentPersistence;
        private int _maxPersistence;
        private double _energy;
        private int _orientationShift;
        private int _lastRssi;
        private string _tagMac = string.Empty;

        public MoveStartDetectorSnapshot Snapshot { get; } = new MoveStartDetectorSnapshot();

        public MoveStartDetector(MoveStartOptions options)
        {
            _options = options?.Clone() ?? MoveStartOptions.Defaults();
        }

        public MoveStartDetectionResult Process(TagAdvertisementRecord record)
        {
            if (record == null)
            {
                return null;
            }

            var now = record.EventTimeUtc;
            var aMag = Math.Abs(record.AccX_mg) + Math.Abs(record.AccY_mg) + Math.Abs(record.AccZ_mg);
            var dtMs = _lastPacketUtc == DateTime.MinValue
                ? 0
                : (int)Math.Max(0, (now - _lastPacketUtc).TotalMilliseconds);

            _tagMac = record.TagMac ?? string.Empty;
            _lastRssi = record.Rssi;

            if (_lastPacketUtc == DateTime.MinValue)
            {
                _baseline = aMag;
                _lastPacketUtc = now;
                UpdateSnapshot(now, aMag, 0);
                return null;
            }

            if (_mode == MoveStartDetectorMode.Triggered && now >= _cooldownUntilUtc)
            {
                ResetToIdle(aMag);
            }

            var delta = Math.Abs(aMag - _baseline);

            if (_mode == MoveStartDetectorMode.Triggered)
            {
                _lastPacketUtc = now;
                if (delta <= _options.QuietDelta)
                {
                    UpdateBaseline(aMag);
                }

                UpdateSnapshot(now, aMag, delta);
                return null;
            }

            MoveStartDetectionResult result = null;
            if (_mode == MoveStartDetectorMode.Idle)
            {
                if (delta >= _options.WakeDelta)
                {
                    BeginCandidate(record, now, delta);
                }
                else
                {
                    if (delta <= _options.QuietDelta)
                    {
                        UpdateBaseline(aMag);
                    }
                }
            }
            else if (_mode == MoveStartDetectorMode.Candidate)
            {
                UpdateCandidate(record, delta, dtMs);
                var elapsedMs = (int)Math.Max(0, (now - _candidateStartUtc).TotalMilliseconds);

                if (elapsedMs >= _options.CandidateTargetWindowMs && ShouldConfirm())
                {
                    result = BuildResult(MoveStartClassification.REAL_MOVE, "confirmed_short_window", now);
                    _mode = MoveStartDetectorMode.Triggered;
                    _cooldownUntilUtc = now.AddMilliseconds(_options.CooldownMs);
                }
                else if (elapsedMs >= _options.CandidateMaxWindowMs)
                {
                    result = BuildResult(MoveStartClassification.NOT_REAL, "insufficient_persistence_or_energy", now);
                    ResetToIdle(aMag);
                }
            }

            _lastPacketUtc = now;
            UpdateSnapshot(now, aMag, delta);
            return result;
        }

        private void BeginCandidate(TagAdvertisementRecord record, DateTime now, int delta)
        {
            _mode = MoveStartDetectorMode.Candidate;
            _candidateStartUtc = now;
            _startX = record.AccX_mg;
            _startY = record.AccY_mg;
            _startZ = record.AccZ_mg;
            _peakDelta = delta;
            _activeDurationMs = 0;
            _motionSampleCount = delta >= _options.QuietDelta ? 1 : 0;
            _totalSampleCount = 1;
            _currentPersistence = delta >= _options.QuietDelta ? 1 : 0;
            _maxPersistence = _currentPersistence;
            _energy = delta >= _options.QuietDelta ? delta * (double)delta : 0d;
            _orientationShift = 0;
        }

        private void UpdateCandidate(TagAdvertisementRecord record, int delta, int dtMs)
        {
            _totalSampleCount++;
            _peakDelta = Math.Max(_peakDelta, delta);

            if (delta >= _options.QuietDelta)
            {
                _motionSampleCount++;
                _currentPersistence++;
                _maxPersistence = Math.Max(_maxPersistence, _currentPersistence);
                _energy += delta * (double)delta;
            }
            else
            {
                _currentPersistence = 0;
            }

            if (delta >= _options.Delta250)
            {
                _activeDurationMs += dtMs;
            }

            var orientationDelta = Math.Abs(record.AccX_mg - _startX)
                + Math.Abs(record.AccY_mg - _startY)
                + Math.Abs(record.AccZ_mg - _startZ);
            _orientationShift = Math.Max(_orientationShift, orientationDelta);
        }

        private bool ShouldConfirm()
        {
            var hasCoreEvidence = _energy >= _options.EnergyThreshold
                && _motionSampleCount >= _options.MinMotionSamples
                && _maxPersistence >= _options.MinPersistence
                && _activeDurationMs >= _options.MinActiveDurationMs;

            if (!hasCoreEvidence)
            {
                return false;
            }

            var hasAmplitudeSupport = _peakDelta >= _options.Delta350 || _orientationShift >= _options.OrientationShiftThreshold;
            return hasAmplitudeSupport;
        }

        private MoveStartDetectionResult BuildResult(MoveStartClassification classification, string reason, DateTime now)
        {
            return new MoveStartDetectionResult
            {
                Classification = classification,
                Reason = reason,
                TagMac = _tagMac,
                DetectedAtUtc = now,
                PeakDelta = _peakDelta,
                Energy = _energy,
                ActiveDurationMs = _activeDurationMs,
                MotionSampleCount = _motionSampleCount,
                MaxPersistence = _maxPersistence,
                OrientationShift = _orientationShift,
                LastRssi = _lastRssi
            };
        }

        private void ResetToIdle(int aMag)
        {
            _mode = MoveStartDetectorMode.Idle;
            _baseline = aMag;
            _peakDelta = 0;
            _activeDurationMs = 0;
            _motionSampleCount = 0;
            _totalSampleCount = 0;
            _currentPersistence = 0;
            _maxPersistence = 0;
            _energy = 0d;
            _orientationShift = 0;
        }

        private void UpdateBaseline(int aMag)
        {
            var alpha = _options.BaselineAlpha;
            if (alpha < 0d) alpha = 0d;
            if (alpha > 1d) alpha = 1d;
            _baseline = (int)Math.Round(((1d - alpha) * _baseline) + (alpha * aMag));
        }

        private void UpdateSnapshot(DateTime now, int aMag, int delta)
        {
            Snapshot.Mode = _mode;
            Snapshot.TagMac = _tagMac;
            Snapshot.LastRssi = _lastRssi;
            Snapshot.AMag = aMag;
            Snapshot.Baseline = _baseline;
            Snapshot.Delta = delta;
            Snapshot.PeakDelta = _peakDelta;
            Snapshot.ActiveDurationMs = _activeDurationMs;
            Snapshot.MotionSampleCount = _motionSampleCount;
            Snapshot.MaxPersistence = _maxPersistence;
            Snapshot.Energy = _energy;
            Snapshot.OrientationShift = _orientationShift;
            Snapshot.LastPacketUtc = now;
        }
    }
}
