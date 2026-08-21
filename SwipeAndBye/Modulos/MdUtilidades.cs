using Android.App;

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

    /// <summary>Raíz del almacenamiento compartido. La papelera vive aquí para que mover sea un rename.</summary>
    public static string RutaAlmacenamiento =>
        Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "/storage/emulated/0";

    public static List<FotoItem> ObtenerFotos(string rootPath, IProgress<ProgresoEscaneo>? progreso = null)
    {
        EstadoEscaneo estado = new(progreso);
        AnalizarDirectorio(rootPath, estado);

        return estado.Fotos;
    }

    /// <summary>Acumula el resultado del escaneo y va avisando del avance sin saturar la UI.</summary>
    private sealed class EstadoEscaneo(IProgress<ProgresoEscaneo>? progreso)
    {
        private const int MilisegundosEntreAvisos = 200;

        private DateTime _ultimoAviso = DateTime.MinValue;

        public List<FotoItem> Fotos { get; } = [];

        public void Reportar(string carpeta)
        {
            if (progreso is null) return;

            DateTime ahora = DateTime.UtcNow;
            if ((ahora - _ultimoAviso).TotalMilliseconds < MilisegundosEntreAvisos) return;

            _ultimoAviso = ahora;
            progreso.Report(new ProgresoEscaneo(Fotos.Count, carpeta));
        }
    }

    private static void AnalizarDirectorio(string path, EstadoEscaneo estado)
    {
        try
        {
            if (path.Contains("WhatsApp Backup Excluded Stickers", StringComparison.OrdinalIgnoreCase))
                return;

            // Nuestra papelera ya es oculta, pero mejor no depender solo de eso
            if (path.Contains(MdPapelera.NombreCarpeta, StringComparison.OrdinalIgnoreCase))
                return;

            // Ignorar carpetas ocultas/sistema
            FileAttributes dirAttributes = File.GetAttributes(path);
            if (dirAttributes.HasFlag(FileAttributes.Hidden) || dirAttributes.HasFlag(FileAttributes.System))
                return;

            // Archivos
            string[] files = Array.Empty<string>();
            try
            {
                files = Directory.GetFiles(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No se pudo acceder a archivos en {path}: {ex.Message}");
            }

            foreach (string file in files)
                try
                {
                    FileAttributes fileAttributes = File.GetAttributes(file);

                    if (fileAttributes.HasFlag(FileAttributes.Hidden) || fileAttributes.HasFlag(FileAttributes.System))
                        continue;

                    string ext = Path.GetExtension(file);
                    if (!ExtensionesImagen.Contains(ext))
                        continue;

                    FileInfo info = new(file);

                    estado.Fotos.Add(new FotoItem
                    {
                        Ruta = file,
                        Nombre = info.Name,

                        // La fecha del archivo cambia al copiar, mover o restaurar un backup.
                        // La del EXIF es la de la toma, que es por la que agrupamos.
                        Fecha = MdExif.ObtenerFecha(file) ?? info.LastWriteTime,

                        Tamaño = info.Length
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error archivo {file}: {ex.Message}");
                }

            estado.Reportar(Path.GetFileName(path));

            // Subdirectorios
            string[] directories = Array.Empty<string>();
            try
            {
                directories = Directory.GetDirectories(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No se pudo acceder a carpetas en {path}: {ex.Message}");
            }

            foreach (string dir in directories) AnalizarDirectorio(dir, estado);
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
                Meses = new ObservableCollection<GrupoMes>(grupoAño
                    .GroupBy(f => f.Fecha.Month)
                    .Select(grupoMes => new GrupoMes
                    {
                        Mes = grupoMes.Key,
                        Fotos = grupoMes
                            .OrderByDescending(f => f.Fecha)
                            .ToList()
                    })
                    .OrderByDescending(m => m.Mes))
            })
            .OrderByDescending(a => a.Año)
            .ToList();

        return resultado;
    }

    public static long ObtenerBytesAhorrados()
    {
        return Preferences.Get(KEY_BYTES_AHORRADOS, 0L);
    }

    /// <summary>Acepta deltas negativos: al deshacer un borrado hay que descontar.</summary>
    public static void SumarBytesAhorrados(long bytes)
    {
        long nuevo = Math.Max(0, Preferences.Get(KEY_BYTES_AHORRADOS, 0L) + bytes);

        Preferences.Set(KEY_BYTES_AHORRADOS, nuevo);
    }

    public static int ObtenerImagenesEliminadas()
    {
        return Preferences.Get(KEY_FOTOS_ELIMINADAS, 0);
    }

    public static void SumarImagenesEliminadas(int cantidad = 1)
    {
        int nuevo = Math.Max(0, Preferences.Get(KEY_FOTOS_ELIMINADAS, 0) + cantidad);

        Preferences.Set(KEY_FOTOS_ELIMINADAS, nuevo);
    }

    public static int ObtenerTotalFotos()
    {
        return Preferences.Get(KEY_TOTAL_FOTOS, 0);
    }

    public static void SumarTotalFotos(int cantidad)
    {
        int nuevo = Math.Max(0, Preferences.Get(KEY_TOTAL_FOTOS, 0) + cantidad);

        Preferences.Set(KEY_TOTAL_FOTOS, nuevo);
    }

    public static void SetTotalFotos(int total)
    {
        Preferences.Set(KEY_TOTAL_FOTOS, total);
    }

    /// <summary>Muestra un Toast comprobando que haya actividad y siempre desde el hilo de UI.</summary>
    public static void MostrarToast(string mensaje)
    {
        Activity? actividad = Platform.CurrentActivity;
        if (actividad is null) return;

        MainThread.BeginInvokeOnMainThread(() => Toast.MakeText(actividad, mensaje, ToastLength.Short)?.Show());
    }
}