using System.Security.Cryptography;
using System.Text;

using Android.Graphics;

using Path = System.IO.Path;

namespace SwipeAndBye.Modulos;

/// <summary>
/// Genera miniaturas cacheadas en disco. Evita decodificar los JPEG a resolución completa
/// (una foto de 12 MP ocupa ~48 MB en memoria) solo para pintarlas a 100 px.
/// </summary>
public static class MdMiniaturas
{
    #region "Constantes"

    private const int AnchoDestino = 400;
    private const int AltoDestino = 220;
    private const int CalidadJpeg = 80;

    private static readonly string DirectorioCache = Path.Combine(FileSystem.CacheDirectory, "miniaturas");

    #endregion "Constantes"

    #region "Funciones"

    /// <summary>Devuelve la ruta de la miniatura, generándola en segundo plano si no está cacheada.</summary>
    public static Task<string?> ObtenerAsync(FotoItem foto, CancellationToken token = default)
    {
        return Task.Run(() => Obtener(foto, token), token);
    }

    private static string? Obtener(FotoItem foto, CancellationToken token)
    {
        try
        {
            if (token.IsCancellationRequested) return null;
            if (!File.Exists(foto.Ruta)) return null;

            // La clave incluye fecha y tamaño: si la foto cambia, la miniatura se regenera sola
            string destino = Path.Combine(DirectorioCache, ClaveCache(foto));
            if (File.Exists(destino)) return destino;

            return Generar(foto.Ruta, destino);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generando miniatura de {foto.Ruta}: {ex.Message}");
            return null;
        }
    }

    private static string? Generar(string origen, string destino)
    {
        // 1ª pasada: solo leer dimensiones, sin reservar memoria para el bitmap
        BitmapFactory.Options medida = new() { InJustDecodeBounds = true };

        using (FileStream lectura = File.OpenRead(origen))
        {
            BitmapFactory.DecodeStream(lectura, null, medida);
        }

        if (medida.OutWidth <= 0 || medida.OutHeight <= 0) return null;

        // 2ª pasada: decodificar ya submuestreado
        BitmapFactory.Options opciones = new()
        {
            InSampleSize = CalcularInSampleSize(medida.OutWidth, medida.OutHeight),
            InPreferredConfig = Bitmap.Config.Rgb565
        };

        Bitmap? bitmap;

        using (FileStream lectura = File.OpenRead(origen))
        {
            bitmap = BitmapFactory.DecodeStream(lectura, null, opciones);
        }

        if (bitmap is null) return null;

        try
        {
            bitmap = AplicarRotacion(bitmap, origen);

            Directory.CreateDirectory(DirectorioCache);

            // Escribir a temporal y mover: si el proceso muere a medias no queda un JPEG truncado en caché
            string temporal = destino + ".tmp";

            using (FileStream escritura = File.Create(temporal))
            {
                if (!bitmap.Compress(Bitmap.CompressFormat.Jpeg!, CalidadJpeg, escritura))
                    return null;
            }

            File.Move(temporal, destino, true);

            return destino;
        }
        finally
        {
            bitmap.Recycle();
            bitmap.Dispose();
        }
    }

    /// <summary>Mayor potencia de 2 que deja la imagen por encima del tamaño de destino.</summary>
    private static int CalcularInSampleSize(int ancho, int alto)
    {
        int muestra = 1;

        while (ancho / (muestra * 2) >= AnchoDestino && alto / (muestra * 2) >= AltoDestino)
            muestra *= 2;

        return muestra;
    }

    /// <summary>BitmapFactory ignora la orientación EXIF, así que hay que aplicarla a mano.</summary>
    private static Bitmap AplicarRotacion(Bitmap bitmap, string ruta)
    {
        int grados = MdExif.ObtenerGrados(ruta);
        if (grados == 0) return bitmap;

        Android.Graphics.Matrix matriz = new();
        matriz.PostRotate(grados);

        Bitmap? rotado = Bitmap.CreateBitmap(bitmap, 0, 0, bitmap.Width, bitmap.Height, matriz, true);
        if (rotado is null || ReferenceEquals(rotado, bitmap)) return bitmap;

        bitmap.Recycle();
        bitmap.Dispose();

        return rotado;
    }

    private static string ClaveCache(FotoItem foto)
    {
        string firma = $"{foto.Ruta}|{foto.Fecha.Ticks}|{foto.Tamaño}";
        byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(firma));

        return Convert.ToHexString(hash) + ".jpg";
    }

    #endregion "Funciones"
}
