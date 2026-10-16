using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CozyFarm
{
    /// <summary>Presentation only: buttons request actions from the central game controller.</summary>
    [DisallowMultipleComponent]
    public sealed class FarmHud : MonoBehaviour
    {
        public event Action InteractRequested;
        public event Action WaterRequested;
        public event Action ExpandRequested;
        public event Action UpgradeRequested;
        public event Action SaveRequested;
        public event Action PauseRequested;
        public event Action<int> CropSelected;

        private static readonly Color Cream = new Color32(255, 249, 232, 247);
        private static readonly Color Ink = new Color32(43, 65, 47, 255);
        private static readonly Color Muted = new Color32(94, 110, 88, 255);
        private static readonly Color Green = new Color32(51, 101, 66, 255);
        private static readonly Color PaleGreen = new Color32(222, 234, 207, 255);
        private static readonly Color Orange = new Color32(237, 162, 66, 255);

        private readonly List<Button> cropButtons = new List<Button>();
        private readonly List<Text> cropTitles = new List<Text>();
        private readonly List<Text> cropDetails = new List<Text>();
        private readonly List<int> cropIds = new List<int>();
        private Font font;
        private RectTransform canvasRoot;
        private Text coinText;
        private Text levelText;
        private Text xpText;
        private RectTransform xpFill;
        private Text objectiveText;
        private Text progressText;
        private Text upgradeText;
        private Text expandText;
        private Text hintText;
        private Text toastText;
        private Text pauseStatusText;
        private GameObject toastPanel;
        private GameObject pausePanel;
        private Button interactButton;
        private Button waterButton;
        private Button expandButton;
        private Button upgradeButton;
        private bool initialized;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject root = new GameObject("FarmHUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvasRoot = root.GetComponent<RectTransform>();
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("FarmEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }

            BuildBrand();
            BuildStats();
            BuildCrops();
            BuildProgression();
            BuildActions();
            BuildToast();
            BuildPause();
        }

        private void BuildBrand()
        {
            RectTransform panel = Panel("Brand", canvasRoot, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, -26f), new Vector2(290f, 96f), Green);
            Label("Title", panel, "VALLE SEMILLA", new Vector2(20f, -15f), new Vector2(255f, 34f), 28, Cream, FontStyle.Bold);
            Label("Subtitle", panel, "TU GRANJA, A TU RITMO", new Vector2(21f, -56f), new Vector2(250f, 22f), 14, PaleGreen);
        }

        private void BuildStats()
        {
            RectTransform panel = Panel("Stats", canvasRoot, Vector2.one, Vector2.one,
                new Vector2(-26f, -26f), new Vector2(300f, 154f), Cream);
            Label("WalletLabel", panel, "MONEDAS", new Vector2(20f, -16f), new Vector2(125f, 20f), 13, Muted, FontStyle.Bold);
            coinText = Label("Coins", panel, "0", new Vector2(18f, -38f), new Vector2(150f, 43f), 34, Ink, FontStyle.Bold);
            coinText.resizeTextForBestFit = true;
            coinText.resizeTextMinSize = 16;
            coinText.resizeTextMaxSize = 34;
            levelText = Label("Level", panel, "NIVEL 1", new Vector2(165f, -27f), new Vector2(115f, 32f), 20, Green, FontStyle.Bold, TextAnchor.MiddleRight);
            xpText = Label("XP", panel, "0 / 40 XP", new Vector2(20f, -93f), new Vector2(260f, 21f), 14, Muted);
            RectTransform track = Panel("XPTrack", panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -124f), new Vector2(260f, 9f), PaleGreen, false);
            xpFill = Panel("XPFill", track, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Green, false);
            xpFill.anchorMin = Vector2.zero;
            xpFill.anchorMax = new Vector2(0f, 1f);
            xpFill.sizeDelta = Vector2.zero;
        }

        private void BuildCrops()
        {
            RectTransform panel = Panel("SeedSelection", canvasRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(26f, 5f), new Vector2(290f, 386f), Cream);
            Label("Heading", panel, "ELIGE TU CULTIVO", new Vector2(18f, -17f), new Vector2(255f, 25f), 18, Ink, FontStyle.Bold);
            Label("Hint", panel, "Teclas 1, 2 y 3 o un clic", new Vector2(18f, -44f), new Vector2(255f, 22f), 13, Muted);
            int index = 0;
            foreach (CropDefinition crop in FarmRules.Crops)
            {
                int cropId = crop.Id;
                Button button = MakeButton("Crop" + cropId, panel, string.Empty,
                    new Vector2(14f, -77f - index * 98f), new Vector2(262f, 88f), PaleGreen,
                    () => CropSelected?.Invoke(cropId));
                cropButtons.Add(button);
                cropIds.Add(cropId);
                RectTransform rect = button.GetComponent<RectTransform>();
                cropTitles.Add(Label("Name", rect, crop.Name, new Vector2(13f, -9f), new Vector2(236f, 27f), 19, Ink, FontStyle.Bold));
                cropDetails.Add(Label("Details", rect, string.Empty, new Vector2(13f, -39f), new Vector2(236f, 42f), 13, Muted));
                index++;
            }
        }

        private void BuildProgression()
        {
            RectTransform panel = Panel("Progression", canvasRoot, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-26f, -27f), new Vector2(300f, 354f), Cream);
            Label("Heading", panel, "UN POCO MÁS CADA DÍA", new Vector2(18f, -18f), new Vector2(264f, 26f), 17, Ink, FontStyle.Bold);
            objectiveText = Label("Objective", panel, string.Empty, new Vector2(18f, -54f), new Vector2(264f, 87f), 15, Muted);
            progressText = Label("Progress", panel, string.Empty, new Vector2(18f, -144f), new Vector2(264f, 23f), 14, Green, FontStyle.Bold);
            expandButton = MakeButton("Expand", panel, "B  ·  AMPLIAR LA GRANJA",
                new Vector2(16f, -182f), new Vector2(268f, 46f), Green, () => ExpandRequested?.Invoke());
            expandText = Label("ExpandCost", panel, string.Empty, new Vector2(18f, -234f), new Vector2(264f, 22f), 13, Muted);
            upgradeButton = MakeButton("Upgrade", panel, "U  ·  MEJORAR REGADERA",
                new Vector2(16f, -267f), new Vector2(268f, 46f), PaleGreen, () => UpgradeRequested?.Invoke());
            upgradeText = Label("UpgradeCost", panel, string.Empty, new Vector2(18f, -319f), new Vector2(264f, 22f), 13, Muted);
        }

        private void BuildActions()
        {
            RectTransform panel = Panel("ContextActions", canvasRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 69f), new Vector2(650f, 121f), Cream);
            hintText = Label("Context", panel, "Acércate a una parcela para empezar.",
                new Vector2(15f, -12f), new Vector2(620f, 44f), 17, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            interactButton = MakeButton("Interact", panel, "E  ·  SEMBRAR / COSECHAR",
                new Vector2(15f, -64f), new Vector2(366f, 44f), Green, () => InteractRequested?.Invoke());
            waterButton = MakeButton("Water", panel, "R  ·  REGAR",
                new Vector2(393f, -64f), new Vector2(242f, 44f), PaleGreen, () => WaterRequested?.Invoke());

            RectTransform footer = Panel("Controls", canvasRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 17f), new Vector2(990f, 38f), new Color32(36, 59, 43, 235));
            Label("Keyboard", footer, "WASD / FLECHAS  Mover    ·    SHIFT  Correr    ·    RUEDA  Zoom",
                new Vector2(13f, -6f), new Vector2(608f, 26f), 13, Cream, FontStyle.Normal, TextAnchor.MiddleLeft);
            MakeButton("Save", footer, "F5  Guardar", new Vector2(626f, -4f), new Vector2(162f, 30f), Green, () => SaveRequested?.Invoke());
            MakeButton("Pause", footer, "ESC  Pausa", new Vector2(797f, -4f), new Vector2(180f, 30f), Green, () => PauseRequested?.Invoke());
        }

        private void BuildToast()
        {
            RectTransform panel = Panel("Toast", canvasRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -27f), new Vector2(660f, 66f), Orange);
            toastPanel = panel.gameObject;
            toastText = Label("Message", panel, string.Empty, new Vector2(18f, -9f), new Vector2(624f, 48f), 17, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            toastPanel.SetActive(false);
        }

        private void BuildPause()
        {
            RectTransform overlay = Panel("PauseOverlay", canvasRoot, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, new Color32(24, 40, 30, 180));
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            pausePanel = overlay.gameObject;
            RectTransform card = Panel("PauseCard", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(480f, 336f), Cream);
            Label("Title", card, "UN RESPIRO", new Vector2(30f, -31f), new Vector2(420f, 42f), 31, Green, FontStyle.Bold, TextAnchor.MiddleCenter);
            Label("Message", card, "La granja está en pausa.\nVuelve cuando quieras seguir cultivando.",
                new Vector2(30f, -90f), new Vector2(420f, 61f), 18, Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            MakeButton("Resume", card, "VOLVER A LA GRANJA  ·  ESC", new Vector2(34f, -181f),
                new Vector2(412f, 53f), Green, () => PauseRequested?.Invoke());
            MakeButton("Save", card, "GUARDAR PARTIDA  ·  F5", new Vector2(34f, -249f),
                new Vector2(412f, 48f), PaleGreen, () => SaveRequested?.Invoke());
            pauseStatusText = Label("SaveStatus", card, string.Empty, new Vector2(20f, -305f),
                new Vector2(440f, 24f), 13, Green, FontStyle.Normal, TextAnchor.MiddleCenter);
            pausePanel.SetActive(false);
        }

        public void Render(FarmState state, string hint, string toast, bool paused)
        {
            if (!initialized || state == null) return;
            SetText(coinText, state.coins.ToString("N0"));
            SetText(levelText, "NIVEL " + state.Level);
            int previousThreshold = 20 * state.Level * (state.Level - 1);
            int required = state.NextLevelExperience - previousThreshold;
            int current = state.experience - previousThreshold;
            SetText(xpText, "EXPERIENCIA  " + current + " / " + required + " XP");
            xpFill.anchorMax = new Vector2(Mathf.Clamp01(required > 0 ? (float)current / required : 1f), 1f);
            SetText(hintText, string.IsNullOrWhiteSpace(hint) ? "Acércate a una parcela para empezar." : hint);
            SetText(toastText, toast ?? string.Empty);
            SetText(pauseStatusText, toast ?? string.Empty);
            bool showToast = !string.IsNullOrWhiteSpace(toast);
            if (toastPanel.activeSelf != showToast) toastPanel.SetActive(showToast);
            if (pausePanel.activeSelf != paused) pausePanel.SetActive(paused);

            int unlocked = 0;
            foreach (PlotState plot in state.plots) if (plot.unlocked) unlocked++;
            SetText(objectiveText, state.harvests == 0
                ? "TU PRIMERA COSECHA\nElige una semilla, siembra en una parcela y riega para acelerar el crecimiento."
                : "HAZ CRECER TU GRANJA\nCosecha para subir de nivel, descubrir cultivos y abrir nuevas parcelas.");
            SetText(progressText, state.harvests + " cosechas  ·  " + unlocked + "/" + state.plots.Count + " parcelas");
            int expansionCost = FarmRules.UnlockCost(state);
            int upgradeCost = FarmRules.UpgradeCost(state);
            bool hasLockedPlots = unlocked < state.plots.Count;
            bool canUpgrade = state.wateringLevel < FarmRules.MaximumWateringLevel;
            SetText(expandText, hasLockedPlots ? "Nueva parcela: " + expansionCost + " monedas" : "Todas las parcelas están disponibles");
            SetText(upgradeText, canUpgrade
                ? "Mejoras " + state.wateringLevel + "/" + FarmRules.MaximumWateringLevel + "  ·  Próxima: " + upgradeCost + " monedas"
                : "Regadera al máximo: todas las mejoras");
            expandButton.interactable = !paused && hasLockedPlots && state.coins >= expansionCost;
            upgradeButton.interactable = !paused && canUpgrade && state.coins >= upgradeCost;
            interactButton.interactable = !paused;
            waterButton.interactable = !paused;

            for (int i = 0; i < cropButtons.Count; i++)
            {
                CropDefinition crop = FarmRules.GetCrop(cropIds[i]);
                bool available = state.Level >= crop.RequiredLevel;
                bool selected = state.selectedCrop == crop.Id;
                cropButtons[i].interactable = !paused && available;
                cropButtons[i].image.color = selected ? Green : PaleGreen;
                cropTitles[i].color = selected ? Cream : Ink;
                cropDetails[i].color = selected ? Cream : Muted;
                SetText(cropTitles[i], (i + 1) + "  " + crop.Name + (selected ? "  •" : string.Empty));
                SetText(cropDetails[i], available
                    ? "Semilla " + crop.SeedCost + "  ·  Venta " + crop.SalePrice + "  ·  +" + crop.Experience + " XP\n"
                        + crop.GrowSeconds + " s de crecimiento" + (selected ? "  ·  Elegido" : string.Empty)
                    : "Disponible en nivel " + crop.RequiredLevel + "\nSemilla " + crop.SeedCost + "  ·  Venta " + crop.SalePrice);
            }
        }

        private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, Color color, bool raycast = true)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size,
            int fontSize, Color color, FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.text = value;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Button MakeButton(string name, Transform parent, string label, Vector2 position,
            Vector2 size, Color color, Action onClick)
        {
            RectTransform rect = Panel(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, color);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.82f);
            colors.pressedColor = new Color(0.82f, 0.88f, 0.77f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.75f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());
            if (!string.IsNullOrEmpty(label))
                Label("Label", rect, label, new Vector2(7f, -3f), size - new Vector2(14f, 6f),
                    15, color == Green ? Cream : Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            return button;
        }

        private static void SetText(Text label, string value)
        {
            if (label.text != value) label.text = value;
        }
    }
}
