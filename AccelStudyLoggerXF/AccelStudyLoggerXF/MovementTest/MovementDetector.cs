using System;
using System.Collections.Generic;

namespace AccelStudyLoggerXF.MovementTest
{
    public enum MovementMode
    {
        Idle,
        Candidate,
        Moving,
        Settling,
        Cooldown
    }

    public class MovementDetectorSnapshot
    {
        public MovementMode Mode { get; set; }
        public int AMag { get; set; }
        public int BaseMag { get; set; }
        public int Delta { get; set; }
        public int DeltaPeak { get; set; }
        public int Dur250Ms { get; set; }
        public int Dur350Ms { get; set; }
        public DateTime LastPacketUtc { get; set; }
        public int LastRssi { get; set; }
    }

    public class MovementDetector
    {
        private readonly EventKeyGenerator _eventKeyGenerator;
        private readonly Action<string> _log;

        private MovementOptions _options;
        private MovementMode _mode = MovementMode.Idle;

        private DateTime _lastPacketUtc = DateTime.MinValue;
        private int _lastMag;
        private int _baseMag;

        private DateTime _candidateStartUtc;
        private DateTime _moveStartUtc;
        private DateTime _moveEndUtc;
        private int _deltaPeak;
        private int _dur250Ms;
        private int _dur350Ms;
        private int _settleQuietSamples;
        private int _endQuietSamples;
        private int _postShiftPeak;
        private DateTime _cooldownUntilUtc = DateTime.MinValue;

        private string _tagMac = string.Empty;
        private string _gatewayMac = string.Empty;
        private int _lastRssi;

        public MovementDetectorSnapshot Snapshot { get; private set; } = new MovementDetectorSnapshot();

        public MovementDetector(MovementOptions options, EventKeyGenerator eventKeyGenerator, Action<string> log = null)
        {
            _options = options?.Clone() ?? MovementOptions.ProductionPreset();
            _eventKeyGenerator = eventKeyGenerator ?? throw new ArgumentNullException(nameof(eventKeyGenerator));
            _log = log;
        }

        public void UpdateOptions(MovementOptions options)
        {
            _options = options?.Clone() ?? _options;
        }

        public MovementDetectionResult Process(TagAdvertisementRecord record)
        {
            if (record == null) return null;
            if (_options.AllowedObjTypes != null && _options.AllowedObjTypes.Count > 0 && !_options.AllowedObjTypes.Contains(record.ObjType))
                return null;

            var now = record.EventTimeUtc;
            var mag = Math.Abs(record.AccX_mg) + Math.Abs(record.AccY_mg) + Math.Abs(record.AccZ_mg);
            var dtMs = _lastPacketUtc == DateTime.MinValue ? 0 : (int)Math.Max(0, (now - _lastPacketUtc).TotalMilliseconds);

            _tagMac = record.TagMac;
            _gatewayMac = record.GatewayMac;
            _lastRssi = record.Rssi;

            if (_lastPacketUtc == DateTime.MinValue)
            {
                _baseMag = mag;
                _lastMag = mag;
                _lastPacketUtc = now;
                UpdateSnapshot(now, mag, 0);
                return null;
            }

            if (_mode != MovementMode.Cooldown && dtMs > _options.NoMessageEndMs && (_mode == MovementMode.Candidate || _mode == MovementMode.Moving || _mode == MovementMode.Settling))
            {
                _moveEndUtc = _lastPacketUtc;
                var finalized = FinalizeEvent();
                StartCooldown(now);
                _lastPacketUtc = now;
                _lastMag = mag;
                UpdateSnapshot(now, mag, Math.Abs(mag - _baseMag));
                return finalized;
            }

            var delta = Math.Abs(mag - _baseMag);

            if (_mode == MovementMode.Cooldown)
            {
                if (now >= _cooldownUntilUtc)
                {
                    _mode = MovementMode.Idle;
                    _baseMag = mag;
                }
                _lastPacketUtc = now;
                _lastMag = mag;
                UpdateSnapshot(now, mag, delta);
                return null;
            }

            switch (_mode)
            {
                case MovementMode.Idle:
                    if (delta <= _options.QuietDelta)
                    {
                        _baseMag = (_baseMag * 4 + mag) / 5;
                    }
                    else if (delta >= _options.WakeDelta)
                    {
                        BeginCandidate(now, delta);
                    }
                    break;

                case MovementMode.Candidate:
                    ApplyDurations(delta, dtMs);
                    _deltaPeak = Math.Max(_deltaPeak, delta);

                    if ((now - _candidateStartUtc).TotalSeconds > _options.CandidateMaxWindowSec)
                    {
                        ResetToIdle(mag);
                    }
                    else if (_deltaPeak >= _options.ConfirmPeak || _dur250Ms >= _options.ConfirmDur250Ms)
                    {
                        _mode = MovementMode.Moving;
                        _moveStartUtc = _candidateStartUtc;
                    }
                    else if (delta <= _options.QuietDelta)
                    {
                        ResetToIdle(mag);
                    }
                    break;

                case MovementMode.Moving:
                    ApplyDurations(delta, dtMs);
                    _deltaPeak = Math.Max(_deltaPeak, delta);

                    if (delta <= _options.SettleDelta)
                    {
                        _settleQuietSamples++;
                        if (_settleQuietSamples >= _options.SettleQuietSamples)
                        {
                            _mode = MovementMode.Settling;
                            _endQuietSamples = 0;
                        }
                    }
                    else
                    {
                        _settleQuietSamples = 0;
                    }
                    break;

                case MovementMode.Settling:
                    ApplyDurations(delta, dtMs);
                    _deltaPeak = Math.Max(_deltaPeak, delta);
                    _postShiftPeak = Math.Max(_postShiftPeak, delta);

                    if (delta <= _options.QuietDelta)
                    {
                        _endQuietSamples++;
                        if (_endQuietSamples >= _options.EndQuietSamples)
                        {
                            _moveEndUtc = now;
                            var result = FinalizeEvent();
                            StartCooldown(now);
                            _lastPacketUtc = now;
                            _lastMag = mag;
                            UpdateSnapshot(now, mag, delta);
                            return result;
                        }
                    }
                    else
                    {
                        _endQuietSamples = 0;
                    }
                    break;
            }

            _lastPacketUtc = now;
            _lastMag = mag;
            UpdateSnapshot(now, mag, delta);
            return null;
        }

