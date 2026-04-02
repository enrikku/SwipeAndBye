namespace SwipeAndBye.Clases;

public class GrupoMes
{
    public int Mes { get; set; }
    public List<FotoItem> Fotos { get; set; }
    public int Count => Fotos?.Count ?? 0;

    public string NombreMes =>
        CultureInfo.CurrentCulture.TextInfo.ToTitleCase(new DateTime(1, Mes, 1).ToString("MMMM"));

    public string FotoPreview => Fotos?.FirstOrDefault()?.Ruta;
}