# SwipeAndBye

App Android para limpiar el carrete a base de swipes. Agrupa las fotos del móvil por año y mes,
y dentro de cada mes las va pasando una a una: **izquierda para descartar, derecha para conservar**.
Lleva la cuenta de cuántas has borrado y cuánto espacio has liberado.

Nada de lo que descartas se borra en el acto: va a una papelera propia y se puede deshacer.

## Requisitos

- .NET SDK 9 o superior con las cargas de trabajo `maui` y `android`
  (`dotnet workload install maui android`)
- Android 5.0 (API 21) o superior
- Permiso de **acceso a todos los archivos** (`MANAGE_EXTERNAL_STORAGE`), que se concede a mano
  desde los ajustes del sistema la primera vez que se abre la app

## Compilar y ejecutar

```bash
# Compilar
dotnet build SwipeAndBye/SwipeAndBye.csproj -c Debug

# Instalar en el dispositivo conectado
dotnet build SwipeAndBye/SwipeAndBye.csproj -c Debug -t:Install

# Compilar y lanzar
dotnet build SwipeAndBye/SwipeAndBye.csproj -c Debug -t:Run
```

Con `adb devices` se comprueba que el móvil está conectado y con la depuración USB activada.

## Cómo funciona

### Pantalla principal

Escanea el almacenamiento compartido buscando `.jpg`, `.jpeg` y `.png`, saltándose carpetas
ocultas y del sistema. Las agrupa por año y mes en una rejilla con la miniatura del mes.

La fecha con la que agrupa sale del **EXIF** (`DateTimeOriginal`), no de la fecha del archivo:
copiar una foto o restaurar un backup cambia la del archivo, pero no la de la toma. Si la foto no
lleva EXIF, se recurre a la fecha del archivo.

El escaneo completo solo ocurre en el primer arranque o al **tirar hacia abajo** para refrescar.
El resto del tiempo la lista vive en memoria: al volver de revisar un mes se aplican los borrados
sobre el modelo, sin recorrer el disco otra vez.

### Papelera

Descartar una foto **no la borra**, la mueve a:

```
/storage/emulated/0/.SwipeAndBye/papelera/
```

Al estar en el mismo volumen, mover es un *rename* instantáneo aunque la foto pese 8 MB, y
deshacer es otro *rename*. La carpeta empieza por punto (oculta) y lleva un `.nomedia`, así que ni
la galería ni el propio escaneo de la app la ven.

Cada foto guarda su ruta original en `papelera/.rutas/`, que es lo que permite devolverla a su
carpeta con **Recuperar todo**. Va en subcarpeta a propósito: `Directory.GetFiles` no recursa, así
que el resto del código sigue viendo solo fotos.

El nombre en la papelera es `{ticks}_{guid}_{nombreOriginal}`:

- **ticks** — cuándo se descartó. Hace falta porque la fecha del archivo es la de la foto y hay que
  conservarla intacta para poder reagruparla al recuperarla.
- **guid** — evita que dos `IMG_0001.jpg` de carpetas distintas se pisen.

Lo que lleve más de **7 días** en la papelera se borra definitivamente al arrancar. También se
puede vaciar a mano desde la cabecera, que muestra cuánto ocupa.

> El contador de *Liberado* suma al descartar, pero el espacio no se recupera de verdad hasta que
> la papelera se purga o se vacía. La fila *En la papelera* enseña justo esa diferencia.

### Deshacer

El botón `↺` de la pantalla de revisión retrocede una foto. Es una pila completa, no solo el
último paso, y funciona tanto si descartaste como si conservaste; solo saca el archivo de la
papelera en el primer caso.

## Estructura

```
SwipeAndBye/
├── Clases/            Modelos (FotoItem, GrupoAño, GrupoMes, EntradaPapelera…)
├── Modulos/
│   ├── MdUtilidades   Escaneo del disco, agrupado y contadores en Preferences
│   ├── MdPapelera     Mover, restaurar, purgar y medir la papelera
│   ├── MdMiniaturas   Miniaturas cacheadas en disco
│   └── MdExif         Fecha de la toma y orientación
├── Pages/
│   └── PageSwipe      Revisión foto a foto: gestos, botones y deshacer
├── Platforms/Android/ Manifest, actividad y permisos
└── MainPage           Rejilla por año y mes
```

### Miniaturas

La rejilla no carga los JPEG originales: una foto de 12 MP ocupa unos 48 MB en memoria y pintarla
a 100 px sale carísimo. `MdMiniaturas` decodifica en dos pasadas (primero solo las dimensiones con
`InJustDecodeBounds`, luego ya submuestreada con `InSampleSize`), aplica la rotación EXIF a mano
—`BitmapFactory` la ignora— y deja el resultado en `CacheDirectory/miniaturas`. La clave es un
SHA1 de ruta, fecha y tamaño, así que si la foto cambia la miniatura se regenera sola.

La primera carga cuesta; a partir de ahí salen de la caché.

## Limitaciones conocidas

- **Solo `.jpg`, `.jpeg` y `.png`.** Fuera se quedan HEIC/HEIF, WebP y RAW.
- **`MANAGE_EXTERNAL_STORAGE`.** Google Play restringe este permiso a gestores de archivos y
  similares, así que tal cual la app no es publicable. Sería necesario migrar a MediaStore.
- **En Android 6–10 no arranca**: se pide `WRITE_EXTERNAL_STORAGE` sin declararlo en el manifest.
- **El arranque en frío tarda** (unos 45 s con ~2.500 fotos): la caché de la lista es en memoria,
  no persiste entre sesiones.
- **Todo en code-behind**, sin MVVM ni inyección de dependencias, y por tanto sin tests.

## Licencia

Sin licencia definida.
