namespace SwipeAndBye.Clases;

/// <summary>Avance del escaneo. No hay porcentaje: el total no se sabe hasta terminar.</summary>
public readonly record struct ProgresoEscaneo(int Fotos, string Carpeta);
