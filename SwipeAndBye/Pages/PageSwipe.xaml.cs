namespace SwipeAndBye.Pages;

public partial class PageSwipe
{
    #region Constructor

    public PageSwipe(List<FotoItem> fotos)
    {
        InitializeComponent();
        _fotos = fotos;
    }

    #endregion Constructor

    #region "Variables"

    private readonly List<FotoItem> _fotos;

    private int _index;
    private double _totalX;

    #endregion "Variables"

    #region "Eventos"

    #region "Eventos de Pagina"

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            LblContador.Text = $"{_index + 1} / {_fotos.Count}";
            await MostrarActual();
        });
    }

    protected override bool OnBackButtonPressed()
    {
        try
        {
            Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al volver atrás: {ex.Message}");
            Toast.MakeText(Platform.CurrentActivity, "Error al abrir la página de swipe", ToastLength.Short).Show();
        }

        return true;
    }

    #endregion "Eventos de Pagina"

    #region "Eventos de Boton"

    private void BtnDelete_OnClicked(object? sender, EventArgs e)
    {
        SwipeIzquierda();
    }

    private void BtnConservar_OnClicked(object? sender, EventArgs e)
    {
        SwipeDerecha();
    }

    #endregion "Eventos de Boton"

    #region "Eventos de Pan"

    private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        LblLike.Opacity = Math.Max(0, e.TotalX / 200);
        LblDelete.Opacity = Math.Max(0, -e.TotalX / 200);

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _totalX = 0;
                break;

            case GestureStatus.Running:
                _totalX = e.TotalX;

                ImgActual.TranslationX = _totalX;
                ImgActual.Rotation = Math.Clamp(_totalX / 20, -15, 15);
                break;

            case GestureStatus.Completed:
                if (Math.Abs(_totalX) > 120)
                {
                    if (_totalX > 0)
                        SwipeDerecha();
                    else
                        SwipeIzquierda();
                }
                else
                {
                    ResetPosition();
                }

                break;
        }
    }

    #endregion "Eventos de Pan"

    #endregion "Eventos"

    #region "Funciones"

    private async Task MostrarActual()
    {
        if (_index >= _fotos.Count)
        {
            await DisplayAlert("Fin", "No quedan fotos", "OK");

            await Navigation.PushModalAsync(new MainPage());
            return;
        }

        ImgActual.Source = _fotos[_index].Ruta;
    }

    private async void ResetPosition()
    {
        await Task.WhenAll(
            ImgActual.TranslateTo(0, 0, 200, Easing.SinOut),
            ImgActual.RotateTo(0, 200, Easing.SinOut)
        );
    }

    private async void SwipeDerecha()
    {
        await ImgActual.TranslateTo(1000, 0, 200);
        SiguienteFoto();
    }

    private async void SwipeIzquierda()
    {
        FotoItem foto = _fotos[_index];
        await ImgActual.TranslateTo(-1000, 0, 200);
        BorrarFoto(foto);
        SiguienteFoto();
    }

    private void BorrarFoto(FotoItem foto)
    {
        try
        {
            if (!File.Exists(foto.Ruta)) return;

            FileInfo info = new(foto.Ruta);
            long tamaño = info.Length;

            File.SetAttributes(foto.Ruta, FileAttributes.Normal);
            File.Delete(foto.Ruta);

            if (!File.Exists(foto.Ruta))
            {
                MdUtilidades.SumarBytesAhorrados(tamaño);
                MdUtilidades.SumarImagenesEliminadas();
                MdUtilidades.RestarTotalFotos();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error borrando: {ex.Message}");
            Toast.MakeText(Platform.CurrentActivity, "Error al borrar la foto", ToastLength.Short).Show();
        }
    }

    private void SiguienteFoto()
    {
        _index++;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ImgActual.TranslationX = 0;
            ImgActual.Rotation = 0;

            if (_index < _fotos.Count)
                LblContador.Text = $"{_index + 1} / {_fotos.Count}";

            MostrarActual();
        });
    }

    #endregion "Funciones"
}