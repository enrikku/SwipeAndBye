namespace SwipeAndBye.Clases;

/// <summary>Una foto movida a la papelera, con lo necesario para devolverla a su sitio.</summary>
public class EntradaPapelera
{
    public required FotoItem Foto { get; init; }
    public required string RutaPapelera { get; init; }
    public long Tamaño { get; init; }
}
