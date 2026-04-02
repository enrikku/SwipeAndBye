namespace SwipeAndBye.Modulos;

public static class MdUtilidades
{
    private const string KEY_BYTES_AHORRADOS = "bytes_ahorrados";
    private const string KEY_FOTOS_ELIMINADAS = "fotos_eliminadas";
    private const string KEY_TOTAL_FOTOS = "total_fotos";

    private static readonly HashSet<string> ExtensionesImagen = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    public static List<FotoItem> ObtenerFotos(string rootPath)
    {
        List<FotoItem> resultado = new();
        AnalizarDirectorio(rootPath, resultado);
        return resultado;
    }

    private static void AnalizarDirectorio(string path, List<FotoItem> resultado)
    {
        try
        {
            if (path.Contains("WhatsApp Backup Excluded Stickers", StringComparison.OrdinalIgnoreCase))
                return;

            // Ignorar carpetas ocultas/sistema
            FileAttributes dirAttributes = File.GetAttributes(path);
            if (dirAttributes.HasFlag(FileAttributes.Hidden) || dirAttributes.HasFlag(FileAttributes.System))
                return;

            // Archivos
            var files = Array.Empty<string>();
            try
            {
                files = Directory.GetFiles(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No se pudo acceder a archivos en {path}: {ex.Message}");
            }

            foreach (var file in files)
                try
                {
                    FileAttributes fileAttributes = File.GetAttributes(file);

                    if (fileAttributes.HasFlag(FileAttributes.Hidden) || fileAttributes.HasFlag(FileAttributes.System))
                        continue;

                    var ext = Path.GetExtension(file);
                    if (!ExtensionesImagen.Contains(ext))
                        continue;

                    FileInfo info = new(file);

                    resultado.Add(new FotoItem
                    {
                        Ruta = file,
                        Nombre = info.Name,
                        Fecha = info.LastWriteTime,
                        Tamaño = info.Length
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error archivo {file}: {ex.Message}");
                }

            // Subdirectorios
            var directories = Array.Empty<string>();
            try
            {
                directories = Directory.GetDirectories(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No se pudo acceder a carpetas en {path}: {ex.Message}");
            }

            foreach (var dir in directories) AnalizarDirectorio(dir, resultado);
        }
        catch (UnauthorizedAccessException)
        {
            // ignorar
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en {path}: {ex.Message}");
        }
    }

    public static List<GrupoAño> AgruparFotos(List<FotoItem> fotos)
    {
        List<GrupoAño> resultado = fotos
            .GroupBy(f => f.Fecha.Year)
            .Select(grupoAño => new GrupoAño
            {
                Año = grupoAño.Key,
                Meses = grupoAño
                    .GroupBy(f => f.Fecha.Month)
                    .Select(grupoMes => new GrupoMes
                    {
                        Mes = grupoMes.Key,
                        Fotos = grupoMes
                            .OrderByDescending(f => f.Fecha)
                            .ToList()
                    })
                    .OrderByDescending(m => m.Mes)
                    .ToList()
            })
            .OrderByDescending(a => a.Año)
            .ToList();

        return resultado;
    }

    public static long ObtenerBytesAhorrados()
    {
        return Preferences.Get(KEY_BYTES_AHORRADOS, 0L);
    }

    public static void SumarBytesAhorrados(long bytes)
    {
        var actual = Preferences.Get(KEY_BYTES_AHORRADOS, 0L);
        var nuevo = actual + bytes;

        Preferences.Set(KEY_BYTES_AHORRADOS, nuevo);
    }

    public static int ObtenerImagenesEliminadas()
    {
        return Preferences.Get(KEY_FOTOS_ELIMINADAS, 0);
    }

    public static void SumarImagenesEliminadas()
    {
        var actual = Preferences.Get(KEY_FOTOS_ELIMINADAS, 0);
        actual++;

        Preferences.Set(KEY_FOTOS_ELIMINADAS, actual);
    }

    public static int ObtenerTotalFotos()
    {
        return Preferences.Get(KEY_TOTAL_FOTOS, 0);
    }

    public static void RestarTotalFotos()
    {
        var actual = Preferences.Get(KEY_TOTAL_FOTOS, 0);
        actual--;

        Preferences.Set(KEY_TOTAL_FOTOS, actual);
    }

    public static void SetTotalFotos(int total)
    {
        Preferences.Set(KEY_TOTAL_FOTOS, total);
    }
}