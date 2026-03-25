namespace SwipeAndBye.Clases;

public class FotoItem
{
    public string Ruta { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.MinValue;
    public long Tamaño { get; set; } = 0;
}