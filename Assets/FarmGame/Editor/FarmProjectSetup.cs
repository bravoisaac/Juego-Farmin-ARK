using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CozyFarm.Editor
{
    [InitializeOnLoad]
    public static class FarmProjectSetup
    {
        public const string ScenePath = "Assets/FarmGame/Scenes/Farm.unity";
        private const string GeneratedPath = "Assets/FarmGame/Generated";

        static FarmProjectSetup()
        {
            EditorApplication.delayCall += OnFirstImport;
        }

        private static void OnFirstImport()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OnFirstImport;
                return;
            }
            if (File.Exists(ScenePath)) return;
            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.Log("Valle Semilla está listo. Guarda tu escena y usa Valle Semilla > Preparar escena inicial.");
                return;
            }
            PrepareInitialScene();
        }

        [MenuItem("Valle Semilla/Preparar escena inicial", priority = 0)]
        public static void PrepareInitialScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            CreateScene(ScenePath);
        }

        [MenuItem("Valle Semilla/Crear otra escena de granja", priority = 1)]
        public static void CreateVariant()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene(AssetDatabase.GenerateUniqueAssetPath(ScenePath));
        }

        private static void CreateScene(string path)
        {
            Directory.CreateDirectory("Assets/FarmGame/Scenes");
            Directory.CreateDirectory(GeneratedPath);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var environment = new GameObject("Entorno low poly · editable");
            WorldReferences world = LowPolyWorld.Build(environment.transform);
            AddPlotPreview(world);
            PersistGeometry(environment);
            var game = new GameObject("Valle Semilla · juego").AddComponent<FarmBootstrap>();
            game.Configure(world);

            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 15;
            camera.transform.position = new Vector3(19, 24, -25);
            camera.transform.LookAt(new Vector3(0, 0, 2));
            camera.backgroundColor = new Color(0.70f, 0.82f, 0.79f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.farClipPlane = 140;
            camera.allowHDR = false;

            var sun = new GameObject("Sol · única luz con sombras").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            sun.color = new Color(1f, 0.93f, 0.79f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.64f, 0.72f, 0.64f);
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 55;
            QualitySettings.shadowCascades = 2;
            QualitySettings.antiAliasing = 2;
            PlayerSettings.companyName = "ValleSemilla";
            PlayerSettings.productName = "Valle Semilla";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            IncludeShader("Standard");
            IncludeShader("CozyFarm/LowPoly");

            // This project deliberately uses Unity's built-in input and renderer.
            // The setting is serialized because its public API differs between editor versions.
            UnityEngine.Object[] projectSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (projectSettings.Length > 0)
            {
                var settings = new SerializedObject(projectSettings[0]);
                SerializedProperty input = settings.FindProperty("activeInputHandler");
                if (input != null) { input.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            }
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSceneManager.SaveScene(scene, path);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(item => item.path == path)) scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 0, 2), Quaternion.Euler(48, -35, 0), 27);
            Selection.activeGameObject = game.gameObject;
            Debug.Log("Valle Semilla: escena creada en " + path + ". Pulsa Play para jugar. WASD, E, R, B, U y 1/2/3.");
        }

        private static void AddPlotPreview(WorldReferences world)
        {
            // A single editor-only mesh makes the farm readable before entering Play Mode.
            var preview = new GameObject("Vista previa de parcelas", typeof(MeshFilter), typeof(MeshRenderer));
            preview.tag = "EditorOnly";
            preview.transform.SetParent(world.plotAnchors[0].parent, false);
            var vertices = new Vector3[72];
            var colors = new Color[72];
            var triangles = new int[108];
            for (int i = 0; i < 18; i++)
            {
                Vector3 center = world.plotAnchors[i].localPosition + Vector3.up * 0.04f;
                int v = i * 4;
                vertices[v] = center + new Vector3(-1.22f, 0, -1.22f);
                vertices[v + 1] = center + new Vector3(-1.22f, 0, 1.22f);
                vertices[v + 2] = center + new Vector3(1.22f, 0, 1.22f);
                vertices[v + 3] = center + new Vector3(1.22f, 0, -1.22f);
                Color color = i < 6 ? new Color(0.40f, 0.26f, 0.16f) : new Color(0.55f, 0.56f, 0.38f);
                for (int c = 0; c < 4; c++) colors[v + c] = color;
                int t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Editor plot preview", vertices = vertices, triangles = triangles, colors = colors };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            preview.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = preview.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = world.sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void IncludeShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader ausente: " + shaderName);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets.Length == 0) throw new InvalidOperationException("No se encontraron los ajustes gráficos del proyecto.");
            var graphics = new SerializedObject(assets[0]);
            SerializedProperty shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            if (shaders == null) throw new InvalidOperationException("No se encontró la lista de shaders incluidos.");
            for (int i = 0; i < shaders.arraySize; i++)
                if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            int index = shaders.arraySize;
            shaders.InsertArrayElementAtIndex(index);
            shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PersistGeometry(GameObject environment)
        {
            // Procedural meshes/materials must become assets before serializing the scene.
            var seen = new HashSet<UnityEngine.Object>();
            foreach (MeshFilter filter in environment.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || AssetDatabase.Contains(mesh) || !seen.Add(mesh)) continue;
                string filename = SafeName(mesh.name) + ".asset";
                AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(GeneratedPath + "/" + filename));
            }
            foreach (Renderer renderer in environment.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || AssetDatabase.Contains(material) || !seen.Add(material)) continue;
                    AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(GeneratedPath + "/" + SafeName(material.name) + ".mat"));
                }
            }
        }

        private static string SafeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrEmpty(name) ? "Geometry" : name;
        }

        [MenuItem("Valle Semilla/Validar proyecto", priority = 20)]
        public static void ValidateProject()
        {
            PrepareInitialScene();
            Shader shader = Shader.Find("CozyFarm/LowPoly");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("El shader low poly no compiló.");
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<FarmBootstrap>();
            if (bootstrap == null) throw new InvalidOperationException("La escena no tiene FarmBootstrap.");
            var serialized = new SerializedObject(bootstrap);
            SerializedProperty anchors = serialized.FindProperty("plotAnchors");
            if (anchors.arraySize != 18) throw new InvalidOperationException("Se esperaban 18 parcelas.");
            for (int i = 0; i < anchors.arraySize; i++)
                if (anchors.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    throw new InvalidOperationException("Parcela sin referencia: " + i);
            if (Camera.main == null) throw new InvalidOperationException("Falta la cámara principal.");
            int triangles = 0;
            foreach (MeshFilter filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (filter.sharedMesh == null) throw new InvalidOperationException("Mesh ausente: " + filter.name);
                if (!AssetDatabase.Contains(filter.sharedMesh)) throw new InvalidOperationException("Mesh sin persistir: " + filter.name);
                triangles += filter.sharedMesh.triangles.Length / 3;
            }
            Debug.Log("VALIDATION PASSED: 18 parcelas, referencias persistidas, cámara, shader y " + triangles + " triángulos de entorno.");
        }

        [MenuItem("Valle Semilla/Compilar para Windows", priority = 30)]
        public static void BuildWindows()
        {
            ValidateProject();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/ValleSemilla.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("La compilación de Windows falló: " + report.summary.result);
            Debug.Log("Build listo: Builds/Windows/ValleSemilla.exe");
        }
    }
}
