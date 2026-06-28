/*
name: UltraEzrajal_v3
description: Ultra Ezrajal v3 — handles Counter Attack windows with army sync. Uses synced class equip and no-taunt fight flow.
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

public class UltraEzrajal_v3
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
        Bot.UltraBossHelper.EnableCounterAttack();

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

        C.Logger($"[UltraEzrajal-v3] Equipping role-based ultra classes for army size {armySize}.");
        string[][] classSlots = new string[armySize][];

        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClassesByRole.Length
                ? UltraClassesByRole[i]
                : UltraClassesByRole[0];
        }

        Ultra.EquipClassSync(classSlots, armySize, "ultra_ezrajal_class-v3.sync", allowDuplicates);
    }

    private void Prep()
    {
        UltraGeneral.EquipWarriorClass();
        EquipPresetClasses();
        Bot.Sleep(3000);

        Enh.Apply();
        Pots.EnsureRecommendedPotions(skipThird: false);
        Pots.UseRecommendedPotions(skipThird: false, ensureStock: false);
    }

    private void Fight()
    {
        const string map = "ultraezrajal";
        const string boss = "Ultra Ezrajal";
        const string drop = "Ezrajal Insignia";
        const string BossDefeated = "Ultra Ezrajal Defeated";

        const string waitSyncFile = "ultra_ezrajal.sync";
        const string wipeDeadSyncFile = "UltraEzrajalWipeDead.sync";
        const string wipeAliveSyncFile = "UltraEzrajalWipeAlive.sync";
        
        const int questId = 8152;
        

        if (!UltraGeneral.IsQuestGreen(Bot, questId))
            UltraGeneral.EnsureAcceptOnce(Bot, questId);

        C.AddDrop(drop);
        Ultra.ClearSyncFile(waitSyncFile);
        Ultra.ClearSyncFile(wipeDeadSyncFile);
        Ultra.ClearSyncFile(wipeAliveSyncFile);

        Engine.Join(map);
        

        int armySize = 4;
        Ultra.WaitForArmy(armySize - 1, waitSyncFile);

        Engine.ChooseBestCell(boss);
        Bot.Sleep(2500);
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
                if (m != null && m.Alive)
                    bossWasAlive = true;
            }

            if (bossWasAlive && Ultra.CheckArmyProgressBool(() =>
            {
                var m = Bot.Monsters.CurrentAvailableMonsters.FirstOrDefault(x => x?.Name == boss);
                return m == null || !m.Alive;
            }, waitSyncFile))
            {
                C.Logger("Ultra Ezrajal defeated. Finishing quest.");
                Engine.Join(map);
                Bot.UltraBossHelper.DisableCounterAttack();
                Ultra.JoinHouse();
                UltraGeneral.CompleteQuest(Bot, questId);
                Bot.Sleep(30000);
                break;
            }

            // Counter Attack handler — stop attacking when Ezrajal reflects
            if (Bot.Player.HasTarget
                && Bot.Target?.Auras?.Any(a => a != null && a.Name == "Counter Attack") == true)
            {
                Bot.Combat.CancelAutoAttack();
                Bot.Sleep(6300);
            }
            else
            {
                Bot.Combat.Attack(boss);
            }

            Pots.ActivateEquippedPotion();
            Bot.Sleep(500);
        }
    }
}
