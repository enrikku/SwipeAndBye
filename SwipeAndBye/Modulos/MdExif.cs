using Path = System.IO.Path;

namespace SwipeAndBye.Modulos;

/// <summary>Lectura de metadatos EXIF: cuándo se hizo la foto y en qué orientación.</summary>
public static class MdExif
{
    #region "Constantes"

    // Literales en vez de las constantes del binding: los nombres varían entre versiones de la
    // API y algunas no existen en Android.Media.ExifInterface. El valor de la etiqueta sí es fijo.
    private const string TagFechaOriginal = "DateTimeOriginal";
    private const string TagFecha = "DateTime";
    private const string TagOrientacion = "Orientation";

    private const string FormatoFechaExif = "yyyy:MM:dd HH:mm:ss";

    /// <summary>Solo estos formatos llevan EXIF. Abrir un PNG a buscarlo es E/S tirada.</summary>
    private static readonly HashSet<string> ConExif = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg"
    };

    #endregion "Constantes"

    #region "Funciones"

    /// <summary>
    /// Fecha real de la toma. Null si la foto no lleva EXIF o no se puede leer; entonces
    /// hay que caer en la fecha del archivo, que es mucho menos fiable.
    /// </summary>
    public static DateTime? ObtenerFecha(string ruta)
    {
        try
        {
            if (!ConExif.Contains(Path.GetExtension(ruta))) return null;

            Android.Media.ExifInterface exif = new(ruta);

            string? valor = exif.GetAttribute(TagFechaOriginal) ?? exif.GetAttribute(TagFecha);
            if (string.IsNullOrWhiteSpace(valor)) return null;

            bool ok = DateTime.TryParseExact(
                valor,
                FormatoFechaExif,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime fecha);

            if (!ok) return null;

            // Cámaras sin reloj en hora dejan ceros o fechas de 1970: no sirven para agrupar
            return fecha.Year < 1990 ? null : fecha;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error leyendo la fecha EXIF de {ruta}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Grados que hay que rotar la imagen decodificada (BitmapFactory ignora el EXIF).</summary>
    public static int ObtenerGrados(string ruta)
    {
        try
        {
            if (!ConExif.Contains(Path.GetExtension(ruta))) return 0;

            Android.Media.ExifInterface exif = new(ruta);
            int orientacion = exif.GetAttributeInt(TagOrientacion, 1);

            return orientacion switch
            {
                6 => 90,
                3 => 180,
                8 => 270,
                _ => 0
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error leyendo la orientación EXIF de {ruta}: {ex.Message}");
            return 0;
        }
    }

    #endregion "Funciones"
}
