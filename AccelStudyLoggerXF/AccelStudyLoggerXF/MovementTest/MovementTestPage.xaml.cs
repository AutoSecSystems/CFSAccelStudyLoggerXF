using System;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace AccelStudyLoggerXF.MovementTest
{
    public partial class MovementTestPage : ContentPage
    {
        private const string PrefPrefix = "MovementTest.";

        private IBleAdvScanner _scanner;
        private MovementOptions _options;
        private readonly EventKeyGenerator _eventKeyGenerator = new EventKeyGenerator();
        private readonly Dictionary<string, MovementDetector> _detectors = new Dictionary<string, MovementDetector>();
        private bool _scanning;
        private DateTime _lastUiRefreshUtc = DateTime.MinValue;

        public MovementTestPage()
        {
            InitializeComponent();
            _scanner = DependencyService.Get<IBleAdvScanner>();

            _options = MovementOptions.ProductionPreset();
            LoadOptionsFromPreferences();
            BindOptionsToUi(_options);
        }

        private async void StartScan_Clicked(object sender, EventArgs e)
        {
            if (_scanner == null || !_scanner.IsSupported())
            {
                await DisplayAlert("Not supported", "BLE advertisement scan is unavailable.", "OK");
                return;
            }

            ApplyOptionsFromUi();
            _detectors.Clear();
            _scanning = true;
            StartScanBtn.IsEnabled = false;
            StopScanBtn.IsEnabled = true;
            _scanner.StartScan(OnPacket);
        }

        private void StopScan_Clicked(object sender, EventArgs e)
        {
            _scanning = false;
            try { _scanner?.StopScan(); } catch { }
            StartScanBtn.IsEnabled = true;
            StopScanBtn.IsEnabled = false;
        }

        private void OnPacket(BleAdvPacket packet)
        {
            if (!_scanning) return;
            if (!KSensorParser.TryParseFromScanRecord(packet.RawScanRecord, out var parsed)) return;
            if (!parsed.AxMg.HasValue || !parsed.AyMg.HasValue || !parsed.AzMg.HasValue) return;

            var normalizedMac = MacAddressNormalizer.NormalizeNoSeparator(packet.Mac);
            var filter = MacAddressNormalizer.NormalizeNoSeparator(MacFilterEntry.Text);
            if (!string.IsNullOrEmpty(filter) && !string.Equals(normalizedMac, filter, StringComparison.OrdinalIgnoreCase)) return;

            var detector = GetOrCreateDetector(normalizedMac);
            var record = new TagAdvertisementRecord
            {
                TagMac = normalizedMac,
                GatewayMac = string.Empty,
                EventTimeUtc = DateTime.UtcNow,
                Rssi = packet.Rssi,
                AccX_mg = parsed.AxMg.Value,
                AccY_mg = parsed.AyMg.Value,
                AccZ_mg = parsed.AzMg.Value,
                ObjType = 1
            };

            var result = detector.Process(record);
            var now = DateTime.UtcNow;
            if ((now - _lastUiRefreshUtc).TotalMilliseconds >= 200)
            {
                _lastUiRefreshUtc = now;
                var snap = detector.Snapshot;
                Device.BeginInvokeOnMainThread(() =>
                {
                    ModeLabel.Text = "Mode: " + snap.Mode;
                    MagLabel.Text = $"aMag: {snap.AMag} | baseMag: {snap.BaseMag} | delta: {snap.Delta}";
                    DurationLabel.Text = $"deltaPeak: {snap.DeltaPeak} | dur250ms: {snap.Dur250Ms} | dur350ms: {snap.Dur350Ms}";
                    PacketLabel.Text = $"last packet: {snap.LastPacketUtc:HH:mm:ss.fff} | RSSI: {snap.LastRssi}";
                });
            }

            if (result != null)
            {
                Device.BeginInvokeOnMainThread(() => ShowResult(result));
            }
        }

        private MovementDetector GetOrCreateDetector(string tagMac)
        {
            if (_detectors.TryGetValue(tagMac, out var existing)) return existing;
            var created = new MovementDetector(_options, _eventKeyGenerator);
            _detectors[tagMac] = created;
            return created;
        }

        private void ShowResult(MovementDetectionResult result)
        {
            if (result.Classification == MovementClassification.REAL_MOVE)
            {
                ResultFrame.BackgroundColor = Color.FromHex("#B7F7C4");
                ResultBanner.TextColor = Color.FromHex("#0B6E2E");
                ResultBanner.FontSize = 26;
                ResultBanner.Text = "REAL_MOVE DETECTED";
            }
            else
            {
                ResultFrame.BackgroundColor = Color.FromHex("#E6E6E6");
                ResultBanner.TextColor = Color.FromHex("#666666");
                ResultBanner.FontSize = 14;
                ResultBanner.Text = "NOT_REAL (ignored)";
            }

            ResultDetails.Text = $"tagMac={result.TagMac}\n" +
                                 $"start={result.StartUtc:O}\n" +
                                 $"end={result.EndUtc:O}\n" +
                                 $"peak={result.PeakDelta}\n" +
                                 $"dur250={result.Duration250Ms}\n" +
                                 $"dur350={result.Duration350Ms}\n" +
                                 $"postShift={result.PostShift}\n" +
                                 $"eventKey={result.EventKey}";
        }

        private void Apply_Clicked(object sender, EventArgs e)
        {
            ApplyOptionsFromUi();
            foreach (var detector in _detectors.Values)
            {
                detector.UpdateOptions(_options);
            }
        }

        private void SavePreset_Clicked(object sender, EventArgs e)
        {
            ApplyOptionsFromUi();
            SaveOptionsToPreferences(_options);
        }

        private void LoadPreset_Clicked(object sender, EventArgs e)
        {
            LoadOptionsFromPreferences();
            BindOptionsToUi(_options);
            foreach (var detector in _detectors.Values)
            {
                detector.UpdateOptions(_options);
            }
        }

        private void ProductionPreset_Clicked(object sender, EventArgs e)
        {
            _options = MovementOptions.ProductionPreset();
            BindOptionsToUi(_options);
            foreach (var detector in _detectors.Values)
            {
                detector.UpdateOptions(_options);
            }
        }

        private void ApplyOptionsFromUi()
        {
            var updated = _options.Clone();
            updated.QuietDelta = ReadInt(QuietDeltaEntry.Text, updated.QuietDelta);
            updated.WakeDelta = ReadInt(WakeDeltaEntry.Text, updated.WakeDelta);
            updated.ConfirmPeak = ReadInt(ConfirmPeakEntry.Text, updated.ConfirmPeak);
            updated.ConfirmDur250Ms = ReadInt(ConfirmDur250MsEntry.Text, updated.ConfirmDur250Ms);
            updated.Delta250 = ReadInt(Delta250Entry.Text, updated.Delta250);
            updated.Delta350 = ReadInt(Delta350Entry.Text, updated.Delta350);
            updated.SettleDelta = ReadInt(SettleDeltaEntry.Text, updated.SettleDelta);
            updated.SettleQuietSamples = ReadInt(SettleQuietSamplesEntry.Text, updated.SettleQuietSamples);
            updated.EndQuietSamples = ReadInt(EndQuietSamplesEntry.Text, updated.EndQuietSamples);
            updated.NoMessageEndMs = ReadInt(NoMessageEndMsEntry.Text, updated.NoMessageEndMs);
            updated.CandidateMaxWindowSec = ReadInt(CandidateMaxWindowSecEntry.Text, updated.CandidateMaxWindowSec);
            updated.CooldownSec = ReadInt(CooldownSecEntry.Text, updated.CooldownSec);
            updated.PostShiftThreshold = ReadInt(PostShiftThresholdEntry.Text, updated.PostShiftThreshold);
            updated.StrongNoPostShiftPeak = ReadInt(StrongNoPostShiftPeakEntry.Text, updated.StrongNoPostShiftPeak);
            updated.StrongNoPostShiftDur250Ms = ReadInt(StrongNoPostShiftDur250MsEntry.Text, updated.StrongNoPostShiftDur250Ms);
            updated.SendNotRealEvents = SendNotRealSwitch.IsToggled;
            updated.AllowedObjTypes = ParseIntList(AllowedObjTypesEntry.Text, updated.AllowedObjTypes);
            _options = updated;
        }

        private void BindOptionsToUi(MovementOptions options)
        {
            QuietDeltaEntry.Text = options.QuietDelta.ToString();
            WakeDeltaEntry.Text = options.WakeDelta.ToString();
            ConfirmPeakEntry.Text = options.ConfirmPeak.ToString();
            ConfirmDur250MsEntry.Text = options.ConfirmDur250Ms.ToString();
            Delta250Entry.Text = options.Delta250.ToString();
            Delta350Entry.Text = options.Delta350.ToString();
            SettleDeltaEntry.Text = options.SettleDelta.ToString();
            SettleQuietSamplesEntry.Text = options.SettleQuietSamples.ToString();
            EndQuietSamplesEntry.Text = options.EndQuietSamples.ToString();
            NoMessageEndMsEntry.Text = options.NoMessageEndMs.ToString();
            CandidateMaxWindowSecEntry.Text = options.CandidateMaxWindowSec.ToString();
            CooldownSecEntry.Text = options.CooldownSec.ToString();
            PostShiftThresholdEntry.Text = options.PostShiftThreshold.ToString();
            StrongNoPostShiftPeakEntry.Text = options.StrongNoPostShiftPeak.ToString();
            StrongNoPostShiftDur250MsEntry.Text = options.StrongNoPostShiftDur250Ms.ToString();
            SendNotRealSwitch.IsToggled = options.SendNotRealEvents;
            AllowedObjTypesEntry.Text = string.Join(",", options.AllowedObjTypes ?? new List<int>());
        }

        private void SaveOptionsToPreferences(MovementOptions options)
        {
            Preferences.Set(PrefPrefix + nameof(options.QuietDelta), options.QuietDelta);
            Preferences.Set(PrefPrefix + nameof(options.WakeDelta), options.WakeDelta);
            Preferences.Set(PrefPrefix + nameof(options.ConfirmPeak), options.ConfirmPeak);
            Preferences.Set(PrefPrefix + nameof(options.ConfirmDur250Ms), options.ConfirmDur250Ms);
            Preferences.Set(PrefPrefix + nameof(options.Delta250), options.Delta250);
            Preferences.Set(PrefPrefix + nameof(options.Delta350), options.Delta350);
            Preferences.Set(PrefPrefix + nameof(options.SettleDelta), options.SettleDelta);
            Preferences.Set(PrefPrefix + nameof(options.SettleQuietSamples), options.SettleQuietSamples);
            Preferences.Set(PrefPrefix + nameof(options.EndQuietSamples), options.EndQuietSamples);
            Preferences.Set(PrefPrefix + nameof(options.NoMessageEndMs), options.NoMessageEndMs);
            Preferences.Set(PrefPrefix + nameof(options.CandidateMaxWindowSec), options.CandidateMaxWindowSec);
            Preferences.Set(PrefPrefix + nameof(options.CooldownSec), options.CooldownSec);
            Preferences.Set(PrefPrefix + nameof(options.PostShiftThreshold), options.PostShiftThreshold);
            Preferences.Set(PrefPrefix + nameof(options.StrongNoPostShiftPeak), options.StrongNoPostShiftPeak);
            Preferences.Set(PrefPrefix + nameof(options.StrongNoPostShiftDur250Ms), options.StrongNoPostShiftDur250Ms);
            Preferences.Set(PrefPrefix + nameof(options.SendNotRealEvents), options.SendNotRealEvents);
            Preferences.Set(PrefPrefix + nameof(options.AllowedObjTypes), string.Join(",", options.AllowedObjTypes ?? new List<int>()));
        }

        private void LoadOptionsFromPreferences()
        {
            var defaults = MovementOptions.ProductionPreset();
            _options = new MovementOptions
            {
                QuietDelta = Preferences.Get(PrefPrefix + nameof(defaults.QuietDelta), defaults.QuietDelta),
                WakeDelta = Preferences.Get(PrefPrefix + nameof(defaults.WakeDelta), defaults.WakeDelta),
                ConfirmPeak = Preferences.Get(PrefPrefix + nameof(defaults.ConfirmPeak), defaults.ConfirmPeak),
                ConfirmDur250Ms = Preferences.Get(PrefPrefix + nameof(defaults.ConfirmDur250Ms), defaults.ConfirmDur250Ms),
                Delta250 = Preferences.Get(PrefPrefix + nameof(defaults.Delta250), defaults.Delta250),
                Delta350 = Preferences.Get(PrefPrefix + nameof(defaults.Delta350), defaults.Delta350),
                SettleDelta = Preferences.Get(PrefPrefix + nameof(defaults.SettleDelta), defaults.SettleDelta),
                SettleQuietSamples = Preferences.Get(PrefPrefix + nameof(defaults.SettleQuietSamples), defaults.SettleQuietSamples),
                EndQuietSamples = Preferences.Get(PrefPrefix + nameof(defaults.EndQuietSamples), defaults.EndQuietSamples),
                NoMessageEndMs = Preferences.Get(PrefPrefix + nameof(defaults.NoMessageEndMs), defaults.NoMessageEndMs),
                CandidateMaxWindowSec = Preferences.Get(PrefPrefix + nameof(defaults.CandidateMaxWindowSec), defaults.CandidateMaxWindowSec),
                CooldownSec = Preferences.Get(PrefPrefix + nameof(defaults.CooldownSec), defaults.CooldownSec),
                PostShiftThreshold = Preferences.Get(PrefPrefix + nameof(defaults.PostShiftThreshold), defaults.PostShiftThreshold),
                StrongNoPostShiftPeak = Preferences.Get(PrefPrefix + nameof(defaults.StrongNoPostShiftPeak), defaults.StrongNoPostShiftPeak),
                StrongNoPostShiftDur250Ms = Preferences.Get(PrefPrefix + nameof(defaults.StrongNoPostShiftDur250Ms), defaults.StrongNoPostShiftDur250Ms),
                SendNotRealEvents = Preferences.Get(PrefPrefix + nameof(defaults.SendNotRealEvents), defaults.SendNotRealEvents),
                AllowedObjTypes = ParseIntList(Preferences.Get(PrefPrefix + nameof(defaults.AllowedObjTypes), "1"), new List<int> { 1 })
            };
        }

        private static int ReadInt(string text, int fallback)
        {
            return int.TryParse(text, out var value) ? value : fallback;
        }

        private static List<int> ParseIntList(string text, List<int> fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback ?? new List<int>();
            var values = new List<int>();
            foreach (var token in text.Split(','))
            {
                if (int.TryParse(token.Trim(), out var parsed)) values.Add(parsed);
            }

            return values.Count > 0 ? values : (fallback ?? new List<int>());
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (_scanning)
            {
                StopScan_Clicked(this, EventArgs.Empty);
            }
        }
    }
}
