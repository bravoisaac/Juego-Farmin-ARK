using System;
using System.Collections.Generic;

namespace CozyFarm
{
    [Serializable]
    public sealed class PlotState
    {
        public int cropId = -1;
        public float growthSeconds;
        public bool watered;
        public bool unlocked;
        [NonSerialized] internal double growthRemainder;
    }

    [Serializable]
    public sealed class FarmState
    {
        public const int PlotCount = 18;
        public const int StartingPlotCount = 6;
        public const int MaximumCounter = 1000000000;

        public int version = 1;
        public int coins = 30;
        public int experience;
        public int wateringLevel;
        public int harvests;
        public int selectedCrop;
        public double worldSeconds;
        public List<PlotState> plots = new List<PlotState>();

        // Level 1: 0 XP, level 2: 40 XP, level 3: 120 XP, level 4: 240 XP.
        public int Level
        {
            get
            {
                int safeExperience = ClampCounter(experience);
                return (int)Math.Floor((1.0 + Math.Sqrt(1.0 + safeExperience / 5.0)) / 2.0);
            }
        }

        // Total accumulated XP needed for the next level, not XP still missing.
        public int NextLevelExperience { get { int level = Level; return 20 * level * (level + 1); } }

        public static FarmState NewGame()
        {
            FarmState state = new FarmState();
            state.Validate();
            return state;
        }

        public void Validate()
        {
            coins = ClampCounter(coins);
            experience = ClampCounter(experience);
            harvests = ClampCounter(harvests);
            wateringLevel = Math.Max(0, Math.Min(FarmRules.MaximumWateringLevel, wateringLevel));
            if (double.IsNaN(worldSeconds) || double.IsInfinity(worldSeconds) || worldSeconds < 0)
                worldSeconds = 0;
            worldSeconds = Math.Min(worldSeconds, 1000000000000.0);

            if (plots == null) plots = new List<PlotState>();
            if (plots.Count > PlotCount) plots.RemoveRange(PlotCount, plots.Count - PlotCount);
            while (plots.Count < PlotCount) plots.Add(new PlotState());

            bool previousUnlocked = true;
            int level = Level;
            for (int i = 0; i < plots.Count; i++)
            {
                if (plots[i] == null) plots[i] = new PlotState();
                PlotState plot = plots[i];
                plot.unlocked = i < StartingPlotCount || (previousUnlocked && plot.unlocked);
                previousUnlocked = plot.unlocked;
                CropDefinition crop = FarmRules.GetCrop(plot.cropId);
                if (!plot.unlocked || crop == null || crop.RequiredLevel > level)
                {
                    ClearPlot(plot);
                    continue;
                }
                if (float.IsNaN(plot.growthSeconds) || float.IsInfinity(plot.growthSeconds))
                {
                    plot.growthSeconds = 0;
                    plot.growthRemainder = 0;
                }
                float safeGrowth = Math.Max(0, Math.Min(crop.GrowSeconds, plot.growthSeconds));
                if (safeGrowth != plot.growthSeconds || safeGrowth == 0 || safeGrowth == crop.GrowSeconds)
                    plot.growthRemainder = 0;
                plot.growthSeconds = safeGrowth;
            }

            CropDefinition selected = FarmRules.GetCrop(selectedCrop);
            if (selected == null || selected.RequiredLevel > level) selectedCrop = 0;
        }

        internal static int ClampCounter(int value) { return Math.Max(0, Math.Min(MaximumCounter, value)); }

        internal static void ClearPlot(PlotState plot)
        {
            plot.cropId = -1;
            plot.growthSeconds = 0;
            plot.growthRemainder = 0;
            plot.watered = false;
        }
    }

    public sealed class CropDefinition
    {
        public readonly int Id;
        public readonly string Name;
        public readonly int SeedCost;
        public readonly int SalePrice;
        public readonly int Experience;
        public readonly float GrowSeconds;
        public readonly int RequiredLevel;

        public CropDefinition(int id, string name, int seedCost, int salePrice,
            int experience, float growSeconds, int requiredLevel)
        {
            Id = id;
            Name = name;
            SeedCost = seedCost;
            SalePrice = salePrice;
            Experience = experience;
            GrowSeconds = growSeconds;
            RequiredLevel = requiredLevel;
        }
    }

