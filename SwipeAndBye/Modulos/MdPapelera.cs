using Path = System.IO.Path;

namespace SwipeAndBye.Modulos;

/// <summary>
/// Papelera propia: en vez de borrar, mueve el archivo a una carpeta oculta del mismo volumen.
/// Al ser el mismo volumen el movimiento es un rename instantáneo, y deshacer es otro rename.
/// </summary>
public static class MdPapelera
{
    #region "Constantes"

    public const string NombreCarpeta = ".SwipeAndBye";

    /// <summary>Donde acaban las fotos sin ruta original conocida al recuperarlas.</summary>
    public const string CarpetaRescate = "SwipeAndBye recuperadas";

    /// <summary>El punto inicial la marca como oculta, así el escaneo y la galería la ignoran.</summary>
    public static string Directorio => Path.Combine(MdUtilidades.RutaAlmacenamiento, NombreCarpeta, "papelera");

    /// <summary>
    /// Un archivo por foto con su ruta original. Va en subcarpeta a propósito: <c>GetFiles</c> no
    /// recursa, así el resto del módulo sigue viendo solo fotos sin filtrar nada.
    /// </summary>
    private static string DirectorioRutas => Path.Combine(Directorio, ".rutas");

    #endregion "Constantes"

    #region "Funciones"

