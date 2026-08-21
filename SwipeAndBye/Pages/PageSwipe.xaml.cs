namespace SwipeAndBye.Pages;

public partial class PageSwipe
{
    #region Constructor

    public PageSwipe(GrupoMes mes)
    {
        InitializeComponent();

        Mes = mes;

        // Copia: el modelo de MainPage no se toca hasta que se cierra esta página
        _fotos = new List<FotoItem>(mes.Fotos);
    }

    #endregion Constructor

    #region "Variables"

    private const double UmbralSwipe = 120;
    private const uint DuracionAnimacion = 200;

    private readonly List<FotoItem> _fotos;
    private readonly List<FotoItem> _eliminadas = [];
    private readonly Stack<Movimiento> _historial = new();

    private int _index;
    private double _totalX;

    private bool _iniciado;
    private bool _procesando;
    private bool _finalizado;
    private bool _cerrando;

    /// <summary>Mes que se está revisando. MainPage lo usa para aplicar los borrados sin reescanear.</summary>
    public GrupoMes Mes { get; }

    /// <summary>Fotos que siguen en la papelera al cerrar (las deshechas ya no están aquí).</summary>
    public IReadOnlyList<FotoItem> Eliminadas => _eliminadas;

    /// <summary>Una foto ya resuelta. <see cref="Papelera" /> es null si se conservó.</summary>
    private sealed record Movimiento(FotoItem Foto, EntradaPapelera? Papelera);

    #endregion "Variables"

    #region "Eventos"

    #region "Eventos de Pagina"

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Solo la primera vez: OnAppearing se vuelve a disparar al cerrar dialogos
        if (_iniciado) return;
        _iniciado = true;

        try
        {
            await MostrarActualAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al mostrar la primera foto: {ex.Message}");
            MdUtilidades.MostrarToast("Error al cargar las fotos");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // CerrarAsync captura sus propias excepciones, no queda ninguna sin observar
        _ = CerrarAsync();
        return true;
    }

    #endregion "Eventos de Pagina"

    #region "Eventos de Boton"

    private async void BtnDelete_OnClicked(object? sender, EventArgs e)
    {
        await ProcesarSwipeAsync(false);
    }

    private async void BtnConservar_OnClicked(object? sender, EventArgs e)
    {
        await ProcesarSwipeAsync(true);
    }

    private async void BtnDeshacer_OnTapped(object? sender, TappedEventArgs e)
    {
        await DeshacerAsync();
    }

    #endregion "Eventos de Boton"

    #region "Eventos de Pan"

    private async void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        // Ignorar el gesto mientras se anima un swipe o si ya no quedan fotos
        if (_procesando || _finalizado || _index >= _fotos.Count) return;

        try
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _totalX = 0;
                    break;

                case GestureStatus.Running:
                    _totalX = e.TotalX;

                    ImgActual.TranslationX = _totalX;
                    ImgActual.Rotation = Math.Clamp(_totalX / 20, -15, 15);

