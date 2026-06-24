/*
name: UltraAvatarTyndarius_v3
description: Ultra Avatar Tyndarius helper for Ultras-v3 with ball1/ball2/tyndarius taunting roles.
*/
//cs_include Scripts/Ultras-v3/UltraDependencies/CoreEngine.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/CoreUltra.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/UltraPotions.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/UltraGeneral.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/UltraEnhancements.cs
//cs_include Scripts/Ultras-v3/CoreDependencies/CoreBots2.cs
//cs_include Scripts/Ultras-v3/CoreDependencies/CoreAdvanced2.cs

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

    private static readonly string[] UltraClasses = new[]
    {
        "Verus DoomKnight",
        "King's Echo",
        "StoneCrusher",
        "Lord Of Order"
    };

    private const string Ball2TaunterClass = "Verus DoomKnight";
    private const string Ball2KillerClass = "King's Echo";
    private const string TynTaunter1Class = "StoneCrusher";
    private const string TynTaunter2Class = "Lord Of Order";

    private const int FixedArmySize = 4;

    private bool isBall2Taunter;
    private bool isBall2Killer;
    private bool isTynTaunter;
    private double tynTauntOffset;
    private DateTime tynFightStartTime = DateTime.MinValue;
    private DateTime tynLastTaunt = DateTime.MinValue;
    private const double TynTauntInterval = 10.0;
    private const double TynTauntWindow = 5.0;

    public void ScriptMain(IScriptInterface bot)
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
            Bot.StopSync();
        }
    }

    private void EquipPresetClasses()
    {
        int armySize = GetBossParticipantCount();
        bool allowDuplicates = armySize > UltraClasses.Length;

        string[][] classSlots = new string[armySize][];
        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClasses.Length
                ? new[] { UltraClasses[i] }
                : UltraClasses;
        }

        Ultra.EquipClassSync(
            classSlots,
            armySize,
            "tyndarius_class-v3.sync",
            allowDuplicates
        );
    }

    private int GetBossParticipantCount() => FixedArmySize;

    private void Prep()
    {
        EquipPresetClasses();

        string currentClass = Bot.Player.CurrentClass?.Name ?? string.Empty;
        isBall2Taunter = string.Equals(currentClass, Ball2TaunterClass, StringComparison.OrdinalIgnoreCase);
        isBall2Killer = string.Equals(currentClass, Ball2KillerClass, StringComparison.OrdinalIgnoreCase);
        isTynTaunter = string.Equals(currentClass, TynTaunter1Class, StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentClass, TynTaunter2Class, StringComparison.OrdinalIgnoreCase);

        if (string.Equals(currentClass, TynTaunter1Class, StringComparison.OrdinalIgnoreCase))
            tynTauntOffset = 0;
        else if (string.Equals(currentClass, TynTaunter2Class, StringComparison.OrdinalIgnoreCase))
            tynTauntOffset = 5;
        else
            tynTauntOffset = 0;

        bool skipThird = isBall2Taunter || isTynTaunter;

        Enh.Apply();
        Pots.EnsureRecommendedPotions(skipThird: skipThird);
        EquipPresetClasses();
        Pots.UseRecommendedPotions(skipThird: skipThird, ensureStock: false);

        if (skipThird)
        {
            C.Logger("[UltraAvatarTyndarius-v3] Taunter role detected, acquiring Scroll of Enrage after potion setup.");
            Ultra.GetScrollOfEnrage();
        }

        C.Logger($"UltraAvatarTyndarius-v3 role: Ball2Taunter={isBall2Taunter}, Ball2Killer={isBall2Killer}, TynTaunter={isTynTaunter}, Offset={tynTauntOffset}s");
    }

    private void Fight()
    {
        const string map = "ultratyndarius";
        const string boss = "Ultra Avatar Tyndarius";

        string syncPath = Ultra.ResolveSyncPath("UltraItemCheck.sync");
        string wipeDeadSyncPath = Ultra.ResolveSyncPath("UltraAvatarTyndariusWipeDead.sync");
        string wipeAliveSyncPath = Ultra.ResolveSyncPath("UltraAvatarTyndariusWipeAlive.sync");

        Ultra.ClearSyncFile(syncPath);
        Ultra.ClearSyncFile(wipeDeadSyncPath);
        Ultra.ClearSyncFile(wipeAliveSyncPath);
        Bot.Sleep(2500);

        C.AddDrop("Avatar Tyndarius Insignia");
        C.EnsureAccept(8245);
        Engine.Join(map);
        Bot.Sleep(2500);

        int armySize = GetBossParticipantCount();
        Ultra.WaitForArmy(armySize - 1, "ultra_tyndarius.sync");

        Engine.ChooseBestCell(boss);
        Bot.Player.SetSpawnPoint();
        Engine.EnableSkills();

        bool armyWipeDetected = false;

        while (!Bot.ShouldExit)
        {
            bool allDead = UltraGeneral.IsWholeArmyDead(Ultra, Bot, wipeDeadSyncPath);
            if (allDead)
            {
                if (!armyWipeDetected)
                    C.Logger("Army wipe detected — all clients dead.");
                armyWipeDetected = true;
            }

            if (armyWipeDetected)
            {
                bool allAlive = UltraGeneral.IsWholeArmyAlive(Ultra, Bot, wipeAliveSyncPath);
                if (allAlive)
                {
                    C.Logger("Army wipe recovered — all clients alive again.");
                    Ultra.ClearSyncFile(wipeDeadSyncPath);
                    Ultra.ClearSyncFile(wipeAliveSyncPath);
                    Bot.Combat.CancelTarget();
                    tynFightStartTime = DateTime.MinValue;
                    tynLastTaunt = DateTime.MinValue;
                    armyWipeDetected = false;
                    C.Logger("Army wipe recovered — resetting timer and resuming fight.");
                    continue;
                }

                Bot.Combat.CancelTarget();
                C.Logger("Army wipe active — waiting for everyone to respawn before fighting.");
                Bot.Sleep(250);
                continue;
            }

            if (!Bot.Player.Alive)
            {
                Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                continue;
            }

            if (Ultra.CheckArmyProgressBool(
                () => Bot.TempInv.Contains("Ultra Avatar Tyndarius Defeated", 1),
                syncPath))
            {
                C.Logger("All players finished farm.");
                Engine.Join(map);
                Ultra.JoinHouse();
                if (UltraGeneral.IsQuestGreen(Bot, 8245))
                    C.EnsureComplete(8245);
                Bot.Sleep(30000);
                break;
            }

            bool ball1Alive = Bot.Monsters.CurrentAvailableMonsters.Any(m => m != null && m.Alive && m.MapID == 1);
            bool ball2Alive = Bot.Monsters.CurrentAvailableMonsters.Any(m => m != null && m.Alive && m.MapID == 3);
            bool bothDead = !ball1Alive && !ball2Alive;

            if (isBall2Taunter)
            {
                if (ball2Alive)
                {
                    AttackTarget(3);
                    for (int i = 0; i < 20 && !Bot.ShouldExit; i++)
                    {
                        if (!Bot.Player.Alive) break;
                        AttackTarget(3);
                        Engine.Cast(5);
                        Bot.Sleep(25);
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
            else if (isBall2Killer)
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

            if (bothDead || (!isBall2Taunter && !isBall2Killer))
            {
                if (tynFightStartTime == DateTime.MinValue)
                {
                    tynFightStartTime = DateTime.UtcNow;
                    C.Logger("Balls down — Tyndarius timer started.");
                }

                AttackTarget(2);

                if (isTynTaunter)
                {
                    double elapsed = (DateTime.UtcNow - tynFightStartTime).TotalSeconds;
                    double cycle = (elapsed - tynTauntOffset) % TynTauntInterval;
                    if (cycle < 0)
                        cycle += TynTauntInterval;

                    bool inWindow = cycle >= 0 && cycle < TynTauntWindow;
                    bool cooled = (DateTime.UtcNow - tynLastTaunt).TotalSeconds >= TynTauntInterval - 1;

                    if (inWindow && cooled)
                    {
                        tynLastTaunt = DateTime.UtcNow;
                        C.Logger($"Tyndarius taunt ({elapsed:F1}s, offset {tynTauntOffset}s)");
                        for (int i = 0; i < 60 && !Bot.ShouldExit; i++)
                        {
                            if (!Bot.Player.Alive) break;
                            AttackTarget(2);
                            Engine.Cast(5);
                            Bot.Sleep(50);
                        }
                        C.Logger("Tyndarius taunt done.");
                        Bot.Sleep(100);
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
            Bot.Sleep(100);
        }
    }

    private void AttackTarget(int mapID)
    {
        if (!Bot.Player.HasTarget || Bot.Player.Target == null || !Bot.Player.Target.Alive || Bot.Player.Target.MapID != mapID)
            Bot.Combat.Attack(mapID);
    }
}