    /// <summary>Mueve la foto a la papelera. Devuelve null si no se ha podido (la foto se queda donde está).</summary>
    public static EntradaPapelera? Mover(FotoItem foto)
    {
        try
        {
            if (!File.Exists(foto.Ruta)) return null;

            Directory.CreateDirectory(Directorio);
            AsegurarNoMedia();

            long tamaño = new FileInfo(foto.Ruta).Length;

            // El prefijo de ticks marca CUÁNDO se borró (el mtime del archivo es el de la foto y hay
            // que conservarlo, porque de él sale la agrupación por año/mes al restaurar).
            // El GUID evita choques entre dos IMG_0001.jpg de carpetas distintas.
            string nombre = $"{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}_{Path.GetFileName(foto.Ruta)}";
            string destino = Path.Combine(Directorio, nombre);

            File.SetAttributes(foto.Ruta, FileAttributes.Normal);
            File.Move(foto.Ruta, destino);

            // Sin esto, "Recuperar todo" no sabría a qué carpeta devolverla
            GuardarRutaOriginal(destino, foto.Ruta);

            // La papelera está oculta y con .nomedia, así que solo avisamos de la ruta que se
            // ha quedado vacía: si no, la galería sigue mostrando la foto como un hueco roto
            NotificarGaleria(foto.Ruta);

            return new EntradaPapelera
            {
                Foto = foto,
                RutaPapelera = destino,
                Tamaño = tamaño
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error moviendo a la papelera {foto.Ruta}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Devuelve la foto a su ruta original.</summary>
    public static bool Restaurar(EntradaPapelera entrada)
    {
        try
        {
            if (!File.Exists(entrada.RutaPapelera)) return false;

            // Si mientras tanto ha aparecido otro archivo con ese nombre no lo pisamos
            if (File.Exists(entrada.Foto.Ruta)) return false;

            string? carpeta = Path.GetDirectoryName(entrada.Foto.Ruta);
            if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);

            File.Move(entrada.RutaPapelera, entrada.Foto.Ruta);
            BorrarSidecar(entrada.RutaPapelera);

            // Para que vuelva a aparecer en la galería sin esperar al escaneo del sistema
            NotificarGaleria(entrada.Foto.Ruta);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error restaurando {entrada.RutaPapelera}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Devuelve toda la papelera a sus carpetas originales. Best-effort: lo que no se pueda
    /// colocar en su sitio se queda, el resto vuelve. Devuelve lo que sí se ha recuperado.
    /// </summary>
    public static ResumenPapelera RestaurarTodo()
    {
        int fotos = 0;
        long bytes = 0;
        List<string> recuperadas = [];

        try
        {
            if (!Directory.Exists(Directorio)) return new ResumenPapelera(0, 0);

            foreach (string archivo in Directory.GetFiles(Directorio))
                try
                {
                    if (FechaBorrado(archivo) is null) continue;

                    long tamaño = new FileInfo(archivo).Length;

                    string? destino = RestaurarArchivo(archivo);
                    if (destino is null) continue;

                    recuperadas.Add(destino);
                    fotos++;
                    bytes += tamaño;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error recuperando {archivo}: {ex.Message}");
                }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error recuperando la papelera: {ex.Message}");
        }

        // Un solo aviso con todas: cientos de llamadas sueltas saturarían el escáner
        NotificarGaleria([.. recuperadas]);

        return new ResumenPapelera(fotos, bytes);
    }

    /// <summary>Devuelve la ruta donde ha quedado la foto, o null si no se ha podido recuperar.</summary>
    private static string? RestaurarArchivo(string rutaPapelera)
    {
        // A diferencia del deshacer (que apunta a una foto concreta y prefiere fallar), aquí
        // buscamos un nombre libre: es preferible un "IMG (1).jpg" a dejar la foto atrapada.
        string destino = RutaLibre(LeerRutaOriginal(rutaPapelera) ?? RutaDeRescate(rutaPapelera));

        string? carpeta = Path.GetDirectoryName(destino);
        if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);

        File.SetAttributes(rutaPapelera, FileAttributes.Normal);
        File.Move(rutaPapelera, destino);
        BorrarSidecar(rutaPapelera);

        return destino;
    }

    /// <summary>
    /// Reindexa esas rutas en el MediaStore. Sin esto la galería sigue enseñando las borradas
    /// como huecos rotos, y las recuperadas tardan en volver a aparecer.
    /// </summary>
    private static void NotificarGaleria(params string[] rutas)
    {
        try
        {
            if (rutas.Length == 0) return;

            Context? contexto = Platform.AppContext;
            if (contexto is null) return;

            Android.Media.MediaScannerConnection.ScanFile(contexto, rutas, null, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error avisando a la galería: {ex.Message}");
        }
    }

    /// <summary>
    /// Borra definitivamente lo que lleve más de <paramref name="antiguedad" /> en la papelera.
    /// Devuelve cuántos archivos se han borrado.
    /// </summary>
    public static int Purgar(TimeSpan antiguedad)
    {
        int borradas = 0;

        try
        {
            if (!Directory.Exists(Directorio)) return 0;

            DateTime limite = DateTime.UtcNow - antiguedad;

            foreach (string archivo in Directory.GetFiles(Directorio))
                try
                {
                    // FechaBorrado null = no lo puso esta papelera (el .nomedia, por ejemplo)
                    DateTime? borradoEl = FechaBorrado(archivo);
                    if (borradoEl is null || borradoEl > limite) continue;

                    File.SetAttributes(archivo, FileAttributes.Normal);
                    File.Delete(archivo);
                    BorrarSidecar(archivo);

                    borradas++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error purgando {archivo}: {ex.Message}");
                }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error purgando la papelera: {ex.Message}");
        }

        return borradas;
    }

    /// <summary>Borra definitivamente TODA la papelera. Sin vuelta atrás: confirmar antes de llamar.</summary>
    public static int Vaciar()
    {
        return Purgar(TimeSpan.Zero);
    }

    /// <summary>Fotos y bytes que la papelera sigue ocupando (borrados pero aún no purgados).</summary>
    public static ResumenPapelera Resumen()
    {
        try
        {
            if (!Directory.Exists(Directorio)) return new ResumenPapelera(0, 0);

            int fotos = 0;
            long bytes = 0;

            foreach (string archivo in Directory.GetFiles(Directorio))
            {
                if (FechaBorrado(archivo) is null) continue;

                fotos++;
                bytes += new FileInfo(archivo).Length;
            }

            return new ResumenPapelera(fotos, bytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error midiendo la papelera: {ex.Message}");
            return new ResumenPapelera(0, 0);
        }
    }

    private static string RutaSidecar(string rutaPapelera)
    {
        return Path.Combine(DirectorioRutas, Path.GetFileName(rutaPapelera) + ".txt");
    }

    private static void GuardarRutaOriginal(string rutaPapelera, string rutaOriginal)
    {
        try
        {
            Directory.CreateDirectory(DirectorioRutas);
            File.WriteAllText(RutaSidecar(rutaPapelera), rutaOriginal);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error guardando la ruta de {rutaPapelera}: {ex.Message}");
        }
    }

    private static string? LeerRutaOriginal(string rutaPapelera)
    {
        try
        {
            string sidecar = RutaSidecar(rutaPapelera);
            if (!File.Exists(sidecar)) return null;

            string ruta = File.ReadAllText(sidecar).Trim();

            return string.IsNullOrEmpty(ruta) ? null : ruta;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error leyendo la ruta de {rutaPapelera}: {ex.Message}");
            return null;
        }
    }

    private static void BorrarSidecar(string rutaPapelera)
    {
        try
        {
            string sidecar = RutaSidecar(rutaPapelera);
            if (File.Exists(sidecar)) File.Delete(sidecar);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error borrando la ruta de {rutaPapelera}: {ex.Message}");
        }
    }

    /// <summary>
    /// Destino para fotos sin ruta original: las que mandó a la papelera una versión anterior,
    /// que no guardaba de dónde venían. Carpeta visible, para que no se pierdan.
    /// </summary>
    private static string RutaDeRescate(string rutaPapelera)
    {
        string nombre = Path.GetFileName(rutaPapelera);

        // Quitar el prefijo "{ticks}_{guid}_" y quedarnos con el nombre original
        int primero = nombre.IndexOf('_');
        int segundo = primero < 0 ? -1 : nombre.IndexOf('_', primero + 1);
        string original = segundo < 0 ? nombre : nombre[(segundo + 1)..];

        return Path.Combine(MdUtilidades.RutaAlmacenamiento, CarpetaRescate, original);
    }

    /// <summary>Si el destino está ocupado, busca "nombre (1).jpg", "nombre (2).jpg"...</summary>
    private static string RutaLibre(string ruta)
    {
        if (!File.Exists(ruta)) return ruta;

        string carpeta = Path.GetDirectoryName(ruta) ?? Directorio;
        string nombre = Path.GetFileNameWithoutExtension(ruta);
        string extension = Path.GetExtension(ruta);

        for (int i = 1; i < 1000; i++)
        {
            string candidata = Path.Combine(carpeta, $"{nombre} ({i}){extension}");
            if (!File.Exists(candidata)) return candidata;
        }

        return Path.Combine(carpeta, $"{nombre} ({Guid.NewGuid():N}){extension}");
    }

    /// <summary>Lee el prefijo de ticks del nombre. Null si el archivo no lo puso esta papelera.</summary>
    private static DateTime? FechaBorrado(string archivo)
    {
        string nombre = Path.GetFileName(archivo);

        int separador = nombre.IndexOf('_');
        if (separador <= 0) return null;

        if (!long.TryParse(nombre[..separador], out long ticks)) return null;
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return null;

        return new DateTime(ticks, DateTimeKind.Utc);
    }

    /// <summary>Para que Google Fotos y la galería no indexen lo que hay en la papelera.</summary>
    private static void AsegurarNoMedia()
    {
        try
        {
            string marca = Path.Combine(Directorio, ".nomedia");
            if (!File.Exists(marca)) File.Create(marca).Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creando .nomedia: {ex.Message}");
        }
    }

    #endregion "Funciones"
}
