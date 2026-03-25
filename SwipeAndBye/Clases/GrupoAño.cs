namespace SwipeAndBye.Clases;

public class GrupoAño
{
    public int Año { get; set; }
    public List<GrupoMes> Meses { get; set; }

    public int Count => Meses?.Sum(m => m.Count) ?? 0;
}