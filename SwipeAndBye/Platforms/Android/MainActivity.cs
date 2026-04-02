using Android.App;
using Android.Content.PM;

using Environment = Android.OS.Environment;
using Uri = Android.Net.Uri;

namespace SwipeAndBye;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    #region "Variables"

    public const int RequestCode = 2296;

    private static TaskCompletionSource<bool>? _tcsPermiso;

    #endregion "Variables"

    #region "Eventos de Actividad"

    protected override void OnCreate(Bundle savedInstanceState)
    {
        try
        {
            base.OnCreate(savedInstanceState);

            Inicializar();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en OnCreate: {ex.Message}");
            Toast.MakeText(this, "Error al iniciar la aplicación", Android.Widget.ToastLength.Long).Show();
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        try
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == RequestCode)
            {
                bool concedido = Environment.IsExternalStorageManager;
                _tcsPermiso?.SetResult(concedido);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en OnActivityResult: {ex.Message}");
            Toast.MakeText(this, "Error al procesar resultado de permiso", Android.Widget.ToastLength.Long).Show();
            _tcsPermiso?.SetResult(false);
        }
    }

    #endregion "Eventos de Actividad"

    #region "Funciones"

    private async void Inicializar()
    {
        try
        {
            bool tienePermiso = await TienePermisos();

            if (tienePermiso)
                IrAMainPage();
            else
                Toast.MakeText(this, "Debes conceder el permiso de archivos para continuar", Android.Widget.ToastLength.Long).Show();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en Inicializar: {ex.Message}");
            Toast.MakeText(this, "Error al iniciar la aplicación", Android.Widget.ToastLength.Long).Show();
        }
    }

    private async Task<bool> TienePermisos()
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                if (Environment.IsExternalStorageManager)
                    return true;

                return await SolicitarManageExternalStorage();
            }

            // Android < 11
            PermissionStatus readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (readStatus != PermissionStatus.Granted)
            {
                readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
                if (readStatus != PermissionStatus.Granted)
                    return false;
            }

            PermissionStatus writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (writeStatus != PermissionStatus.Granted)
            {
                writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                if (writeStatus != PermissionStatus.Granted)
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error verificando permisos: {ex.Message}");
            Toast.MakeText(this, "Error al verificar permisos", Android.Widget.ToastLength.Long).Show();
            return false;
        }
    }

    public static Task<bool> SolicitarManageExternalStorage()
    {
        try
        {
            Activity? activity = Platform.CurrentActivity;

            _tcsPermiso = new TaskCompletionSource<bool>();

            var uri = Uri.Parse("package:" + activity.PackageName);
            var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission, uri);

            activity.StartActivityForResult(intent, RequestCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error solicitando permiso: {ex.Message}");
            Toast.MakeText(Platform.CurrentActivity, "Error al solicitar permiso de archivos", Android.Widget.ToastLength.Long).Show();
            _tcsPermiso?.SetResult(false);
        }

        return _tcsPermiso!.Task;
    }

    private void IrAMainPage()
    {
        try
        {
            if (Microsoft.Maui.Controls.Application.Current.MainPage is MainPage)
                return;

            Microsoft.Maui.Controls.Application.Current.MainPage = new MainPage();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error navegando a MainPage: {ex.Message}");
            Toast.MakeText(this, "Error al navegar a la página principal", Android.Widget.ToastLength.Long).Show();
        }
    }

    #endregion "Funciones"
}