using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

namespace AccelStudyLoggerXF.Android
{
    [Activity(Label = "Accel Study Logger", Icon = "@mipmap/icon", Theme = "@style/MainTheme",
        MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize, Exported = true)]
    public class MainActivity : FormsAppCompatActivity
    {
        const int ReqCode = 9001;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            Forms.Init(this, savedInstanceState);

            RequestBlePermissionsIfNeeded();

            LoadApplication(new AccelStudyLoggerXF.App());
        }

        void RequestBlePermissionsIfNeeded()
        {
            if ((int)Build.VERSION.SdkInt >= 31) // Android 12+
            {
                var scan = ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothScan);
                var connect = ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothConnect);
                var loc = ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation);

                if (scan != Permission.Granted || connect != Permission.Granted || loc != Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(this, new[]
                    {
                        Manifest.Permission.BluetoothScan,
                        Manifest.Permission.BluetoothConnect,
                        Manifest.Permission.AccessFineLocation
                    }, ReqCode);
                }
            }
            else
            {
                var loc = ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation);
                if (loc != Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(this, new[]
                    {
                        Manifest.Permission.AccessFineLocation
                    }, ReqCode);
                }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
