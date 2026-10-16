using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CozyFarm
{
    /// <summary>One clock for the whole farm, event driven actions, cached visuals.</summary>
    public sealed class FarmBootstrap : MonoBehaviour
    {
        [Header("Escenario editable generado desde Valle Semilla > Preparar escena")]
        [SerializeField] private Transform[] plotAnchors;
        [SerializeField] private Material worldMaterial;
        [SerializeField] private Vector3 playerSpawn = new Vector3(0, 0, -4);
        [Header("Rendimiento")]
        [SerializeField, Range(30, 144)] private int targetFrameRate = 60;
        [SerializeField, Range(0.1f, 0.5f)] private float simulationInterval = 0.2f;
        [SerializeField, Range(15, 120)] private float autoSaveInterval = 30;

        private FarmState state;
        private FarmPlayer player;
        private FarmHud hud;
        private Transform[] cropVisuals;
        private int[] cropIds;
        private Renderer[] plotRings;
        private GameObject[] cropTemplates;
        private Stack<GameObject>[] cropPools;
        private Transform cropPoolRoot;
        private Transform selection;
        private MaterialPropertyBlock colors;
        private Mesh bedMesh;
        private Mesh ringMesh;
        private int nearestPlot = -1;
        private bool paused;
        private bool hasFocus = true;
        private float simulationClock;
        private float saveClock;
        private float toastUntil;
        private string toast;
        private bool ready;

        public void Configure(WorldReferences world)
        {
            plotAnchors = world.plotAnchors;
            worldMaterial = world.sharedMaterial;
            playerSpawn = world.playerSpawn;
        }

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 2;
            QualitySettings.shadowDistance = 55;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            if (plotAnchors == null || plotAnchors.Length != 18 || plotAnchors[0] == null)
                Configure(LowPolyWorld.Build(new GameObject("Entorno").transform));
            Transform editorPreview = plotAnchors[0].parent.Find("Vista previa de parcelas");
            if (editorPreview != null) editorPreview.gameObject.SetActive(false);

            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                camera = cameraObject.GetComponent<Camera>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.70f, 0.82f, 0.79f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 140;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            if (RenderSettings.sun == null)
            {
                var sun = new GameObject("Sol").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(48, -35, 0);
                sun.color = new Color(1f, 0.93f, 0.79f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.65f;
                sun.shadowBias = 0.05f;
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.64f, 0.72f, 0.64f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 65;
            RenderSettings.fogEndDistance = 125;

            state = FarmSaveStore.Load(out toast);
            toastUntil = Time.unscaledTime + 8;
            var playerObject = new GameObject("Granjero");
            playerObject.transform.position = playerSpawn;
            player = playerObject.AddComponent<FarmPlayer>();
            player.Initialize(camera);

            CreatePlotVisuals();
            hud = new GameObject("Interfaz").AddComponent<FarmHud>();
            hud.Initialize();
            hud.InteractRequested += Interact;
            hud.WaterRequested += Water;
            hud.ExpandRequested += Expand;
            hud.UpgradeRequested += Upgrade;
            hud.SaveRequested += Save;
            hud.PauseRequested += TogglePause;
            hud.CropSelected += SelectCrop;
            ready = true;
            Refresh();
        }

        private void CreatePlotVisuals()
        {
            colors = new MaterialPropertyBlock();
            bedMesh = CreateQuad(1.22f, new Color(0.40f, 0.26f, 0.16f));
            ringMesh = CreateRing(1.32f, 1.23f);
            cropVisuals = new Transform[18];
            cropIds = new int[18];
            plotRings = new Renderer[18];
            var templatesRoot = new GameObject("Modelos de cultivos (reutilizables)").transform;
            templatesRoot.SetParent(transform, false);
            cropPoolRoot = templatesRoot;
            cropTemplates = new GameObject[FarmRules.Crops.Length];
            cropPools = new Stack<GameObject>[FarmRules.Crops.Length];
            for (int c = 0; c < cropTemplates.Length; c++)
            {
                cropTemplates[c] = LowPolyWorld.CreateCrop(templatesRoot, c, worldMaterial);
                cropTemplates[c].SetActive(false);
                cropPools[c] = new Stack<GameObject>();
            }
            for (int i = 0; i < plotAnchors.Length; i++)
            {
                cropIds[i] = -1;
                CreateMeshObject("Tierra", plotAnchors[i], bedMesh, 0.04f);
                plotRings[i] = CreateMeshObject("Estado de parcela", plotAnchors[i], ringMesh, 0.055f).GetComponent<Renderer>();
            }
            selection = CreateMeshObject("Parcela seleccionada", transform, CreateRing(1.42f, 1.35f), 0.08f).transform;
            SetTint(selection.GetComponent<Renderer>(), new Color(1f, 0.88f, 0.40f));
        }

        private GameObject CreateMeshObject(string label, Transform parent, Mesh mesh, float y)
        {
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = worldMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static Mesh CreateQuad(float radius, Color color)
        {
            var mesh = new Mesh { name = "Cama compartida" };
            mesh.vertices = new[] { new Vector3(-radius, 0, -radius), new Vector3(-radius, 0, radius), new Vector3(radius, 0, radius), new Vector3(radius, 0, -radius) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.colors = new[] { color, color, color, color };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateRing(float outer, float inner)
        {
            var vertices = new Vector3[8];
            var indices = new int[24];
            var vertexColors = new Color[8];
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -1 : 1;
                float z = i == 0 || i == 3 ? -1 : 1;
                vertices[i] = new Vector3(x * outer, 0, z * outer);
                vertices[i + 4] = new Vector3(x * inner, 0, z * inner);
                vertexColors[i] = vertexColors[i + 4] = Color.white;
                int next = (i + 1) % 4;
                int t = i * 6;
                indices[t] = i; indices[t + 1] = next; indices[t + 2] = next + 4;
                indices[t + 3] = i; indices[t + 4] = next + 4; indices[t + 5] = i + 4;
            }
            var mesh = new Mesh { name = "Borde compartido" };
            mesh.vertices = vertices; mesh.triangles = indices; mesh.colors = vertexColors;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void Update()
        {
            if (!ready) return;
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (paused || !hasFocus) return;
            FindNearestPlot();
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectCrop(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectCrop(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectCrop(2);
            if (Input.GetKeyDown(KeyCode.E)) Interact();
            if (Input.GetKeyDown(KeyCode.R)) Water();
            if (Input.GetKeyDown(KeyCode.B)) Expand();
            if (Input.GetKeyDown(KeyCode.U)) Upgrade();
            simulationClock += Time.deltaTime;
            saveClock += Time.deltaTime;
            if (simulationClock >= simulationInterval)
            {
                FarmRules.Tick(state, simulationClock);
                simulationClock = 0;
                Refresh();
            }
            if (saveClock >= autoSaveInterval) Save();
        }

        private void FindNearestPlot()
        {
            int nearest = -1;
            float closest = 2.5f * 2.5f;
            Vector3 position = player.transform.position;
            for (int i = 0; i < plotAnchors.Length; i++)
            {
                Vector3 delta = plotAnchors[i].position - position;
                delta.y = 0;
                if (delta.sqrMagnitude >= closest) continue;
                closest = delta.sqrMagnitude;
                nearest = i;
            }
            nearestPlot = nearest;
            selection.gameObject.SetActive(nearest >= 0);
            if (nearest >= 0) selection.position = plotAnchors[nearest].position + Vector3.up * 0.08f;
        }

        private void Refresh()
        {
            if (!ready) return;
            FindNearestPlot();
            for (int i = 0; i < state.plots.Count; i++)
            {
                PlotState plot = state.plots[i];
                if (cropIds[i] != plot.cropId)
                {
                    if (cropVisuals[i] != null)
                    {
                        GameObject previous = cropVisuals[i].gameObject;
                        previous.SetActive(false);
                        previous.transform.SetParent(cropPoolRoot, false);
                        cropPools[cropIds[i]].Push(previous);
                    }
                    cropVisuals[i] = null;
                    cropIds[i] = plot.cropId;
                    if (plot.cropId >= 0)
                    {
                        var pool = cropPools[plot.cropId];
                        var crop = pool.Count > 0 ? pool.Pop() : Instantiate(cropTemplates[plot.cropId]);
                        crop.transform.SetParent(plotAnchors[i], false);
                        crop.name = FarmRules.GetCrop(plot.cropId).Name;
                        crop.transform.localPosition = Vector3.up * 0.06f;
                        crop.SetActive(true);
                        cropVisuals[i] = crop.transform;
                    }
                }
                float progress = FarmRules.Progress(state, i);
                if (cropVisuals[i] != null)
                    cropVisuals[i].localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, progress);
                Color color = !plot.unlocked ? new Color(0.43f, 0.47f, 0.40f)
                    : progress >= 1 ? new Color(1f, 0.78f, 0.23f)
                    : plot.watered ? new Color(0.35f, 0.72f, 0.84f)
                    : new Color(0.70f, 0.63f, 0.44f);
                SetTint(plotRings[i], color);
            }
            hud.Render(state, Hint(), Time.unscaledTime < toastUntil ? toast : "", paused);
        }

        private string Hint()
        {
            if (nearestPlot < 0) return "Acércate a una parcela · WASD para caminar";
            PlotState plot = state.plots[nearestPlot];
            string name = "Parcela " + (nearestPlot + 1) + " · ";
            if (!plot.unlocked) return name + "B amplía tu granja por " + FarmRules.UnlockCost(state) + " monedas";
            if (plot.cropId < 0) return name + "E siembra " + FarmRules.GetCrop(state.selectedCrop).Name;
            CropDefinition crop = FarmRules.GetCrop(plot.cropId);
            float progress = FarmRules.Progress(state, nearestPlot);
            if (progress >= 1) return name + "E cosecha " + crop.Name + " · +" + crop.SalePrice + " monedas";
            return name + crop.Name + " " + Mathf.FloorToInt(progress * 100) + "% · " + (plot.watered ? "Regada" : "R para regar");
        }

        private void SetTint(Renderer renderer, Color color)
        {
            colors.SetColor("_Color", color);
            renderer.SetPropertyBlock(colors);
        }

        private bool CanAct()
        {
            return ready && !paused && hasFocus;
        }

        private void Interact()
        {
            if (!CanAct()) return;
            FindNearestPlot();
            if (nearestPlot < 0) { Notify("Acércate a una parcela para trabajar."); return; }
            string message;
            if (state.plots[nearestPlot].cropId < 0)
                FarmRules.TryPlant(state, nearestPlot, state.selectedCrop, out message);
            else FarmRules.TryHarvest(state, nearestPlot, out message);
            Notify(message);
        }

        private void Water()
        {
            if (!CanAct()) return;
            FindNearestPlot();
            string message;
            FarmRules.TryWater(state, nearestPlot, out message);
            Notify(message);
        }

        private void Expand()
        {
            if (!CanAct()) return;
            int next = state.plots.FindIndex(p => !p.unlocked);
            if (next < 0) { Notify("Tu granja ya tiene las 18 parcelas. ¡Ahora hazlas prosperar!"); return; }
            string message;
            FarmRules.TryUnlock(state, next, out message);
            Notify(message);
        }

        private void Upgrade()
        {
            if (!CanAct()) return;
            string message;
            FarmRules.TryUpgrade(state, out message);
            Notify(message);
        }

        private void SelectCrop(int id)
        {
            if (!CanAct() || id < 0 || id >= FarmRules.Crops.Length) return;
            CropDefinition crop = FarmRules.GetCrop(id);
            if (state.Level < crop.RequiredLevel) { Notify(crop.Name + " se desbloquea en el nivel " + crop.RequiredLevel + "."); return; }
            state.selectedCrop = id;
            Refresh();
        }

        private void Notify(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 4;
            Refresh();
        }

        private void TogglePause()
        {
            if (!ready) return;
            paused = !paused;
            player.CanMove = !paused && hasFocus;
            if (paused) Save();
            Refresh();
        }

        private void Save()
        {
            if (!ready) return;
            string message;
            FarmSaveStore.Save(state, out message);
            saveClock = 0;
            Notify(message);
        }

        private void OnApplicationFocus(bool focus)
        {
            hasFocus = focus;
            if (!ready) return;
            player.CanMove = focus && !paused;
            if (!focus) Save();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && ready) Save();
        }

        private void OnApplicationQuit()
        {
            if (ready) Save();
        }

        private void OnDestroy()
        {
            // Runtime meshes are shared across all plots; release only the owned sources.
            if (bedMesh != null) Destroy(bedMesh);
            if (ringMesh != null) Destroy(ringMesh);
            if (selection != null) Destroy(selection.GetComponent<MeshFilter>().sharedMesh);
            if (cropTemplates == null) return;
            foreach (GameObject template in cropTemplates)
                if (template != null)
                    foreach (MeshFilter filter in template.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.sharedMesh != null) Destroy(filter.sharedMesh);
        }
    }
}
