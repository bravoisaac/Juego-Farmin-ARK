# Mapa dinámico

- Objetivo: base jugable de farmeo y progresión low poly, inicialmente para PC.
- Framework: Unity 6.3 LTS, C#, Built-in Render Pipeline, uGUI.
- Punto de entrada: `Assets/FarmGame/Scenes/Farm.unity` → `FarmBootstrap`.
- Flujo: crear/cargar partida → mover jugador → sembrar/regar/cosechar → ganar monedas y experiencia → ampliar la granja.
- Componentes conectados: `LowPolyWorld`, `FarmPlayer`, `FarmHud`, `FarmBootstrap`.
- Modelos: `FarmState`, `PlotState`, `CropDefinition`, `FarmRules` (C# sin dependencia de Unity).
- Servicios: `FarmSaveStore`, JSON local con copia de respaldo.
- Estado: una partida central; reloj de simulación cada 0,2 s; UI actualizada por el reloj y acciones, con textos reasignados solo cuando cambian.
- API/backend: ninguno.
- Patrones existentes: carpeta inicialmente vacía, sin arquitectura previa.
- Archivos candidatos revisados: inventario del proyecto, herramientas instaladas, documentación oficial de Unity.
- Archivos a crear: Assets/FarmGame, Packages/manifest.json, ProjectSettings, README.md y Tools.
- Archivos que no se deben tocar: archivos personales externos al proyecto; Library/Temp generados por Unity.
- Riesgo: medio; requiere importar y ejecutar en Unity para validar renderizado y físicas.
- Verificación realizada: 225 aserciones en seis escenarios; importación y compilación en Unity 6000.3.26f1; escena, shader, 18 referencias de parcelas y 4.790 triángulos persistidos; Play Mode con jugador, cámara, HUD y serialización JSON; captura revisada; build Windows completado.
- Verificación pendiente: prueba manual completa de controles, colisiones, progresión, recarga del guardado y medición de FPS/CPU/GPU en el equipo objetivo.
- Herramientas: `Tools/Open-Unity.ps1`, `Tools/Test-Project.ps1`; menú `Valle Semilla` dentro del editor.

## Contratos de integración

Namespace común: `CozyFarm`.

- `LowPolyWorld.Build(Transform parent)` devuelve `WorldReferences` con `Transform[] plotAnchors` (18, seis columnas por tres filas), `Vector3 playerSpawn`, `Material sharedMaterial` y `Transform decorationRoot`.
- `LowPolyWorld.CreateCrop(Transform parent, int cropId, Material material)` crea una visual reutilizable; `SetPlotState` queda a cargo del bootstrap.
- `FarmPlayer.Initialize(Camera camera)`; `FarmPlayer.CanMove`; movimiento WASD/flechas y Shift.
- `FarmHud.Initialize()`; `Render(FarmState state, string hint, string toast, bool paused)`; eventos `Action InteractRequested`, `WaterRequested`, `ExpandRequested`, `UpgradeRequested`, `SaveRequested`, `PauseRequested`; `Action<int> CropSelected`.
- Estado: `FarmState.NewGame()`, campos `coins`, `experience`, `wateringLevel`, `harvests`, `selectedCrop`, `worldSeconds`, `List<PlotState> plots`, propiedades `Level`, `NextLevelExperience`, método `Validate()`.
- Parcela: `cropId` (-1 vacía), `growthSeconds`, `watered`, `unlocked`.
- `FarmRules.Crops`, `GetCrop(int)`, `TryPlant`, `TryWater`, `TryHarvest`, `TryUnlock`, `TryUpgrade`, `Tick`, `Progress`, `UpgradeCost`, `UnlockCost`.

## Flujo final

`FarmProjectSetup` crea/persiste el entorno y configura `FarmBootstrap`. Al jugar, el bootstrap carga `FarmSaveStore`, crea jugador/HUD y gestiona un pool de visuales por cultivo. Las acciones de teclado o botones pasan por `FarmRules`; el reloj actualiza crecimiento y aspecto. El guardado conserva una copia de respaldo. El código de edición no se incluye en el ejecutable.

## Puesta en marcha (continuación)

- Proyecto localizado tras renombrado en Juego-Farmin-ARK.
- Unity Hub, CLI y editor 6000.3.26f1 instalados. Licencia reconocida por el editor.
- Revisión Unity CLI de 36 archivos: sin errores de integridad ni advertencias.
- FarmRuntimeValidation pasa en modo batch y genera Temp/Validation/FarmPreview.png. Espera los callbacks de inicio antes de entrar en Play para evitar interrumpir la indexación de Unity Search.
- Shader corregido para convertir colores de vértice a espacio lineal cuando corresponde.
- Ejecutable: Builds/Windows/ValleSemilla.exe. Logs de validación/build/ejecución en Temp/Validation.
