using System;
using System.IO;
using Xamarin.Essentials;
using Xamarin.Forms;
using AccelStudyLoggerXF.MovementTest;

namespace AccelStudyLoggerXF
{
    public partial class MainPage : ContentPage
    {
        private IBleAdvScanner _scanner;
        private CsvSessionLogger _logger;
        private bool _running;
        private int _packetCount;

        public MainPage()
        {
            InitializeComponent();
            _scanner = DependencyService.Get<IBleAdvScanner>();
        }

        async void StartSession_Clicked(object sender, EventArgs e)
        {
            var mac = (MacEntry.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(mac))
            {
                await DisplayAlert("Missing", "Enter Tag MAC address.", "OK");
                return;
            }

            if (_scanner == null || !_scanner.IsSupported())
            {
                await DisplayAlert("Not supported", "BLE advertisement scan not supported or Bluetooth is OFF.", "OK");
                return;
            }

            _packetCount = 0;
            _logger = new CsvSessionLogger(new SessionMeta
            {
                TagMac = mac.ToUpperInvariant(),
                ContainerNo = (ContainerEntry.Text ?? "").Trim(),
                Location = (LocationEntry.Text ?? "").Trim(),
                Notes = (NotesEditor.Text ?? "").Trim(),
            });

            _running = true;
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            ExportBtn.IsEnabled = false;
            StatusLabel.Text = "Status: Running (BLE ADV scan)";

            _scanner.StartScan(OnPacket);
        }

        void StopSession_Clicked(object sender, EventArgs e)
        {
            _running = false;
            try { _scanner?.StopScan(); } catch { }

            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
            ExportBtn.IsEnabled = _logger != null;
            StatusLabel.Text = "Status: Stopped";
        }

        void OnPacket(BleAdvPacket p)
        {
            if (!_running || _logger == null) return;

            if (!string.Equals(p.Mac, _logger.Meta.TagMac, StringComparison.OrdinalIgnoreCase)) return;

            KSensorData ks = null;
            if (KSensorParser.TryParseFromScanRecord(p.RawScanRecord, out var parsed))
                ks = parsed;

            _logger.AddPacket(p, ks);

            _packetCount++;

            Device.BeginInvokeOnMainThread(() =>
            {
                StatsLabel.Text = $"Packets: {_packetCount} | Last RSSI: {p.Rssi}";
            });
        }

        void LiftStart_Clicked(object s, EventArgs e) => _logger?.MarkEvent("LIFT_START");
        void LiftOff_Clicked(object s, EventArgs e) => _logger?.MarkEvent("LIFT_OFF");
        void SetDown_Clicked(object s, EventArgs e) => _logger?.MarkEvent("SET_DOWN");
        void Touch_Clicked(object s, EventArgs e) => _logger?.MarkEvent("TOUCH_VIBRATION");

        async void Export_Clicked(object sender, EventArgs e)
        {
            if (_logger == null) return;

            var csv = _logger.BuildCsv();
            var fileName = $"accel_study_{_logger.Meta.ContainerNo}_{_logger.Meta.SessionId}.csv";
            var path = Path.Combine(FileSystem.AppDataDirectory, fileName);

            File.WriteAllText(path, csv);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Share CSV",
                File = new ShareFile(path)
            });
        }

        async void MovementTest_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new MovementTestPage());
        }
    }
}