    // One shared simulation clock; no coroutine, timer, or Update per plant.
    public static class FarmRules
    {
        public const int MaximumWateringLevel = 5;
        public const float MaximumTickSeconds = 60f;

        public static readonly CropDefinition[] Crops =
        {
            new CropDefinition(0, "Zanahoria", 4, 10, 8, 30f, 1),
            new CropDefinition(1, "Trigo", 9, 22, 16, 55f, 2),
            new CropDefinition(2, "Calabaza", 18, 46, 28, 90f, 3)
        };

        public static CropDefinition GetCrop(int cropId)
        {
            return cropId >= 0 && cropId < Crops.Length ? Crops[cropId] : null;
        }

        public static bool TryPlant(FarmState state, int plotIndex, int cropId, out string message)
        {
            PlotState plot;
            if (!TryGetPlot(state, plotIndex, out plot, out message)) return false;
            if (!plot.unlocked) return Fail("Primero desbloquea esta parcela.", out message);
            if (plot.cropId >= 0) return Fail("La parcela ya tiene un cultivo.", out message);
            CropDefinition crop = GetCrop(cropId);
            if (crop == null) return Fail("Cultivo no válido.", out message);
            if (state.Level < crop.RequiredLevel)
                return Fail(crop.Name + " se desbloquea en el nivel " + crop.RequiredLevel + ".", out message);
            if (state.coins < crop.SeedCost)
                return Fail("Necesitas " + crop.SeedCost + " monedas para estas semillas.", out message);

            state.coins -= crop.SeedCost;
            plot.cropId = cropId;
            plot.growthSeconds = 0;
            plot.growthRemainder = 0;
            plot.watered = false;
            message = crop.Name + " sembrada. Puedes regarla para acelerar su crecimiento.";
            return true;
        }

        public static bool TryWater(FarmState state, int plotIndex, out string message)
        {
            PlotState plot;
            if (!TryGetPlot(state, plotIndex, out plot, out message)) return false;
            if (!plot.unlocked) return Fail("Primero desbloquea esta parcela.", out message);
            if (plot.cropId < 0) return Fail("Siembra un cultivo antes de regar.", out message);
            if (Progress(state, plotIndex) >= 1f) return Fail("El cultivo ya está listo para cosechar.", out message);
            if (plot.watered) return Fail("Este cultivo ya está regado.", out message);
            plot.watered = true;
            message = "¡Cultivo regado! Ahora crecerá más rápido.";
            return true;
        }

        public static bool TryHarvest(FarmState state, int plotIndex, out string message)
        {
            PlotState plot;
            if (!TryGetPlot(state, plotIndex, out plot, out message)) return false;
            if (!plot.unlocked || plot.cropId < 0) return Fail("No hay ningún cultivo que cosechar.", out message);
            if (Progress(state, plotIndex) < 1f) return Fail("El cultivo todavía está creciendo.", out message);
            CropDefinition crop = GetCrop(plot.cropId);
            int previousLevel = state.Level;
            state.coins = FarmState.ClampCounter(state.coins + crop.SalePrice);
            state.experience = FarmState.ClampCounter(state.experience + crop.Experience);
            state.harvests = FarmState.ClampCounter(state.harvests + 1);
            FarmState.ClearPlot(plot);
            message = "Cosecha vendida: +" + crop.SalePrice + " monedas y +" + crop.Experience + " XP.";
            if (state.Level > previousLevel) message += " ¡Nivel " + state.Level + "!";
            return true;
        }

        public static bool TryUnlock(FarmState state, int plotIndex, out string message)
        {
            PlotState plot;
            if (!TryGetPlot(state, plotIndex, out plot, out message)) return false;
            if (plot.unlocked) return Fail("Esta parcela ya está desbloqueada.", out message);
            if (plotIndex > 0 && !state.plots[plotIndex - 1].unlocked)
                return Fail("Amplía la granja en orden, empezando por la siguiente parcela.", out message);
            int cost = UnlockCost(state);
            if (state.coins < cost) return Fail("Necesitas " + cost + " monedas para ampliar la granja.", out message);
            if (!CanAffordWithoutStranding(state, cost))
                return Fail("Reserva 4 monedas para semillas o mantén un cultivo sembrado antes de ampliar.", out message);
            state.coins -= cost;
            plot.unlocked = true;
            message = "¡Nueva parcela desbloqueada! (-" + cost + " monedas)";
            return true;
        }

