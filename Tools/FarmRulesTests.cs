using System;
using System.Collections.Generic;
using CozyFarm;

// Compile this file with FarmState.cs. No Unity editor or testing package required.
public static class FarmRulesTests
{
    private static int assertions;

    public static int Main(string[] args)
    {
        try
        {
            NewGameAndCropLocks();
            CompleteFarmingLoop();
            PurchasesAreAtomic();
            GrowthUsesElapsedTime();
            CorruptSavesAreSanitized();
            BoundariesAndSaturation();
            Console.WriteLine("PASS: " + assertions + " assertions across 6 farming scenarios.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }

    private static void NewGameAndCropLocks()
    {
        FarmState state = FarmState.NewGame();
        Equal(1, state.version, "Versión de guardado");
        Equal(18, state.plots.Count, "Total parcelas");
        Equal(30, state.coins, "Monedas iniciales");
        Equal(1, state.Level, "Nivel inicial");
        Equal(40, state.NextLevelExperience, "XP necesaria para nivel 2");
        for (int i = 0; i < state.plots.Count; i++)
        {
            Check(state.plots[i].unlocked == (i < 6), "Seis parcelas iniciales");
            Equal(-1, state.plots[i].cropId, "Parcela inicialmente vacía");
        }
        string message;
        Check(!FarmRules.TryPlant(state, 0, 1, out message), "Trigo bloqueado en nivel 1");
        Equal(30, state.coins, "Semilla bloqueada no cobra");
        Check(!FarmRules.TryPlant(state, 6, 0, out message), "Parcela bloqueada no se siembra");
        Check(!FarmRules.TryPlant(state, 0, -1, out message), "ID negativo rechazado");
        Check(!FarmRules.TryPlant(state, 0, 999, out message), "ID desconocido rechazado");
        state.experience = 40;
        Check(FarmRules.TryPlant(state, 0, 1, out message), "Trigo disponible en nivel 2");
        Check(!FarmRules.TryPlant(state, 1, 2, out message), "Calabaza bloqueada antes de nivel 3");
        state.experience = 120;
        Check(FarmRules.TryPlant(state, 1, 2, out message), "Calabaza disponible en nivel 3");
        Equal(3, state.Level, "Umbral exacto nivel 3");
        Equal(240, state.NextLevelExperience, "Siguiente umbral acumulado");
    }

    private static void CompleteFarmingLoop()
    {
        FarmState state = FarmState.NewGame();
        string message;
        for (int harvest = 0; harvest < 5; harvest++)
        {
            int before = state.coins;
            Check(FarmRules.TryPlant(state, 0, 0, out message), "Siembra válida");
            Equal(before - 4, state.coins, "Cobro semilla");
            Check(!FarmRules.TryPlant(state, 0, 0, out message), "No sobrescribir cultivo");
            Check(!FarmRules.TryHarvest(state, 0, out message), "No cosechar antes de madurar");
            Check(FarmRules.TryWater(state, 0, out message), "Riego válido");
            Check(!FarmRules.TryWater(state, 0, out message), "No acumular riegos");
            FarmRules.Tick(state, 19f);
            Near(0.95f, FarmRules.Progress(state, 0), 0.0001f, "Riego acelera 1.5x");
            Check(!FarmRules.TryHarvest(state, 0, out message), "No cosechar al 95%");
            FarmRules.Tick(state, 1f);
            Check(FarmRules.TryHarvest(state, 0, out message), "Cosecha madura");
            Equal(before + 6, state.coins, "Ganancia neta correcta");
            Check(!FarmRules.TryHarvest(state, 0, out message), "No duplicar cosecha");
            Equal(-1, state.plots[0].cropId, "Parcela vuelve vacía");
            Check(!state.plots[0].watered, "Riego no persiste entre cultivos");
        }
        Equal(60, state.coins, "Economía tras cinco cosechas");
        Equal(40, state.experience, "XP tras cinco cosechas");
        Equal(5, state.harvests, "Número de cosechas");
        Equal(2, state.Level, "Cinco zanahorias alcanzan nivel 2");
        Check(FarmRules.TryPlant(state, 0, 1, out message), "Desbloqueo de trigo por jugar");
    }

    private static void PurchasesAreAtomic()
    {
        FarmState state = FarmState.NewGame();
        string message;
        Check(!FarmRules.TryUnlock(state, 6, out message), "Saldo insuficiente para parcela");
        Equal(30, state.coins, "Compra fallida conserva saldo");
        Check(!state.plots[6].unlocked, "Compra fallida conserva bloqueo");
        state.coins = 35;
        Check(!FarmRules.TryUnlock(state, 7, out message), "No saltar parcelas");
        Check(!FarmRules.TryUnlock(state, 6, out message), "No gastar todas las monedas sin cultivos");
        Equal(35, state.coins, "Reserva de semillas conserva saldo");
        state.coins = 39;
        Check(FarmRules.TryUnlock(state, 6, out message), "Comprar con reserva de semillas");
        Equal(4, state.coins, "Compra mantiene semilla disponible");
        Equal(55, FarmRules.UnlockCost(state), "Coste siguiente parcela");
        Check(!FarmRules.TryUnlock(state, 6, out message), "No cobrar parcela ya comprada");
        Check(FarmRules.TryPlant(state, 0, 0, out message), "Sembrar con reserva");
        Check(!FarmRules.TryPlant(state, 1, 0, out message), "No sembrar sin dinero");
        Check(!FarmRules.TryUpgrade(state, out message), "No mejorar sin dinero");
        FarmRules.Tick(state, 30f);
        Check(FarmRules.TryHarvest(state, 0, out message), "Cultivo permite recuperar saldo");
        state.coins = 60;
        Check(!FarmRules.TryUpgrade(state, out message), "Mejora también reserva semillas sin cultivos");
        Equal(60, state.coins, "Mejora rechazada por reserva conserva saldo");
        state.coins = 64;
        Check(FarmRules.TryUpgrade(state, out message), "Comprar primera mejora");
        Equal(1, state.wateringLevel, "Nivel regadera");
        Equal(4, state.coins, "Coste mejora cobrado");
        Equal(120, FarmRules.UpgradeCost(state), "Coste mejora progresivo");
        Check(FarmRules.TryPlant(state, 0, 0, out message), "Siembra posterior a mejora");
        Check(FarmRules.TryWater(state, 0, out message), "Riego mejorado");
        FarmRules.Tick(state, 10f);
        Near(17.5f, state.plots[0].growthSeconds, 0.0001f, "Mejora aumenta velocidad regada");
        state.coins = 100000;
        for (int i = 7; i < FarmState.PlotCount; i++) Check(FarmRules.TryUnlock(state, i, out message), "Expandir granja completa");
        Equal(0, FarmRules.UnlockCost(state), "Sin coste al completar granja");
        while (state.wateringLevel < FarmRules.MaximumWateringLevel) Check(FarmRules.TryUpgrade(state, out message), "Mejorar hasta límite");
        int balance = state.coins;
        Check(!FarmRules.TryUpgrade(state, out message), "Respetar límite mejoras");
        Equal(balance, state.coins, "Mejora máxima no cobra");
        Equal(0, FarmRules.UpgradeCost(state), "Sin coste al completar mejoras");

        FarmState backedByCrop = FarmState.NewGame();
        FarmRules.TryPlant(backedByCrop, 0, 0, out message);
        backedByCrop.coins = 35;
        Check(FarmRules.TryUnlock(backedByCrop, 6, out message), "Compra con saldo exacto si hay cultivo activo");
        Equal(0, backedByCrop.coins, "Compra exacta nunca genera deuda");
        FarmRules.Tick(backedByCrop, 30f);
        Check(FarmRules.TryHarvest(backedByCrop, 0, out message), "Cultivo activo recupera saldo tras compra exacta");
        Equal(10, backedByCrop.coins, "Economía puede continuar desde saldo cero");
    }

    private static void GrowthUsesElapsedTime()
    {
        FarmState fast = FarmState.NewGame();
        FarmState slow = FarmState.NewGame();
        string message;
        FarmRules.TryPlant(fast, 0, 0, out message);
        FarmRules.TryPlant(slow, 0, 0, out message);
        for (int i = 0; i < 600; i++) FarmRules.Tick(fast, 1f / 60f);
        for (int i = 0; i < 100; i++) FarmRules.Tick(slow, 0.1f);
        Near(fast.plots[0].growthSeconds, slow.plots[0].growthSeconds, 0.001f, "Crecimiento independiente de FPS");
        Near(10f, slow.plots[0].growthSeconds, 0.001f, "Tiempo transcurrido correcto");
        Near(10f, (float)fast.worldSeconds, 0.001f, "Reloj compartido");
        foreach (int frameRate in new[] { 15, 30, 60, 120, 144, 240 })
        {
            FarmState highFrameRate = FarmState.NewGame();
            FarmRules.TryPlant(highFrameRate, 0, 0, out message);
            for (int i = 0; i < frameRate * 30; i++) FarmRules.Tick(highFrameRate, 1f / frameRate);
            Near(1f, FarmRules.Progress(highFrameRate, 0), 0, "Madurez consistente después de 30 s a " + frameRate + " FPS");
            Check(FarmRules.TryHarvest(highFrameRate, 0, out message), "Cosecha tras duración exacta a " + frameRate + " FPS");
            highFrameRate.experience = 120;
            FarmRules.TryPlant(highFrameRate, 0, 2, out message);
            for (int i = 0; i < frameRate * 90; i++) FarmRules.Tick(highFrameRate, 1f / frameRate);
            Near(1f, FarmRules.Progress(highFrameRate, 0), 0, "Cultivo largo madura después de 90 s a " + frameRate + " FPS");
        }
        float before = fast.plots[0].growthSeconds;
        FarmRules.Tick(fast, float.NaN);
        FarmRules.Tick(fast, float.PositiveInfinity);
        FarmRules.Tick(fast, -100f);
        FarmRules.Tick(fast, 0f);
        Near(before, fast.plots[0].growthSeconds, 0, "Deltas inválidos no modifican cultivo");
        double elapsed = fast.worldSeconds;
        FarmRules.Tick(fast, float.MaxValue);
        Near(60f, (float)(fast.worldSeconds - elapsed), 0.001f, "Delta extremo acotado");
        Near(1f, FarmRules.Progress(fast, 0), 0, "Progreso saturado al madurar");
        FarmRules.Tick(fast, 60f);
        Near(30f, fast.plots[0].growthSeconds, 0, "Cultivo maduro no sigue acumulando");
    }

    private static void CorruptSavesAreSanitized()
    {
        FarmState state = new FarmState();
        state.coins = -25;
        state.experience = -1;
        state.harvests = -10;
        state.wateringLevel = int.MaxValue;
        state.worldSeconds = double.NaN;
        state.selectedCrop = 999;
        state.plots = new List<PlotState>
        {
            null,
            new PlotState { cropId = 123, growthSeconds = 999, watered = true },
            new PlotState { cropId = 0, growthSeconds = float.NaN },
            new PlotState { cropId = 0, growthSeconds = float.PositiveInfinity },
            new PlotState { cropId = 0, growthSeconds = -10 },
            new PlotState { cropId = 2, growthSeconds = 90 },
            new PlotState { cropId = 0, growthSeconds = 30, unlocked = false },
            new PlotState { cropId = 0, growthSeconds = 30, unlocked = true }
        };
        state.Validate();
        Equal(18, state.plots.Count, "Restaurar parcelas faltantes");
        Equal(0, state.coins, "Saldo corrupto corregido");
        Equal(0, state.experience, "XP corrupta corregida");
        Equal(0, state.harvests, "Cosechas corruptas corregidas");
        Equal(5, state.wateringLevel, "Mejoras acotadas");
        Near(0, (float)state.worldSeconds, 0, "Reloj inválido corregido");
        Equal(0, state.selectedCrop, "Selección inválida corregida");
        Equal(-1, state.plots[1].cropId, "ID corrupto eliminado");
        Check(!state.plots[1].watered, "Riego corrupto eliminado");
        Near(0, state.plots[2].growthSeconds, 0, "NaN no permite cosecha");
        Near(0, state.plots[3].growthSeconds, 0, "Infinito no permite cosecha");
        Near(0, state.plots[4].growthSeconds, 0, "Crecimiento negativo corregido");
        Equal(-1, state.plots[5].cropId, "Cultivo de nivel bloqueado eliminado");
        Equal(-1, state.plots[6].cropId, "Cultivo en parcela bloqueada eliminado");
        Check(!state.plots[7].unlocked, "Desbloqueo fuera de orden corregido");
        for (int i = 0; i < 100; i++) state.plots.Add(new PlotState());
        state.Validate();
        Equal(18, state.plots.Count, "Eliminar parcelas extra");
        state.plots = null;
        state.Validate();
        Equal(18, state.plots.Count, "Lista nula reconstruida");
        state.worldSeconds = double.PositiveInfinity;
        state.Validate();
        Near(0, (float)state.worldSeconds, 0, "Reloj infinito corregido");
    }

    private static void BoundariesAndSaturation()
    {
        FarmState state = FarmState.NewGame();
        string message;
        Check(!FarmRules.TryPlant(null, 0, 0, out message), "Partida nula rechazada");
        Check(!FarmRules.TryPlant(state, -1, 0, out message), "Parcela negativa rechazada");
        Check(!FarmRules.TryWater(state, int.MaxValue, out message), "Parcela enorme rechazada");
        Check(!FarmRules.TryHarvest(state, 18, out message), "Parcela fuera de rango rechazada");
        Check(!FarmRules.TryUnlock(state, -1, out message), "Compra fuera de rango rechazada");
        Check(!FarmRules.TryUpgrade(null, out message), "Mejora sin partida rechazada");
        Check(!FarmRules.TryWater(state, 0, out message), "No regar parcela vacía");
        Near(0, FarmRules.Progress(null, 0), 0, "Progreso sin partida seguro");
        Near(0, FarmRules.Progress(state, -1), 0, "Progreso índice inválido seguro");
        FarmRules.Tick(null, 1f);
        state.coins = int.MaxValue;
        state.experience = int.MaxValue;
        state.harvests = int.MaxValue;
        state.Validate();
        Check(state.NextLevelExperience > state.experience, "Umbral XP no desborda");
        Check(FarmRules.TryPlant(state, 0, 2, out message), "Siembra a contadores máximos");
        FarmRules.Tick(state, 60f);
        FarmRules.Tick(state, 30f);
        Check(FarmRules.TryHarvest(state, 0, out message), "Cosecha a contadores máximos");
        Equal(FarmState.MaximumCounter, state.coins, "Monedas se saturan sin desbordar");
        Equal(FarmState.MaximumCounter, state.experience, "XP se satura sin desbordar");
        Equal(FarmState.MaximumCounter, state.harvests, "Cosechas se saturan sin desbordar");
    }

    private static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition) throw new Exception(label);
    }

    private static void Equal(int expected, int actual, string label)
    {
        Check(expected == actual, label + ": expected " + expected + ", got " + actual);
    }

    private static void Near(float expected, float actual, float tolerance, string label)
    {
        Check(!float.IsNaN(actual) && Math.Abs(expected - actual) <= tolerance,
            label + ": expected " + expected + ", got " + actual);
    }
}
