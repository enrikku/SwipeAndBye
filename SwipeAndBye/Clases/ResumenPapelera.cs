namespace SwipeAndBye.Clases;

/// <summary>Lo que la papelera ocupa ahora mismo: fotos borradas pero aún no purgadas.</summary>
public readonly record struct ResumenPapelera(int Fotos, long Bytes)
{
    public bool Vacia => Fotos == 0;
}
