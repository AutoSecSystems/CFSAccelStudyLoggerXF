using System;
using System.Collections.Generic;
using Xamarin.Forms;

namespace AccelStudyLoggerXF.MovementTest
{
    public partial class MovementTestPage : ContentPage
    {
        private readonly IBleAdvScanner _scanner;
        private readonly MoveStartOptions _options = MoveStartOptions.Defaults();
        private readonly Dictionary<string, MoveStartDetector> _detectors = new Dictionary<string, MoveStartDetector>();

        private bool _scanning;
        private DateTime _lastUiRefreshUtc = DateTime.MinValue;

        public MovementTestPage()
        {
            InitializeComponent();
            _scanner = DependencyService.Get<IBleAdvScanner>();
        }

        private async void StartScan_Clicked(object sender, EventArgs e)
        {
            if (_scanner == null || !_scanner.IsSupported())
            {
                await DisplayAlert("Not supported", "BLE advertisement scan is unavailable.", "OK");
                return;
            }

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
            var filterMac = MacAddressNormalizer.NormalizeNoSeparator(MacFilterEntry?.Text);
            if (!string.IsNullOrEmpty(filterMac) && !string.Equals(normalizedMac, filterMac, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var detector = GetOrCreateDetector(normalizedMac);
            var record = new TagAdvertisementRecord
            {
                TagMac = normalizedMac,
                EventTimeUtc = DateTime.UtcNow,
                Rssi = packet.Rssi,
                AccX_mg = parsed.AxMg.Value,
                AccY_mg = parsed.AyMg.Value,
                AccZ_mg = parsed.AzMg.Value
            };

            var result = detector.Process(record);
            var now = DateTime.UtcNow;
            if ((now - _lastUiRefreshUtc).TotalMilliseconds >= 250)
            {
                _lastUiRefreshUtc = now;
                var snapshot = detector.Snapshot;
                Device.BeginInvokeOnMainThread(() => UpdateLiveStatus(snapshot));
            }

            if (result != null)
            {
                Device.BeginInvokeOnMainThread(() => ShowResult(result));
            }
        }

        private MoveStartDetector GetOrCreateDetector(string tagMac)
        {
            if (_detectors.TryGetValue(tagMac, out var existing))
            {
                return existing;
            }

            var created = new MoveStartDetector(_options);
            _detectors[tagMac] = created;
            return created;
        }

        private void UpdateLiveStatus(MoveStartDetectorSnapshot snapshot)
        {
            ModeLabel.Text = $"Mode: {snapshot.Mode}";
            TagLabel.Text = $"Tag: {snapshot.TagMac} | RSSI: {snapshot.LastRssi}";
            MagnitudeLabel.Text = $"aMag: {snapshot.AMag} | baseline: {snapshot.Baseline} | delta: {snapshot.Delta}";
            MetricsLabel.Text = $"peak: {snapshot.PeakDelta} | activeMs: {snapshot.ActiveDurationMs} | motion: {snapshot.MotionSampleCount}";
            PersistenceEnergyLabel.Text = $"persistence: {snapshot.MaxPersistence} | energy: {snapshot.Energy:F0} | orientShift: {snapshot.OrientationShift}";
            PacketLabel.Text = $"last packet: {snapshot.LastPacketUtc:HH:mm:ss.fff}";
        }

        private void ShowResult(MoveStartDetectionResult result)
        {
            if (result.IsRealMove)
            {
                ResultFrame.BackgroundColor = Color.FromHex("#B7F7C4");
                ResultBanner.TextColor = Color.FromHex("#0B6E2E");
                ResultBanner.FontSize = 28;
                ResultBanner.Text = "REAL_MOVE DETECTED";
            }
            else
            {
                ResultFrame.BackgroundColor = Color.FromHex("#E6E6E6");
                ResultBanner.TextColor = Color.FromHex("#666666");
                ResultBanner.FontSize = 14;
                ResultBanner.Text = "NOT_REAL (ignored)";
            }

            ResultDetails.Text =
                $"classification: {result.Classification}\n" +
                $"reason: {result.Reason}\n" +
                $"tag: {result.TagMac}\n" +
                $"time: {result.DetectedAtUtc:O}\n" +
                $"peak: {result.PeakDelta}\n" +
                $"energy: {result.Energy:F0}\n" +
                $"activeMs: {result.ActiveDurationMs}\n" +
                $"motionSamples: {result.MotionSampleCount}\n" +
                $"persistence: {result.MaxPersistence}\n" +
                $"orientationShift: {result.OrientationShift}";
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
