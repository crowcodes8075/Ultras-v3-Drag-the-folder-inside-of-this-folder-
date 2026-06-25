/*
name: ChampionDrakath_v3
description: Champion Drakath v3 — threshold-based taunt timing with army sync and HP-reset detection.
*/
//cs_include Scripts/Ultras-v3/UltraDependencies/CoreEngine.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/CoreUltra.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/UltraEnhancements.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/UltraPotions.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/UltraGeneral.cs
//cs_include Scripts/Ultras-v3/CoreDependencies/CoreBots2.cs
//cs_include Scripts/Ultras-v3/CoreDependencies/CoreAdvanced2.cs

using System;
using System.Linq;
using Skua.Core.Interfaces;

public class ChampionDrakath_v3
{
    private static IScriptInterface Bot => IScriptInterface.Instance;
    private static CoreBots C => CoreBots.Instance;
    private static CoreEngine Engine => _Engine ??= new CoreEngine();
    private static CoreEngine _Engine;
    private static CoreUltra Ultra => _Ultra ??= new CoreUltra();
    private static CoreUltra _Ultra;
    private static UltraEnhancements Enh => _Enh ??= new UltraEnhancements();
    private static UltraEnhancements _Enh;
    private static UltraPotions Pots => _Pots ??= new UltraPotions();
    private static UltraPotions _Pots;

    private const string Taunter1 = "Verus DoomKnight";
    private const string Taunter2 = "Lord Of Order";
    private const string Taunter3 = "StoneCrusher";
    private const string Dps1 = "King's Echo";

    private static readonly string[][] UltraClassesByRole =
    {
        new[] { Taunter1 },
        new[] { Taunter2 },
        new[] { Taunter3 },
        new[] { Dps1 }
    };

    private static readonly int[] roundThresholds = { 18000000, 16000000, 14000000, 12000000, 10000000, 8000000, 6000000, 4000000 };
    private bool[] tauntFired = new bool[8];
    private int previousHP = 0;
    private int myIndex = -1;
    private int taunterCount = 3;
    private int buffer = 250000;

    // Kept for ArmyWipeHelperWithTaunters compatibility
    private DateTime fightStartTime = DateTime.MinValue;
    private DateTime lastTauntTime = DateTime.MinValue;

    public void ScriptMain(IScriptInterface bot)
    {
        RunBoss();
        Bot.StopSync();
    }

    public void RunBoss()
    {
        C.SetOptions(disableCoreSkills: true);
        Engine.Boot();

        try
        {
            Prep();
            Fight();
        }
        finally
        {
            Engine.DisableSkills();
            C.SetOptions(false);
        }
    }

    private bool IsTaunter()
    {
        string? className = Bot.Player.CurrentClass?.Name;
        return className == Taunter1 || className == Taunter2 || className == Taunter3;
    }

    private int MyTaunterIndex()
    {
        string? className = Bot.Player.CurrentClass?.Name;
        if (className == Taunter1) return 0;
        if (className == Taunter2) return 1;
        if (className == Taunter3) return 2;
        return -1;
    }

    private void EquipPresetClasses()
    {
        int armySize = 4;
        bool allowDuplicates = armySize > UltraClassesByRole.Length;

        C.Logger($"[ChampionDrakath-v3] Equipping role-based ultra classes for army size {armySize}.");
        string[][] classSlots = new string[armySize][];

        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClassesByRole.Length ? UltraClassesByRole[i] : UltraClassesByRole[0];
        }