        public static bool TryUpgrade(FarmState state, out string message)
        {
            if (state == null) return Fail("No hay una partida activa.", out message);
            state.Validate();
            if (state.wateringLevel >= MaximumWateringLevel)
                return Fail("La regadera ya tiene todas las mejoras.", out message);
            int cost = UpgradeCost(state);
            if (state.coins < cost) return Fail("Necesitas " + cost + " monedas para mejorar la regadera.", out message);
            if (!CanAffordWithoutStranding(state, cost))
                return Fail("Reserva 4 monedas para semillas o mantén un cultivo sembrado antes de mejorar.", out message);
            state.coins -= cost;
            state.wateringLevel++;
            message = "¡Regadera mejorada! Los cultivos regados crecerán más rápido.";
            return true;
        }

        public static void Tick(FarmState state, float deltaSeconds)
        {
            if (state == null || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0) return;
            state.Validate();
            // Bound long stalls and untrusted elapsed times; saved games do not grant offline growth.
            double delta = Math.Min(deltaSeconds, MaximumTickSeconds);
            state.worldSeconds = Math.Min(1000000000000.0, state.worldSeconds + delta);
            double wateredMultiplier = 1.5 + 0.25 * state.wateringLevel;
            for (int i = 0; i < state.plots.Count; i++)
            {
                PlotState plot = state.plots[i];
                if (!plot.unlocked || plot.cropId < 0) continue;
                CropDefinition crop = GetCrop(plot.cropId);
                // Preserve sub-float fractions so small frame deltas do not accumulate drift.
                double grown = plot.growthSeconds + plot.growthRemainder + delta * (plot.watered ? wateredMultiplier : 1.0);
                plot.growthSeconds = grown >= crop.GrowSeconds - 0.0001
                    ? crop.GrowSeconds : (float)grown;
                plot.growthRemainder = plot.growthSeconds >= crop.GrowSeconds ? 0 : grown - plot.growthSeconds;
            }
        }

        public static float Progress(FarmState state, int plotIndex)
        {
            if (state == null || state.plots == null || plotIndex < 0 || plotIndex >= state.plots.Count) return 0f;
            PlotState plot = state.plots[plotIndex];
            if (plot == null || !plot.unlocked) return 0f;
            CropDefinition crop = GetCrop(plot.cropId);
            if (crop == null || float.IsNaN(plot.growthSeconds) || float.IsInfinity(plot.growthSeconds)) return 0f;
            return Math.Max(0f, Math.Min(1f, plot.growthSeconds / crop.GrowSeconds));
        }

        public static int UpgradeCost(FarmState state)
        {
            if (state == null) return 60;
            int level = Math.Max(0, Math.Min(MaximumWateringLevel, state.wateringLevel));
            return level >= MaximumWateringLevel ? 0 : 60 * (level + 1);
        }

        public static int UnlockCost(FarmState state)
        {
            int unlocked = FarmState.StartingPlotCount;
            if (state != null && state.plots != null)
            {
                for (int i = FarmState.StartingPlotCount; i < Math.Min(FarmState.PlotCount, state.plots.Count); i++)
                {
                    if (state.plots[i] == null || !state.plots[i].unlocked) break;
                    unlocked++;
                }
            }
            return unlocked >= FarmState.PlotCount ? 0 : 35 + 20 * (unlocked - FarmState.StartingPlotCount);
        }

        private static bool TryGetPlot(FarmState state, int plotIndex, out PlotState plot, out string message)
        {
            plot = null;
            if (state == null) return Fail("No hay una partida activa.", out message);
            state.Validate();
            if (plotIndex < 0 || plotIndex >= state.plots.Count) return Fail("Parcela no válida.", out message);
            plot = state.plots[plotIndex];
            message = string.Empty;
            return true;
        }

        private static bool CanAffordWithoutStranding(FarmState state, int cost)
        {
            if (state.coins - cost >= Crops[0].SeedCost) return true;
            for (int i = 0; i < state.plots.Count; i++)
                if (state.plots[i].unlocked && state.plots[i].cropId >= 0) return true;
            return false;
        }

        private static bool Fail(string reason, out string message) { message = reason; return false; }
    }
}
