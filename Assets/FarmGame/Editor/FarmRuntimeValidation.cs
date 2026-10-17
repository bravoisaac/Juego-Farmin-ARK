using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFarm.Editor
{
    /// <summary>Checks the actual Play Mode startup and captures a frame for visual review.</summary>
    [InitializeOnLoad]
    public static class FarmRuntimeValidation
    {
        private const string PendingKey = "CozyFarm.RuntimeValidation.Pending";
        private const string StartedKey = "CozyFarm.RuntimeValidation.Started";
        private const string ErrorKey = "CozyFarm.RuntimeValidation.Error";
        private static double enteredPlayAt;

        static FarmRuntimeValidation()
        {
            if (SessionState.GetBool(PendingKey, false)) Monitor();
        }

        [MenuItem("Valle Semilla/Validar inicio de partida", priority = 21)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Sal de Play antes de iniciar la validación.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            FarmProjectSetup.ValidateProject();
            SessionState.SetBool(PendingKey, true);
            SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetString(ErrorKey, string.Empty);
            Monitor();
            EditorApplication.EnterPlaymode();
        }

        private static void Monitor()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceived -= ObserveLog;
            Application.logMessageReceived += ObserveLog;
            enteredPlayAt = 0;
        }

        private static void ObserveLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                SessionState.SetString(ErrorKey, message);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedKey, 0) > 90)
            {
                Finish(false, "Tiempo de espera agotado al entrar en Play.");
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (enteredPlayAt == 0) enteredPlayAt = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - enteredPlayAt < 2) return;
            try
            {
                string error = SessionState.GetString(ErrorKey, string.Empty);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                if (UnityEngine.Object.FindFirstObjectByType<FarmBootstrap>() == null)
                    throw new InvalidOperationException("No se inició el controlador de la granja.");
                FarmPlayer player = UnityEngine.Object.FindFirstObjectByType<FarmPlayer>();
                if (player == null || player.GetComponent<CharacterController>() == null)
                    throw new InvalidOperationException("No se creó el jugador con su controlador.");
                if (Camera.main == null) throw new InvalidOperationException("No hay cámara activa.");
                Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                if (canvas == null || canvas.GetComponentsInChildren<Button>(true).Length < 8)
                    throw new InvalidOperationException("La interfaz no se creó completamente.");
                FarmState sample = FarmState.NewGame();
                string message;
                FarmRules.TryPlant(sample, 0, 0, out message);
                FarmRules.TryWater(sample, 0, out message);
                FarmRules.Tick(sample, 3);
                FarmState restored = JsonUtility.FromJson<FarmState>(JsonUtility.ToJson(sample));
                restored.Validate();
                if (restored.version != 1 || restored.coins != sample.coins || restored.plots.Count != 18
                    || restored.plots[0].cropId != 0 || !restored.plots[0].watered
                    || Math.Abs(restored.plots[0].growthSeconds - sample.plots[0].growthSeconds) > 0.001f)
                    throw new InvalidOperationException("El estado no sobrevive a la serialización JSON.");
                CaptureFrame(Camera.main, canvas);
                error = SessionState.GetString(ErrorKey, string.Empty);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                Finish(true, "RUNTIME VALIDATION PASSED: jugador, interfaz, cámara, JSON y captura de render.");
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void CaptureFrame(Camera camera, Canvas canvas)
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            try
            {
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 0.5f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                pixels.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Validation"));
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "FarmPreview.png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(PendingKey, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= ObserveLog;
            if (success) Debug.Log(message); else Debug.LogError(message);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }
}