                    LblLike.Opacity = Math.Max(0, _totalX / 200);
                    LblDelete.Opacity = Math.Max(0, -_totalX / 200);
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    if (e.StatusType == GestureStatus.Completed && Math.Abs(_totalX) > UmbralSwipe)
                        await ProcesarSwipeAsync(_totalX > 0);
                    else
                        await ResetPositionAsync();

                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en el gesto: {ex.Message}");
            MdUtilidades.MostrarToast("Error al procesar el gesto");
        }
    }

    #endregion "Eventos de Pan"

    #endregion "Eventos"

    #region "Funciones"

    /// <summary>
    /// Punto unico de avance. El guard <see cref="_procesando" /> impide que dos swipes solapados
    /// (gesto + boton, o dos toques rapidos) incrementen el indice dos veces y borren la foto equivocada.
    /// </summary>
    private async Task ProcesarSwipeAsync(bool conservar)
    {
        if (_procesando || _finalizado) return;
        if (_index >= _fotos.Count) return;

        _procesando = true;

        try
        {
            FotoItem foto = _fotos[_index];

            await ImgActual.TranslateTo(conservar ? 1000 : -1000, 0, DuracionAnimacion);

            EntradaPapelera? papelera = null;

            if (!conservar)
            {
                papelera = MdPapelera.Mover(foto);

                if (papelera is null)
                {
                    // No se ha podido mover: la foto sigue donde estaba, no contamos nada
                    MdUtilidades.MostrarToast("No se ha podido borrar la foto");
                }
                else
                {
                    MdUtilidades.SumarBytesAhorrados(papelera.Tamaño);
                    MdUtilidades.SumarImagenesEliminadas();
                    MdUtilidades.SumarTotalFotos(-1);

                    _eliminadas.Add(foto);
                }
            }

            _historial.Push(new Movimiento(foto, papelera));
            _index++;

            await MostrarActualAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error procesando el swipe: {ex.Message}");
            MdUtilidades.MostrarToast("Error al procesar la foto");
        }
        finally
        {
            _procesando = false;
            ActualizarDeshacer();
        }
    }

    /// <summary>Retrocede una foto y, si esa foto se borró, la saca de la papelera.</summary>
    private async Task DeshacerAsync()
    {
        if (_procesando || _finalizado) return;
        if (_historial.Count == 0) return;

        _procesando = true;

        try
        {
            Movimiento ultimo = _historial.Pop();

            if (ultimo.Papelera is not null)
            {
                if (MdPapelera.Restaurar(ultimo.Papelera))
                {
                    MdUtilidades.SumarBytesAhorrados(-ultimo.Papelera.Tamaño);
                    MdUtilidades.SumarImagenesEliminadas(-1);
                    MdUtilidades.SumarTotalFotos(1);

                    _eliminadas.Remove(ultimo.Foto);
                }
                else
                {
                    MdUtilidades.MostrarToast("No se ha podido recuperar la foto");
                }
            }

            _index = Math.Max(0, _index - 1);

            await MostrarActualAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deshaciendo: {ex.Message}");
            MdUtilidades.MostrarToast("Error al deshacer");
        }
        finally
        {
            _procesando = false;
            ActualizarDeshacer();
        }
    }

    private void ActualizarDeshacer()
    {
        bool hay = _historial.Count > 0 && !_finalizado;

        BorderDeshacer.Opacity = hay ? 1 : 0.3;
    }

    private async Task MostrarActualAsync()
    {
        ImgActual.TranslationX = 0;
        ImgActual.Rotation = 0;
        LblLike.Opacity = 0;
        LblDelete.Opacity = 0;

        if (_index >= _fotos.Count)
        {
            ImgActual.Source = null;
            await FinalizarAsync();
            return;
        }

        FotoItem foto = _fotos[_index];

        LblContador.Text = $"{_index + 1} / {_fotos.Count}";
        ImgActual.Source = ImageSource.FromFile(foto.Ruta);

        // TalkBack no puede describir la foto, pero sí decir por cuál vas
        SemanticProperties.SetDescription(ImgActual, $"Foto {_index + 1} de {_fotos.Count}: {foto.Nombre}");
    }

    private async Task FinalizarAsync()
    {
        if (_finalizado) return;
        _finalizado = true;

        LblContador.Text = $"{_fotos.Count} / {_fotos.Count}";

        await DisplayAlert("Fin", "No quedan fotos", "OK");
        await CerrarAsync();
    }

    private async Task CerrarAsync()
    {
        if (_cerrando) return;
        _cerrando = true;

        try
        {
            if (Navigation.ModalStack.Count > 0)
                await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            _cerrando = false;

            Console.WriteLine($"Error al volver atrás: {ex.Message}");
            MdUtilidades.MostrarToast("Error al cerrar la página");
        }
    }

    private async Task ResetPositionAsync()
    {
        await Task.WhenAll(
            ImgActual.TranslateTo(0, 0, DuracionAnimacion, Easing.SinOut),
            ImgActual.RotateTo(0, DuracionAnimacion, Easing.SinOut),
            LblLike.FadeTo(0, DuracionAnimacion),
            LblDelete.FadeTo(0, DuracionAnimacion)
        );
    }

    #endregion "Funciones"
}
