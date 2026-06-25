/*
name: UltraEngineer_v3
description: Ultra Engineer v3 — prioritizes drones with KillWithPriority and army sync. Uses synced class equip and no-taunt fight flow.
*/
//cs_include Scripts/Ultras-v3/UltraDependencies/CoreEngine.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/CoreUltra.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/UltraEnhancements.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/UltraPotions.cs
//cs_include Scripts/Ultras-v3/UltraDependencies/UltraGeneral.cs
//cs_include Scripts/Ultras-v3/CoreDependencies/CoreBots2.cs
//cs_include Scripts/Ultras-v3/CoreDependencies/CoreAdvanced2.cs

using System;
using Skua.Core.Interfaces;

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

    private const string Dps1 = "Verus DoomKnight";
    private const string Dps2 = "StoneCrusher";
    private const string Dps3 = "Lord Of Order";
    private const string Dps4 = "King's Echo";

    private static readonly string[][] UltraClassesByRole =
    {
        new[] { Dps1 },
        new[] { Dps2 },
        new[] { Dps3 },
        new[] { Dps4 }
    };

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

    private void EquipPresetClasses()
    {
        int armySize = 4;
        bool allowDuplicates = armySize > UltraClassesByRole.Length;

        C.Logger($"[UltraEngineer-v3] Equipping role-based ultra classes for army size {armySize}.");
        string[][] classSlots = new string[armySize][];

        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClassesByRole.Length
                ? UltraClassesByRole[i]
                : UltraClassesByRole[0];
        }

        Ultra.EquipClassSync(classSlots, armySize, "ultra_engineer_class-v3.sync", allowDuplicates);
    }

    private void Prep()
    {
        EquipPresetClasses();

        Enh.Apply();
        Pots.EnsureRecommendedPotions(skipThird: false);
        Pots.UseRecommendedPotions(skipThird: false, ensureStock: false);

        EquipPresetClasses();
    }

    private void Fight()
    {
        const string map = "ultraengineer";
        const string boss = "Ultra Engineer";
        const string drop = "Engineer Insignia";
        const string BossDefeated = "Ultra Engineer Defeated";
        const string priority1 = "Defense Drone";
        const string priority2 = "Attack Drone";

        const string waitSyncFile = "ultra_engineer.sync";
        const string wipeDeadSyncFile = "UltraEngineerWipeDead.sync";
        const string wipeAliveSyncFile = "UltraEngineerWipeAlive.sync";
        
        const int questId = 8154;
        
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

        bool armyWipeDetected = false;
        bool bossWasAlive = false;

        while (!Bot.ShouldExit)
        {
            if (!Bot.Player.Alive)
            {
                Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                continue;
            }

            if (UltraGeneral.ArmyWipeHelperWithNoTaunters(Ultra, Bot, wipeDeadSyncFile, wipeAliveSyncFile, ref armyWipeDetected))
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
                C.Logger("Ultra Engineer defeated. Finishing quest.");
                Engine.Join(map);
                Ultra.JoinHouse();
                UltraGeneral.CompleteQuest(Bot, questId);
                Bot.Sleep(30000);
                break;
            }

            Ultra.KillWithPriority(boss, 3, priority1, 2, priority2, 1);
            Pots.ActivateEquippedPotion();
            Bot.Sleep(100);
        }
    }
}
