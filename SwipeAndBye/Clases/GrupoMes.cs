namespace SwipeAndBye.Clases;

public class GrupoMes : INotifyPropertyChanged
{
    public int Mes { get; set; }
    public List<FotoItem> Fotos { get; set; } = [];

    public int Count => Fotos.Count;

    public string NombreMes =>
        CultureInfo.CurrentCulture.TextInfo.ToTitleCase(new DateTime(1, Mes, 1).ToString("MMMM"));

    public FotoItem? FotoPreview => Fotos.FirstOrDefault();

    /// <summary>Lo que lee TalkBack en la tarjeta: la miniatura sola no dice nada.</summary>
    public string Descripcion => Count == 1 ? $"{NombreMes}, 1 foto" : $"{NombreMes}, {Count} fotos";

    private string? _miniatura;

    /// <summary>Ruta de la miniatura cacheada. Se rellena en segundo plano tras cargar los grupos.</summary>
    public string? Miniatura
    {
        get => _miniatura;
        set
        {
            if (_miniatura == value) return;

            _miniatura = value;
            OnPropertyChanged(nameof(Miniatura));
        }
    }

    /// <summary>A llamar tras tocar <see cref="Fotos" /> a mano, para refrescar el contador de la tarjeta.</summary>
    public void NotificarCambios()
    {
        OnPropertyChanged(nameof(Count));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propiedad)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
    }
}
