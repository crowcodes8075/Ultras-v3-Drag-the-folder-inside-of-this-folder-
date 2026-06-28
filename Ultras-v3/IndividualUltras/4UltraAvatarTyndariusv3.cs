/*
name: UltraAvatarTyndarius_v3
description: Ultra Avatar Tyndarius v3 — Ball1Taunter / Ball2Taunter / TynTaunter / Ball2Killer with army sync.
*/
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreEngine.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreUltra.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/UltraEnhancements.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/UltraPotions.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/UltraGeneral.cs
//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreBots2.cs
//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreAdvanced2.cs

using System;
using System.Linq;
using Skua.Core.Interfaces;

public class UltraAvatarTyndarius_v3
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

    private const string Ball1Taunter = "StoneCrusher";
    private const string Ball2Taunter = "Lord Of Order";
    private const string TynTaunter = "ArchPaladin";
    private const string Ball2Killer = "King's Echo";

    private static readonly string[][] UltraClassesByRole =
    {
        new[] { Ball1Taunter },
        new[] { Ball2Taunter },
        new[] { TynTaunter },
        new[] { Ball2Killer }
    };

    private const double TauntIntervalSeconds = 10.0;
    private const double TauntWindowSeconds = 5.0;
    private DateTime fightStartTime = DateTime.MinValue;
    private DateTime lastTauntTime = DateTime.MinValue;
    private DateTime tyndariusPhaseStart = DateTime.MinValue;
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
        return className == Ball1Taunter || className == Ball2Taunter || className == TynTaunter;
    }

    private bool IsTynTaunter()
    {
        return Bot.Player.CurrentClass?.Name == TynTaunter;
    }

    private double GetTauntOffsetSeconds(string? className)
    {
        return 0;
    }

    private void EquipPresetClasses()
    {
        int armySize = 4;
        bool allowDuplicates = armySize > UltraClassesByRole.Length;

        C.Logger($"[UltraAvatarTyndarius-v3] Equipping role-based ultra classes for army size {armySize}.");
        string[][] classSlots = new string[armySize][];

        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClassesByRole.Length ? UltraClassesByRole[i] : UltraClassesByRole[0];
        }

        Ultra.EquipClassSync(classSlots, armySize, "ultra_tyndarius_class-v3.sync", allowDuplicates);
    }

    private void Prep()
    {
        UltraGeneral.EquipWarriorClass();
        EquipPresetClasses();
        Bot.Sleep(3000);

        bool skipThird = IsTaunter();
        Enh.ApplyTyndarius();
        Pots.EnsureRecommendedPotions(skipThird: skipThird);
        Pots.UseRecommendedPotions(skipThird: skipThird, ensureStock: false);

        if (skipThird)
        {
            C.Logger("[UltraAvatarTyndarius-v3] Taunter detected, acquiring Scroll of Enrage after potion setup.");
            Ultra.GetScrollOfEnrage();
        }

        string? className = Bot.Player.CurrentClass?.Name;
        tauntOffsetSeconds = GetTauntOffsetSeconds(className);
        C.Logger($"[UltraAvatarTyndarius-v3] Taunt offset: {tauntOffsetSeconds}s for class: {className}");
    }

    private void Fight()
    {
        const string map = "ultratyndarius";
        const string boss = "Ultra Avatar Tyndarius";
        const string drop = "Avatar Tyndarius Insignia";
        const string BossDefeated = "Ultra Avatar Tyndarius Defeated";

        const string waitSyncFile = "ultra_tyndarius.sync";
        const string wipeDeadSyncFile = "UltraAvatarTyndariusWipeDead.sync";
        const string wipeAliveSyncFile = "UltraAvatarTyndariusWipeAlive.sync";
        
        const int questId = 8245;
        
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
                C.Logger("Ultra Avatar Tyndarius defeated. Finishing quest.");
                Engine.Join(map);
                Ultra.JoinHouse();
                UltraGeneral.CompleteQuest(Bot, questId);
                Bot.Sleep(30000);
                break;
            }

            if (Bot.Map.Name != map)
                Engine.Join(map);

            if (Bot.Player.Cell != "Boss")
            {
                Bot.Map.Jump("Boss", "Left", autoCorrect: false);
                Bot.Wait.ForCellChange("Boss");
            }

            bool ball1Alive = Bot.Monsters.CurrentAvailableMonsters.Any(x => x != null && x.Alive && x.MapID == 1);
            bool ball2Alive = Bot.Monsters.CurrentAvailableMonsters.Any(x => x != null && x.Alive && x.MapID == 3);
            bool bothDead   = !ball1Alive && !ball2Alive;

            // ── Ball roles: Ball1Taunter / Ball2Taunter / Ball2Killer ────────────
            if (Bot.Player.CurrentClass?.Name == Ball1Taunter)
            {
                if (ball1Alive)
                {
                    AttackTarget(1);
                    for (int i = 0; i < 60 && !Bot.ShouldExit; i++)
                    {
                        if (!Bot.Player.Alive) break;
                        AttackTarget(1);
                        Engine.Cast(5);
                        Bot.Sleep(50);
                    }
                }
                else if (ball2Alive)
                {
                    AttackTarget(3);
                    Bot.Sleep(250);
                }
                else
                {
                    AttackTarget(2);
                    Bot.Sleep(250);
                }
            }
            else if (Bot.Player.CurrentClass?.Name == Ball2Taunter)
            {
                if (ball2Alive)
                {
                    AttackTarget(3);
                    for (int i = 0; i < 60 && !Bot.ShouldExit; i++)
                    {
                        if (!Bot.Player.Alive) break;
                        AttackTarget(3);
                        Engine.Cast(5);
                        Bot.Sleep(50);
                    }
                }
                else if (ball1Alive)
                {
                    AttackTarget(1);
                    Bot.Sleep(250);
                }
                else
                {
                    AttackTarget(2);
                    Bot.Sleep(250);
                }
            }
            else if (Bot.Player.CurrentClass?.Name == Ball2Killer)
            {
                if (ball2Alive)
                {
                    AttackTarget(3);
                }
                else if (ball1Alive)
                {
                    AttackTarget(1);
                }
                else
                {
                    AttackTarget(2);
                    Bot.Sleep(250);
                }
            }

            // ── Tyndarius phase (timer-based, TynTaunter always + ball roles when balls dead) ──
            if (bothDead || IsTynTaunter())
            {
                if (tyndariusPhaseStart == DateTime.MinValue)
                {
                    tyndariusPhaseStart = DateTime.UtcNow;
                    C.Logger("Balls down — Tyndarius timer started.");
                }

                AttackTarget(2);

                if (IsTynTaunter())
                {
                    double elapsed = (DateTime.UtcNow - tyndariusPhaseStart).TotalSeconds;
                    double offsetTime = (elapsed - tauntOffsetSeconds) % TauntIntervalSeconds;
                    if (offsetTime < 0)
                        offsetTime += TauntIntervalSeconds;

                    if (offsetTime <= TauntWindowSeconds && (DateTime.UtcNow - lastTauntTime).TotalSeconds >= TauntIntervalSeconds - 1)
                    {
                        lastTauntTime = DateTime.UtcNow;
                        C.Logger($"[UltraAvatarTyndarius-v3] Tyndarius taunt window active at {elapsed:F1}s.");

                        for (int i = 0; i < 60 && !Bot.ShouldExit; i++)
                        {
                            if (!Bot.Player.Alive) break;
                            AttackTarget(2);
                            Engine.Cast(5);
                            Bot.Sleep(50);
                        }

                        C.Logger("[UltraAvatarTyndarius-v3] Tyndarius taunt done — 60 presses complete.");
                    }
                    else
                    {
                        AttackTarget(2);
                        Bot.Sleep(250);
                    }
                }
                else
                {
                    Bot.Sleep(250);
                }
            }

            Pots.ActivateEquippedPotion();
            Bot.Sleep(500);
        }
    }

    private void AttackTarget(int mapID)
    {
        if (!Bot.Player.HasTarget || Bot.Player.Target == null || !Bot.Player.Target.Alive || Bot.Player.Target.MapID != mapID)
            Bot.Combat.Attack(mapID);
    }
}
