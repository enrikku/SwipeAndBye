using Environment = Android.OS.Environment;

namespace SwipeAndBye;

public partial class MainPage
{
    #region Constructor

    public MainPage()
    {
        InitializeComponent();
    }

    #endregion Constructor

    #region "Variables"

    /// <summary>Lo que lleve más de esto en la papelera se borra de verdad al arrancar.</summary>
    private static readonly TimeSpan RetencionPapelera = TimeSpan.FromDays(7);

    private CancellationTokenSource? _ctsMiniaturas;

    /// <summary>Caché en memoria del último escaneo. Mientras no sea null no se vuelve a escanear.</summary>
    private ObservableCollection<GrupoAño>? _grupos;

    private PageSwipe? _swipeAbierta;
    private bool _cargando;

    #endregion "Variables"

    #region "Eventos"

    #region "Eventos de Pagina"

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!Environment.IsExternalStorageManager) return;

        // Al volver del swipe se aplican los borrados sobre el modelo que ya tenemos.
        // Escanear otra vez /storage/emulated/0 entero solo para enterarnos de N bajas no compensa.
        if (_swipeAbierta is not null)
        {
            AplicarEliminaciones(_swipeAbierta);
            _swipeAbierta = null;

            ActualizarContadores();
        }
        else if (_grupos is not null)
        {
            ActualizarContadores();
        }
        else
        {
            await CargarDatos();
        }

        ActualizarEstadoVacio();

        await ActualizarPapeleraAsync();
    }

    #endregion "Eventos de Pagina"

    #region "Eventos de Refresco"

    private async void Refresco_OnRefreshing(object? sender, EventArgs e)
    {
        try
        {
            await CargarDatos();
        }
        finally
        {
            Refresco.IsRefreshing = false;
        }
    }

    #endregion "Eventos de Refresco"

    #region "Eventos de Boton"

    private async void BtnRecuperar_OnClicked(object? sender, EventArgs e)
    {
        try
        {
            ResumenPapelera resumen = await Task.Run(MdPapelera.Resumen);
            if (resumen.Vacia) return;

            bool confirmado = await DisplayAlert(
                "Recuperar todo",
                $"Se devolverán {DescribirFotos(resumen.Fotos)} ({FormatearBytes(resumen.Bytes)}) a sus carpetas originales.",
                "Recuperar",
                "Cancelar");

            if (!confirmado) return;

            HabilitarBotonesPapelera(false);

            ResumenPapelera recuperado = await Task.Run(MdPapelera.RestaurarTodo);

            MdUtilidades.SumarBytesAhorrados(-recuperado.Bytes);
            MdUtilidades.SumarImagenesEliminadas(-recuperado.Fotos);

            MdUtilidades.MostrarToast(recuperado.Fotos == 1
                ? "1 foto recuperada"
                : $"{recuperado.Fotos} fotos recuperadas");

            // Las fotos vuelven a sus carpetas: la caché en memoria ya no vale, toca reescanear
            await CargarDatos();
            await ActualizarPapeleraAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error recuperando la papelera: {ex.Message}");
            MdUtilidades.MostrarToast("Error al recuperar las fotos");
        }
        finally
        {
            HabilitarBotonesPapelera(true);
        }
    }

    private async void BtnVaciar_OnClicked(object? sender, EventArgs e)
    {
        try
        {
            ResumenPapelera resumen = await Task.Run(MdPapelera.Resumen);
            if (resumen.Vacia) return;

            // Esto sí es irreversible, a diferencia del swipe
            bool confirmado = await DisplayAlert(
                "Vaciar papelera",
                $"Se borrarán definitivamente {DescribirFotos(resumen.Fotos)} ({FormatearBytes(resumen.Bytes)}). Esto no se puede deshacer.",
                "Vaciar",
                "Cancelar");

            if (!confirmado) return;

            HabilitarBotonesPapelera(false);

            int borradas = await Task.Run(MdPapelera.Vaciar);

            await ActualizarPapeleraAsync();

            MdUtilidades.MostrarToast(borradas == 1
                ? "1 foto borrada definitivamente"
                : $"{borradas} fotos borradas definitivamente");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error vaciando la papelera: {ex.Message}");
            MdUtilidades.MostrarToast("Error al vaciar la papelera");
        }
        finally
        {
            HabilitarBotonesPapelera(true);
        }
    }

    /// <summary>Vaciar y recuperar se pisarían entre sí: mientras corre uno, los dos fuera.</summary>
    private void HabilitarBotonesPapelera(bool habilitados)
    {
        BtnRecuperar.IsEnabled = habilitados;
        BtnVaciar.IsEnabled = habilitados;
    }

    #endregion "Eventos de Boton"

    #region "Gesture Recognizers"

    private async void TapGestureRecognizer_OnTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (sender is not BindableObject view || view.BindingContext is not GrupoMes mes) return;
            if (mes.Fotos.Count == 0) return;

            _swipeAbierta = new PageSwipe(mes);
            await Navigation.PushModalAsync(_swipeAbierta);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            MdUtilidades.MostrarToast("Error al abrir la página de swipe");
        }
    }

    #endregion "Gesture Recognizers"

    #endregion "Eventos"

    #region "Funciones"

    /// <summary>Escaneo completo del almacenamiento. Solo en el primer arranque o al tirar para refrescar.</summary>
    private async Task CargarDatos()
    {
        if (_cargando) return;
        _cargando = true;

        try
        {
            // Cancelar las miniaturas pendientes de una carga anterior antes de reemplazar la lista
            _ctsMiniaturas?.Cancel();
            _ctsMiniaturas?.Dispose();
            _ctsMiniaturas = new CancellationTokenSource();

            CancellationToken token = _ctsMiniaturas.Token;

            // Sin token: si se cancelara, Task.Run dejaría una excepción sin observar
            _ = Task.Run(() => MdPapelera.Purgar(RetencionPapelera));

            PanelVacio.IsVisible = false;
            MostrarIndicador(true);

            ActualizarContadores();

            await Task.Yield();

            // Progress<T> publica en el hilo donde se construye, o sea este
            Progress<ProgresoEscaneo> progreso = new(ReportarProgreso);

            List<FotoItem> fotos = await Task.Run(
                () => MdUtilidades.ObtenerFotos(MdUtilidades.RutaAlmacenamiento, progreso), token);

            MdUtilidades.SetTotalFotos(fotos.Count);

            LblProgreso.Text = "Ordenando…";
            LblProgresoCarpeta.Text = string.Empty;

            List<GrupoAño> grupos = await Task.Run(() => MdUtilidades.AgruparFotos(fotos), token);

            _grupos = new ObservableCollection<GrupoAño>(grupos);
            cv.ItemsSource = _grupos;

            ActualizarContadores();

            MostrarIndicador(false);
            _cargando = false;
            ActualizarEstadoVacio();

            // Las miniaturas van aparte: la lista ya se puede usar mientras se generan
            _ = CargarMiniaturasAsync(_grupos, token);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            MdUtilidades.MostrarToast("Error al cargar las fotos");

            MostrarIndicador(false);
        }
        finally
        {
            _cargando = false;
        }
    }

    private void ReportarProgreso(ProgresoEscaneo progreso)
    {
        // Puede llegar cuando el escaneo ya ha terminado; entonces no pisamos el texto
        if (!_cargando) return;

        LblProgreso.Text = progreso.Fotos == 1
            ? "1 foto encontrada"
            : $"{progreso.Fotos:N0} fotos encontradas";

        LblProgresoCarpeta.Text = progreso.Carpeta;
    }

    private void MostrarIndicador(bool visible)
    {
        BorderIndicator.IsVisible = visible;
        Indicator.IsVisible = visible;
        Indicator.IsRunning = visible;

        if (!visible) return;

        LblProgreso.Text = "Buscando fotos…";
        LblProgresoCarpeta.Text = string.Empty;
    }

    /// <summary>Quita del modelo las fotos que el swipe mandó a la papelera, sin volver a escanear.</summary>
    private void AplicarEliminaciones(PageSwipe swipe)
    {
        if (_grupos is null || swipe.Eliminadas.Count == 0) return;

        GrupoMes mes = swipe.Mes;
        HashSet<string> borradas = new(swipe.Eliminadas.Select(f => f.Ruta), StringComparer.Ordinal);

        FotoItem? previaAnterior = mes.FotoPreview;

        mes.Fotos.RemoveAll(f => borradas.Contains(f.Ruta));
        mes.NotificarCambios();

        GrupoAño? año = _grupos.FirstOrDefault(a => a.Meses.Contains(mes));

        if (mes.Fotos.Count == 0)
            año?.Meses.Remove(mes);
        else if (!ReferenceEquals(previaAnterior, mes.FotoPreview))
            _ = RefrescarMiniaturaAsync(mes); // la portada del mes ya no existe

        año?.NotificarCambios();

        if (año is not null && año.Meses.Count == 0)
            _grupos.Remove(año);
    }

    /// <summary>Refresca la fila de papelera. Se oculta entera cuando no queda nada por purgar.</summary>
    private async Task ActualizarPapeleraAsync()
    {
        try
        {
            ResumenPapelera resumen = await Task.Run(MdPapelera.Resumen);

            PanelPapelera.IsVisible = !resumen.Vacia;
            if (resumen.Vacia) return;

            LblPapelera.Text = $"{FormatearBytes(resumen.Bytes)} · {DescribirFotos(resumen.Fotos)}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error leyendo la papelera: {ex.Message}");
        }
    }

    private static string DescribirFotos(int cantidad)
    {
        return cantidad == 1 ? "1 foto" : $"{cantidad} fotos";
    }

    /// <summary>Cubre también el caso de vaciar el carrete a base de swipes, sin reescanear.</summary>
    private void ActualizarEstadoVacio()
    {
        PanelVacio.IsVisible = !_cargando && _grupos is not null && _grupos.Count == 0;
    }

    private void ActualizarContadores()
    {
        LblAhorro.Text = FormatearBytes(MdUtilidades.ObtenerBytesAhorrados());
        LblEliminadas.Text = MdUtilidades.ObtenerImagenesEliminadas().ToString();
        LblFotosEncontradas.Text = (_grupos?.Sum(a => a.Count) ?? MdUtilidades.ObtenerTotalFotos()).ToString();
    }

    /// <summary>
    /// Genera las miniaturas de una en una en segundo plano y las va publicando en la UI.
    /// La primera vez cuesta; después salen de la caché en disco.
    /// </summary>
    private static async Task CargarMiniaturasAsync(ObservableCollection<GrupoAño> años, CancellationToken token)
    {
        try
        {
            // Copia: la lista puede cambiar mientras esto corre si se borran fotos
            List<GrupoMes> meses = años.SelectMany(a => a.Meses).ToList();

            foreach (GrupoMes mes in meses)
            {
                if (token.IsCancellationRequested) return;

                FotoItem? preview = mes.FotoPreview;
                if (preview is null) continue;

                string? miniatura = await MdMiniaturas.ObtenerAsync(preview, token);
                if (miniatura is null || token.IsCancellationRequested) continue;

                MainThread.BeginInvokeOnMainThread(() => mes.Miniatura = miniatura);
            }
        }
        catch (System.OperationCanceledException)
        {
            // Recarga en curso, no hay nada que hacer
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cargando miniaturas: {ex.Message}");
        }
    }

    private static async Task RefrescarMiniaturaAsync(GrupoMes mes)
    {
        FotoItem? preview = mes.FotoPreview;
        if (preview is null) return;

        string? miniatura = await MdMiniaturas.ObtenerAsync(preview);
        if (miniatura is null) return;

        MainThread.BeginInvokeOnMainThread(() => mes.Miniatura = miniatura);
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