        Ultra.EquipClassSync(classSlots, armySize, "champion_drakath_class-v3.sync", allowDuplicates);
    }

    private void Prep()
    {
        EquipPresetClasses();

        bool skipThird = IsTaunter();
        Enh.ApplyDrakath();
        Pots.EnsureRecommendedPotions(skipThird: skipThird);
        Pots.UseRecommendedPotions(skipThird: skipThird, ensureStock: false);

        if (skipThird)
        {
            C.Logger("[ChampionDrakath-v3] Taunter detected, acquiring Scroll of Enrage after potion setup.");
            Ultra.GetScrollOfEnrage();
        }

        EquipPresetClasses();

        myIndex = MyTaunterIndex();
        C.Logger($"[ChampionDrakath-v3] Taunter index: {myIndex}");
    }

    private void Fight()
    {
        const string map = "championdrakath";
        const string boss = "Champion Drakath";
        const string drop = "Champion Drakath Insignia";
        const string BossDefeated = "Champion Drakath Defeated";

        const string waitSyncFile = "champion_drakath.sync";
        const string wipeDeadSyncFile = "ChampionDrakathWipeDead.sync";
        const string wipeAliveSyncFile = "ChampionDrakathWipeAlive.sync";

        const int questId = 8300;
        

        if (!UltraGeneral.IsQuestGreen(Bot, questId))
            UltraGeneral.EnsureAcceptOnce(Bot, questId);

        C.AddDrop(drop);
        Ultra.ClearSyncFile(waitSyncFile);
        Ultra.ClearSyncFile(wipeDeadSyncFile);
        Ultra.ClearSyncFile(wipeAliveSyncFile);

        Engine.Join(map);
        Bot.Sleep(2500);

        int armySize = 4;
        Ultra.WaitForArmy(armySize - 1, waitSyncFile);

        Engine.ChooseBestCell(boss);
        Bot.Player.SetSpawnPoint();
        Engine.EnableSkills();

        previousHP = 0;
        for (int j = 0; j < tauntFired.Length; j++)
            tauntFired[j] = false;
        fightStartTime = DateTime.UtcNow;

        int[] thresholds = roundThresholds.Select(t => t + buffer).ToArray();
        C.Logger($"[ChampionDrakath-v3] Thresholds: [{string.Join(", ", thresholds.Select(t => $"{t:n0}"))}] (buffer: {buffer:n0})");

        bool armyWipeDetected = false;
        bool bossWasAlive = false;

        while (!Bot.ShouldExit)
        {
            if (!Bot.Player.Alive)
            {
                Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                continue;
            }

            if (UltraGeneral.ArmyWipeHelperWithTaunters(Ultra, Bot, wipeDeadSyncFile, wipeAliveSyncFile, ref armyWipeDetected, ref fightStartTime, ref lastTauntTime))
                continue;

            // Track whether we've ever seen the boss alive (avoids false defeat before spawn)
            if (!bossWasAlive)
            {
                var m = Bot.Monsters.CurrentAvailableMonsters.FirstOrDefault(x => x?.Name == boss);
                if (m != null && m.HP > 0)
                    bossWasAlive = true;
            }

            if (bossWasAlive && Ultra.CheckArmyProgressBool(() =>
            {
                var m = Bot.Monsters.CurrentAvailableMonsters.FirstOrDefault(x => x?.Name == boss);
                return m == null || m.HP <= 0;
            }, waitSyncFile))
            {
                C.Logger("Champion Drakath defeated. Finishing quest.");
                Engine.Join(map);
                Ultra.JoinHouse();
                UltraGeneral.CompleteQuest(Bot, questId);
                Bot.Sleep(30000);
                break;
            }

            Bot.Combat.Attack(boss);
            Pots.ActivateEquippedPotion();

            // ── Threshold-based taunt logic ────────────────────────────────────
            if (myIndex >= 0
                && Bot.Player.HasTarget
                && Bot.Player.Target?.HP > 0)
            {
                // Detect HP reset (boss respawned after wipe)
                if (Bot.Player.Target?.HP > previousHP + 1000000)
                {
                    C.Logger("[ChampionDrakath-v3] Boss HP reset detected - clearing taunt flags");
                    for (int j = 0; j < tauntFired.Length; j++)
                        tauntFired[j] = false;
                }

                previousHP = Bot.Player.Target?.HP ?? 0;

                // Check thresholds (18M down to 4M) — each assigned round-robin by taunter index
                for (int i = 0; i < thresholds.Length; i++)
                {
                    if (tauntFired[i])
                        continue;

                    if (Bot.Player.Target?.HP > thresholds[i])
                        continue;

                    // Only this taunter's assigned thresholds
                    if (i % taunterCount != myIndex)
                    {
                        tauntFired[i] = true;
                        continue;
                    }

                    C.Logger($"[ChampionDrakath-v3] {roundThresholds[i] / 1000000}M threshold — my turn (slot {myIndex}), taunting!");

                    Bot.Combat.Attack(boss);

                    for (int p = 0; p < 60 && !Bot.ShouldExit; p++)
                    {
                        if (!Bot.Player.Alive)
                        {
                            Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                            for (int j = i; j < tauntFired.Length; j++)
                                tauntFired[j] = false;
                            break;
                        }
                        Engine.Cast(5);
                        Bot.Sleep(50);
                    }

                    tauntFired[i] = true;
                    Bot.Sleep(100);
                    break;
                }

                // After 2M → all taunters continuously taunt
                if (Bot.Player.HasTarget && Bot.Player.Target?.HP <= 2100000)
                {
                    C.Logger($"[ChampionDrakath-v3] HP < 2M — continuous taunt (slot {myIndex})");
                    Bot.Combat.Attack(boss);

                    for (int p = 0; p < 60 && !Bot.ShouldExit; p++)
                    {
                        if (!Bot.Player.Alive) break;
                        Engine.Cast(5);
                        Bot.Sleep(50);
                    }

                    Bot.Sleep(100);
                }
            }

            Bot.Sleep(100);
        }
    }
}
