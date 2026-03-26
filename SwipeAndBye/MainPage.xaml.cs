using Environment = Android.OS.Environment;
using Uri = Android.Net.Uri;

namespace SwipeAndBye;

public partial class MainPage
{
    #region Variables

    private bool _debeRecargar;

    #endregion Variables

    #region Constructor

    public MainPage()
    {
        InitializeComponent();
    }

    #endregion Constructor

    #region "Eventos"

    #region "Eventos de Pagina"

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_debeRecargar || Environment.IsExternalStorageManager)
        {
            _debeRecargar = false;
            await CargarDatos();
            return;
        }

        if (!Environment.IsExternalStorageManager)
        {
            _debeRecargar = true;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                Intent intent = new(Settings.ActionManageAppAllFilesAccessPermission);
                intent.SetData(Uri.Parse("package:" + AppInfo.PackageName));
                Platform.CurrentActivity.StartActivity(intent);
            }
        }
    }

    #endregion "Eventos de Pagina"

    #region "Gesture Recognizers"

    private async void TapGestureRecognizer_OnTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (sender is BindableObject view && view.BindingContext is GrupoMes mes)
                await Navigation.PushModalAsync(new PageSwipe(mes.Fotos));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Toast.MakeText(Platform.CurrentActivity, "Error al abrir la página de swipe", ToastLength.Short).Show();
        }
    }

    #endregion "Gesture Recognizers"

    #endregion "Eventos"

    #region "Funciones"

    private async Task CargarDatos()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BorderIndicator.IsVisible = true;
                Indicator.IsVisible = true;
                Indicator.IsRunning = true;
            });

            await Task.Yield();

            long total = MdUtilidades.ObtenerBytesAhorrados();
            int totalEliminadas = MdUtilidades.ObtenerImagenesEliminadas();
            int totalFotos = MdUtilidades.ObtenerTotalFotos();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                LblAhorro.Text = FormatearBytes(total);
                LblEliminadas.Text = totalEliminadas.ToString();
                LblFotosEncontradas.Text = totalFotos.ToString();
            });

            var fotos = await Task.Run(() => MdUtilidades.ObtenerFotos("/storage/emulated/0"));
            MdUtilidades.SetTotalFotos(fotos.Count);

            var GrupoAño = await Task.Run(() => MdUtilidades.AgruparFotos(fotos));

            MainThread.BeginInvokeOnMainThread(() =>
            {
                LblFotosEncontradas.Text = fotos.Count.ToString();
                cv.ItemsSource = GrupoAño;
                Indicator.IsRunning = false;
                Indicator.IsVisible = false;
                BorderIndicator.IsVisible = false;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Toast.MakeText(Platform.CurrentActivity, "Error al abrir la página de swipe", ToastLength.Short).Show();
        }
    }

    private string FormatearBytes(long bytes)
    {
        switch (bytes)
        {
            case 0:
                return "0 B";

            case < 0:
                return "-" + FormatearBytes(-bytes);
        }

        string[] sufijos = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

        decimal tamaño = bytes;
        int i = 0;

        while (tamaño >= 1024 && i < sufijos.Length - 1)
        {
            tamaño /= 1024;
            i++;
        }

        return $"{tamaño:0.##} {sufijos[i]}";
    }

    #endregion "Funciones"
}