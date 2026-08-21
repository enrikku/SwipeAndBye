namespace SwipeAndBye.Clases;

public class GrupoAño : INotifyPropertyChanged
{
    public int Año { get; set; }

    /// <summary>Observable: al vaciarse un mes se quita de aquí y la rejilla se refresca sola.</summary>
    public ObservableCollection<GrupoMes> Meses { get; set; } = [];

    public int Count => Meses.Sum(m => m.Count);

    /// <summary>A llamar tras quitar fotos o meses, para refrescar el contador de la cabecera.</summary>
    public void NotificarCambios()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
