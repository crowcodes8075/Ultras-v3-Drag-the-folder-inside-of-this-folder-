/*
name: UltraWarden_v3
description: Ultra Warden v3 — timer-based 2-taunter rotation with army synchronization.
*/
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreEngine.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreUltra.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/UltraEnhancements.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/UltraPotions.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/UltraGeneral.cs
//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreBots2.cs
//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreAdvanced2.cs

using System;
using Skua.Core.Interfaces;

public class UltraWarden_v3
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
    // DPS classes
    private const string Dps1 = "King's Echo";
    private const string Dps2 = "StoneCrusher";

    private static readonly string[][] UltraClassesByRole =
    {
        new[] { Taunter1 },
        new[] { Taunter2 },
        new[] { Dps1 },
        new[] { Dps2 }
    };

    private const double TauntIntervalSeconds = 10.0;
    private const double TauntWindowSeconds = 5.0;
    private DateTime fightStartTime = DateTime.MinValue;
    private DateTime lastTauntTime = DateTime.MinValue;
    private double tauntOffsetSeconds = 0;

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
        return className == Taunter1 || className == Taunter2;
    }

    private double GetTauntOffsetSeconds(string? className)
    {
        if (className == null) return 0;
        return className switch
        {
            Taunter1 => 0,
            Taunter2 => 5,
            _ => 0
        };
    }

    private void EquipPresetClasses()
    {
        int armySize = 4;
        bool allowDuplicates = armySize > UltraClassesByRole.Length;

        C.Logger($"[UltraWarden-v3] Equipping role-based ultra classes for army size {armySize}.");
        string[][] classSlots = new string[armySize][];

        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClassesByRole.Length ? UltraClassesByRole[i] : UltraClassesByRole[0];
        }

        Ultra.EquipClassSync(classSlots, armySize, "ultra_warden_class-v3.sync", allowDuplicates);
    }

    private void Prep()
    {
        UltraGeneral.EquipWarriorClass();
        EquipPresetClasses();
        Bot.Sleep(3000);

        bool skipThird = IsTaunter();
        Enh.Apply();
        Pots.EnsureRecommendedPotions(skipThird: skipThird);
        Pots.UseRecommendedPotions(skipThird: skipThird, ensureStock: false);

        if (skipThird)
        {
            C.Logger("[UltraWarden-v3] Taunter detected, acquiring Scroll of Enrage after potion setup.");
            Ultra.GetScrollOfEnrage();
        }

        string? className = Bot.Player.CurrentClass?.Name;
        tauntOffsetSeconds = GetTauntOffsetSeconds(className);
        C.Logger($"[UltraWarden-v3] Taunt offset: {tauntOffsetSeconds}s for class: {className}");
    }

    private void Fight()
    {
        const string map = "ultrawarden";
        const string boss = "Ultra Warden";
        const string drop = "Warden Insignia";
        const string BossDefeated = "Ultra Warden Defeated";

        const string waitSyncFile = "ultra_warden.sync";
        const string wipeDeadSyncFile = "UltraWardenWipeDead.sync";
        const string wipeAliveSyncFile = "UltraWardenWipeAlive.sync";

        const int questId = 8153;

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

        fightStartTime = DateTime.UtcNow;
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
                if (m != null && m.Alive)
                    bossWasAlive = true;
            }

            if (bossWasAlive && Ultra.CheckArmyProgressBool(() =>
            {
                var m = Bot.Monsters.CurrentAvailableMonsters.FirstOrDefault(x => x?.Name == boss);
                return m == null || !m.Alive;
            }, waitSyncFile))
            {
                C.Logger("Ultra Warden defeated. Finishing quest.");
                Engine.Join(map);
                Ultra.JoinHouse();
                UltraGeneral.CompleteQuest(Bot, questId);
                Bot.Sleep(30000);
                break;
            }

            Bot.Combat.Attack(boss);
            Pots.ActivateEquippedPotion();

            if (IsTaunter() && Bot.Player.HasTarget)
            {
                double elapsed = (DateTime.UtcNow - fightStartTime).TotalSeconds;
                double offsetTime = (elapsed - tauntOffsetSeconds) % TauntIntervalSeconds;
                if (offsetTime < 0)
                    offsetTime += TauntIntervalSeconds;

                if (offsetTime <= TauntWindowSeconds && (DateTime.UtcNow - lastTauntTime).TotalSeconds >= TauntIntervalSeconds - 1)
                {
                    lastTauntTime = DateTime.UtcNow;
                    C.Logger($"[UltraWarden-v3] Taunt window active at {elapsed:F1}s.");

                    for (int i = 0; i < 60 && !Bot.ShouldExit; i++)
                    {
                        if (!Bot.Player.Alive)
                            break;

                        Engine.Cast(5);
                        Bot.Sleep(50);
                    }

                    C.Logger("[UltraWarden-v3] Taunt done — 60 presses complete.");
                }
            }

            Bot.Sleep(500);
        }
    }
}
