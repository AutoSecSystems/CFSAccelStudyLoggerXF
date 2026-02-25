using System;
//using Android.Bluetooth;
//using Android.Bluetooth.LE;
using Xamarin.Forms;

[assembly: Dependency(typeof(AccelStudyLoggerXF.Android.BleAdvScannerAndroid))]
namespace AccelStudyLoggerXF.Android
{
    public class BleAdvScannerAndroid : AccelStudyLoggerXF.IBleAdvScanner
    {
        global::Android.Bluetooth.LE.BluetoothLeScanner? _scanner;
        ScanCallbackImpl? _callback;

        public bool IsSupported()
        {
            var adapter = global::Android.Bluetooth.BluetoothAdapter.DefaultAdapter;
            return adapter != null && adapter.IsEnabled && adapter.BluetoothLeScanner != null;
        }

        public void StartScan(Action<AccelStudyLoggerXF.BleAdvPacket> onPacket)
        {
            var adapter = global::Android.Bluetooth.BluetoothAdapter.DefaultAdapter;
            _scanner = adapter.BluetoothLeScanner;

            var settings = new global::Android.Bluetooth.LE.ScanSettings.Builder()
    .SetScanMode(global::Android.Bluetooth.LE.ScanMode.LowLatency)
    .Build();


            _callback = new ScanCallbackImpl(onPacket);
            _scanner.StartScan(null, settings, _callback);
        }

        public void StopScan()
        {
            try
            {
                if (_scanner != null && _callback != null)
                    _scanner.StopScan(_callback);
            }
            catch { }
            _callback = null;
            _scanner = null;
        }

        class ScanCallbackImpl : global::Android.Bluetooth.LE.ScanCallback
        {
            readonly Action<AccelStudyLoggerXF.BleAdvPacket> _onPacket;
            public ScanCallbackImpl(Action<AccelStudyLoggerXF.BleAdvPacket> onPacket) => _onPacket = onPacket;

            public override void OnScanResult(global::Android.Bluetooth.LE.ScanCallbackType callbackType, global::Android.Bluetooth.LE.ScanResult result)
            {
                try
                {
                    var mac = result.Device?.Address ?? "";
                    var rssi = result.Rssi;
                    var bytes = result.ScanRecord?.GetBytes();
                    if (bytes == null) return;

                    _onPacket(new AccelStudyLoggerXF.BleAdvPacket
                    {
                        TimestampMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        Mac = mac,
                        Rssi = rssi,
                        RawScanRecord = bytes
                    });
                }
                catch { }
            }
        }
    }
}