        private void BeginCandidate(DateTime now, int delta)
        {
            _mode = MovementMode.Candidate;
            _candidateStartUtc = now;
            _moveStartUtc = now;
            _deltaPeak = delta;
            _dur250Ms = 0;
            _dur350Ms = 0;
            _settleQuietSamples = 0;
            _endQuietSamples = 0;
            _postShiftPeak = 0;
            _log?.Invoke("Movement candidate started.");
        }

        private void ApplyDurations(int delta, int dtMs)
        {
            if (delta >= _options.Delta250) _dur250Ms += dtMs;
            if (delta >= _options.Delta350) _dur350Ms += dtMs;
        }

        private MovementDetectionResult FinalizeEvent()
        {
            var postShift = _mode == MovementMode.Settling ? (int?)_postShiftPeak : null;
            var classificationText = Classify(_deltaPeak, _dur250Ms, _dur350Ms, postShift);
            var classification = classificationText == "REAL_MOVE"
                ? MovementClassification.REAL_MOVE
                : MovementClassification.NOT_REAL;

            var eventKey = _eventKeyGenerator.Generate(_tagMac, _gatewayMac, _moveStartUtc, _moveEndUtc);

            var result = new MovementDetectionResult
            {
                Classification = classification,
                TagMac = _tagMac,
                GatewayMac = _gatewayMac,
                StartUtc = _moveStartUtc,
                EndUtc = _moveEndUtc,
                PeakDelta = _deltaPeak,
                Duration250Ms = _dur250Ms,
                Duration350Ms = _dur350Ms,
                PostShift = postShift ?? 0,
                EventKey = eventKey
            };

            ResetToIdle(_lastMag);
            _log?.Invoke("Movement finalized: " + classification);
            if (classification == MovementClassification.NOT_REAL && !_options.SendNotRealEvents)
            {
                return result;
            }

            return result;
        }

        private static string Classify(int peak, int dur250, int dur350, int? postShift)
        {
            var ruleA = peak >= 500 && dur250 >= 2000;
            var ruleB = dur250 >= 3000 && dur350 >= 1000 && peak >= 300;
            var ruleC = postShift.HasValue && dur250 >= 6000 && peak >= 300 && postShift.Value >= 100;
            var ruleD = dur250 >= 7000 && peak >= 400;

            return (ruleA || ruleB || ruleC || ruleD) ? "REAL_MOVE" : "NOT_REAL";
        }

        private void ResetToIdle(int mag)
        {
            _mode = MovementMode.Idle;
            _baseMag = mag;
            _deltaPeak = 0;
            _dur250Ms = 0;
            _dur350Ms = 0;
            _settleQuietSamples = 0;
            _endQuietSamples = 0;
            _postShiftPeak = 0;
        }

        private void StartCooldown(DateTime now)
        {
            _mode = MovementMode.Cooldown;
            _cooldownUntilUtc = now.AddSeconds(_options.CooldownSec);
        }

        private void UpdateSnapshot(DateTime now, int mag, int delta)
        {
            Snapshot.Mode = _mode;
            Snapshot.AMag = mag;
            Snapshot.BaseMag = _baseMag;
            Snapshot.Delta = delta;
            Snapshot.DeltaPeak = _deltaPeak;
            Snapshot.Dur250Ms = _dur250Ms;
            Snapshot.Dur350Ms = _dur350Ms;
            Snapshot.LastPacketUtc = now;
            Snapshot.LastRssi = _lastRssi;
        }
    }
}
