using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CozyFarm
{
    /// <summary>Versioned local save. Keep the last successful write as a fallback.</summary>
    public static class FarmSaveStore
    {
        public static string SavePath { get { return Path.Combine(Application.persistentDataPath, "farm-save-v1.json"); } }

        public static FarmState Load(out string message)
        {
            message = "Bienvenido a Valle Semilla. Acércate a una parcela y pulsa E.";
            FarmState state;
            if (TryRead(SavePath, out state))
            {
                message = "Tu granja está lista. ¡Seguimos creciendo!";
                return state;
            }
            if (TryRead(SavePath + ".bak", out state))
            {
                message = "Partida recuperada desde la copia de respaldo.";
                return state;
            }
            if (File.Exists(SavePath))
                message = "No se pudo leer la partida. Se inicia una granja nueva; se conserva el archivo anterior.";
            return FarmState.NewGame();
        }

        private static bool TryRead(string path, out FarmState state)
        {
            state = null;
            try
            {
                if (!File.Exists(path)) return false;
                var info = new FileInfo(path);
                if (info.Length == 0 || info.Length > 1024 * 1024) return false;
                string json = File.ReadAllText(path, Encoding.UTF8);
                // Require a recognized payload: JsonUtility can accept an unrelated empty object.
                if (!json.Contains("\"version\"") || !json.Contains("\"plots\"")) return false;
                state = JsonUtility.FromJson<FarmState>(json);
                if (state == null || state.version != 1 || state.plots == null) return false;
                state.Validate();
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                Debug.LogWarning("No se pudo leer el guardado: " + ex.Message);
                return false;
            }
        }

        public static bool Save(FarmState state, out string message)
        {
            string temporary = SavePath + ".tmp";
            try
            {
                state.Validate();
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(temporary, JsonUtility.ToJson(state, true), new UTF8Encoding(false));
                if (File.Exists(SavePath))
                {
                    FarmState previous;
                    if (TryRead(SavePath, out previous))
                        File.Replace(temporary, SavePath, SavePath + ".bak");
                    else
                    {
                        // Preserve unreadable data for recovery instead of silently overwriting it.
                        string recovery = SavePath + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                        File.Move(SavePath, recovery);
                        File.Move(temporary, SavePath);
                    }
                }
                else File.Move(temporary, SavePath);
                message = "Partida guardada.";
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                Debug.LogWarning("No se pudo guardar la granja: " + ex.Message);
                message = "No se pudo guardar. Revisa el espacio y los permisos de la carpeta de guardado.";
                return false;
            }
        }
    }
}
