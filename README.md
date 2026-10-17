# Valle Semilla

Base de un juego **low poly de farmeo y progresión para PC**, preparada para Unity 6.3 LTS. Incluye código de una escena jugable, un generador de entorno editable y herramientas para validar y compilar el proyecto.

**Estado:** código y reglas preparados; 225 comprobaciones del modelo aprobadas. Unity Hub y la CLI oficial están instalados; Unity Editor 6000.3.26f1 está en instalación. La validación visual y de ejecución depende de terminar esa instalación y activar una licencia en Hub.

## Abrir por primera vez

1. Instala [Unity Hub](https://unity.com/download) y, desde Hub, el **último parche disponible de Unity 6.3 LTS**. La versión de referencia del proyecto es `6000.3.26f1`; puedes abrirlo con un parche posterior de la misma familia. Unity 6.3 LTS tiene soporte hasta diciembre de 2027 según [Unity](https://unity.com/releases/unity-6/support).
2. En Hub selecciona **Projects → Add → Add project from disk** y elige esta carpeta completa. No necesitas crear otro proyecto ni copiar `Assets` a una plantilla.
3. Espera a que termine la importación y descarga de uGUI. Se genera y abre `Assets/FarmGame/Scenes/Farm.unity` automáticamente. Si no aparece, usa **Valle Semilla → Preparar escena inicial**.
4. Pulsa **Play**. Camina hacia las parcelas, elige una semilla y pulsa **E**.

Para abrirlo desde PowerShell después de instalar el editor:

```powershell
.\Tools\Open-Unity.ps1
```

Si instalas Unity en otra unidad:

```powershell
.\Tools\Open-Unity.ps1 -UnityPath "D:\Unity\Editor\Unity.exe"
```

Visual Studio ya está presente en este equipo. Para disponer de integración y depuración con Unity, agrega **Desarrollo de juegos con Unity** desde Visual Studio Installer si aún no tienes esa carga de trabajo. No hace falta instalar Android para esta base de PC. El menú de compilación produce Windows de 64 bits; otros sistemas de escritorio requieren sus módulos de build.

## Qué incluye

- Diorama con terreno, granero, árboles facetados, cercas, camino, estanque, flores y rocas.
- Granjero con movimiento, carrera, animación sencilla y cámara isométrica con zoom.
- 18 parcelas: seis iniciales y doce ampliables con monedas.
- Sembrar, regar, esperar el crecimiento y cosechar. La cosecha se vende automáticamente.
- Experiencia, niveles, tres cultivos desbloqueables y cinco mejoras de regadera.
- Interfaz en español con dinero, experiencia, objetivos, selección y acciones contextuales.
- Pausa, guardado manual y guardado automático cada 30 segundos, al perder el foco y al salir.

| Cultivo | Nivel | Semilla | Venta | XP | Crecimiento sin riego |
| --- | ---: | ---: | ---: | ---: | ---: |
| Zanahoria | 1 | 4 | 10 | 8 | 30 s |
| Trigo | 2 | 9 | 22 | 16 | 55 s |
| Calabaza | 3 | 18 | 46 | 28 | 90 s |

Empiezas con 30 monedas. El nivel 2 llega a 40 XP y el 3 a 120 XP. Regar acelera el crecimiento a 1,5×; cada mejora añade 0,25×. La primera parcela adicional cuesta 35 monedas y cada una posterior aumenta 20. La primera mejora de regadera cuesta 60 monedas. Las compras preservan dinero para al menos una semilla si no hay cultivos activos, para evitar bloquear la partida.

## Controles

| Acción | Control |
| --- | --- |
| Mover | WASD o flechas, respecto a la cámara |
| Correr | Shift |
| Zoom | Rueda del ratón |
| Elegir cultivo | 1, 2, 3 o botones |
| Sembrar / cosechar parcela cercana | E |
| Regar parcela cercana | R |
| Comprar la siguiente parcela | B |
| Mejorar regadera | U |
| Guardar | F5 |
| Pausar / continuar | Esc |

El contorno amarillo indica la parcela cercana. Los bordes azulados indican riego y los dorados indican cosecha lista. Se puede sembrar cuando el centro de una parcela queda a menos de 2,5 unidades del jugador. Las ampliaciones siguen el orden de las parcelas, aunque estés cerca de otra.

## Trabajar en el entorno

- **Valle Semilla → Preparar escena inicial** abre la escena existente o la crea si falta; no sobrescribe una escena ya generada.
- **Valle Semilla → Crear otra escena de granja** crea una variante en otra ruta, conservando la anterior.
- La jerarquía separa entorno, parcelas, cámara, sol y lógica del juego. Los cinco grupos de decorado pueden moverse como grupos; para cambiar elementos dentro de una malla combinada, modifica el generador y crea una variante.
- Las mallas y el material generados se guardan en `Assets/FarmGame/Generated`. Conserva esos assets, sus `.meta` y la escena en el control de versiones cuando Unity los genere.
- Las camas visibles en edición son una vista previa que se oculta al jugar y se excluye del build. Los cultivos y la interfaz se crean al iniciar la partida.
- El dinero, los precios, las recompensas y los tiempos están en `Scripts/Data/FarmState.cs`.
- La composición del entorno está en `Scripts/World/LowPolyWorld.cs`; la cámara y velocidad, en `Scripts/Player/FarmPlayer.cs`.
- El objeto **Valle Semilla · juego** permite ajustar en el Inspector el límite de FPS, el intervalo de simulación y el autoguardado.

## Base de rendimiento

Se utiliza **Built-in Render Pipeline** y un shader sencillo de colores por vértice. No requiere texturas, paquetes de assets ni servicios externos. uGUI es el único paquete de interfaz, además de los módulos del motor.

- Decorado agrupado en **cinco mallas** con un material compartido.
- Siete colliders simples para suelo, bordes, granero y estanque; el personaje usa un `CharacterController`.
- Mallas compartidas para parcelas y tres modelos de cultivo. Los objetos cosechados se guardan en pools para reutilizarlos al sembrar.
- Un reloj central actualiza crecimiento e interfaz cada 0,2 segundos. No hay un `Update`, coroutine o temporizador por planta.
- Selección por distancia sobre las 18 parcelas; sin búsquedas de objetos ni raycasts globales en cada fotograma.
- Una luz direccional, distancia de sombras de 55 unidades y MSAA 2×. Sin posprocesado ni luces adicionales.
- Objetivo inicial de 60 FPS. **Es un límite configurado, no una medición ni una garantía.**

Antes de ampliar el mapa, perfila un build en el PC objetivo: CPU/GPU, GC, batches y memoria. Esta agrupación funciona para una granja pequeña; un mundo grande necesitará división espacial, LOD y carga por zonas. El tamaño visual low poly por sí solo no garantiza buen rendimiento.

## Guardado

Se escribe `farm-save-v1.json` en `Application.persistentDataPath`. Con los ajustes generados, en Windows normalmente será:

```text
%USERPROFILE%\AppData\LocalLow\ValleSemilla\Valle Semilla\farm-save-v1.json
```

La escritura usa archivo temporal y reemplazo; `.bak` conserva la versión anterior. Si el archivo principal no se puede leer, se intenta recuperar el respaldo. Un archivo ilegible se conserva con sufijo `.unreadable-...` al guardar de nuevo. No se almacena la posición del jugador: al abrir reaparece en la entrada, conservando la granja. No hay crecimiento mientras el juego está cerrado, pausado o sin foco.

## Validar y compilar

Modelo sin Unity, desde PowerShell:

```powershell
.\Tools\Test-Project.ps1
```

Validación adicional con un editor instalado, cerrado y con licencia activada:

```powershell
.\Tools\Test-Project.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
```

Sustituye la versión por la instalada. El script usa el compilador C# de Visual Studio, ejecuta las pruebas y, si indicas el editor, comprueba escena, shader y referencias persistidas. El log de Unity queda en `Temp/Validation/UnityValidation.log`.

Desde el editor: **Valle Semilla → Validar proyecto** y luego **Valle Semilla → Compilar para Windows**. El ejecutable se genera en `Builds/Windows/ValleSemilla.exe`.

Prueba manual pendiente: iniciar Play sin errores; sembrar y regar las seis parcelas; comprobar que solo se cosecha al madurar; alcanzar nivel 2; comprar una parcela y una mejora; guardar, salir y volver; pausar y comprobar que el crecimiento se detiene; probar HUD a 1280×720 y 1920×1080; recorrer los bordes y obstáculos. Confirmar también materiales/sombras en el build y medir rendimiento.

## Estructura

```text
Assets/FarmGame/
  Editor/FarmProjectSetup.cs       Generar, validar y compilar
  Scripts/Core/                   Coordinación y guardado
  Scripts/Data/FarmState.cs        Reglas de economía y crecimiento
  Scripts/Player/FarmPlayer.cs     Movimiento, personaje y cámara
  Scripts/UI/FarmHud.cs            HUD y eventos de botones
  Scripts/World/LowPolyWorld.cs    Generación del diorama
  Shaders/LowPoly.shader           Luz, sombras y colores por vértice
  Scenes/                         Generada en primera importación
  Generated/                      Mallas/materiales persistidos por el editor
Packages/manifest.json
ProjectSettings/
Tools/                            Abrir y probar
PROJECT_MAP.md                    Mapa de dependencias
```

Es un prototipo inicial para ampliar. Inventario independiente, construcción libre, animales, audio, controles de mando y multijugador quedan fuera de esta primera base.

## Prueba automática de inicio y captura

Con el editor cerrado y una licencia activa:

```powershell
.\Tools\Test-Project.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -Runtime
```

Arranca Play Mode, comprueba que se crearon jugador, cámara y botones, verifica la serialización JSON y guarda `Temp/Validation/FarmPreview.png`. Para incluir el HUD en esa captura se usa temporalmente un canvas de cámara; el juego normal conserva su canvas overlay. Esta comprobación no reemplaza una prueba de controles ni una medición de FPS. También puedes ejecutarla desde **Valle Semilla → Validar inicio de partida**.