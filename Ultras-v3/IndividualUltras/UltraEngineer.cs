/*
name: UltraEngineer_v3
description: Ultra Engineer helper for Ultras-v3 with army sync, potions, and enhancements.
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
using Skua.Core.Options;

public class UltraEngineer_v3
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

    private const int FixedArmySize = 4;

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

        Ultra.EquipClassSync(classSlots, armySize, "UltraEngineer-v3.class_assign.sync", allowDuplicates);
    }

    private int GetBossParticipantCount() => FixedArmySize;

    private void Prep()
    {
        EquipPresetClasses();
        Enh.Apply();
        Pots.EnsureRecommendedPotions();
        Pots.UseRecommendedPotions(ensureStock: false);
        EquipPresetClasses();
    }

    private void Fight()
    {
        const string map = "ultraengineer";
        const string boss = "Ultra Engineer";
        const string priority1 = "Defense Drone";
        const string priority2 = "Attack Drone";

        string syncPath = Ultra.ResolveSyncPath("UltraItemCheck.sync");
        string wipeDeadSyncPath = Ultra.ResolveSyncPath("UltraEngineerWipeDead.sync");
        string wipeAliveSyncPath = Ultra.ResolveSyncPath("UltraEngineerWipeAlive.sync");

        Ultra.ClearSyncFile(syncPath);
        Ultra.ClearSyncFile(wipeDeadSyncPath);
        Ultra.ClearSyncFile(wipeAliveSyncPath);

        Bot.Sleep(2500);
        C.EnsureAccept(8154);
        C.AddDrop("Engineer Insignia");
        Engine.Join(map);

        int armySize = GetBossParticipantCount();
        Ultra.WaitForArmy(armySize - 1, "ultra_engineer.sync");

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
                    armyWipeDetected = false;
                    C.Logger("Army wipe recovered — resuming fight.");
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

            if (Ultra.CheckArmyProgressBool(() => Bot.TempInv.Contains("Ultra Engineer Defeated", 1), syncPath))
            {
                C.Logger("All players finished farm.");
                Engine.Join(map);
                Ultra.JoinHouse();
                C.EnsureComplete(8154);
                Bot.Sleep(30000);
                break;
            }

            Ultra.KillWithPriority(boss, 3, priority1, 2, priority2, 1);
            Pots.ActivateEquippedPotion();
            Bot.Sleep(100);
        }
    }
}
