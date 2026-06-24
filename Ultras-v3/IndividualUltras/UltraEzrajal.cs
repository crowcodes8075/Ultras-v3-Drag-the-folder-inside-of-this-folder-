/*
name: UltraEzrajal_v3
description: Ultra Ezrajal for Ultras-v3.
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

    private static readonly string[] UltraClasses = new[]
    {
        "Verus DoomKnight",
        "King's Echo",
        "StoneCrusher",
        "Lord Of Order"
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
            Bot.UltraBossHelper.DisableCounterAttack();
        }
    }

    private const string BossParticipantSyncFile = "ultras_v3_participants.sync";
    private const int FixedArmySize = 4;

    private int GetBossParticipantCount() => FixedArmySize;

    private void EquipPresetClasses()
    {
        int armySize = GetBossParticipantCount();
        bool allowDuplicates = armySize > UltraClasses.Length;

        C.Logger($"[UltraEzrajal-v3] Equipping hardcoded ultra classes for army size {armySize}.");

        string[][] classSlots = new string[armySize][];
        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClasses.Length
                ? new[] { UltraClasses[i] }
                : UltraClasses;
        }

        Ultra.EquipClassSync(classSlots, armySize, "UltraEzrajal-v3.class_assign.sync", allowDuplicates);
    }

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
        const string map = "ultraezrajal";
        const string boss = "Ultra Ezrajal";
        const string waitSyncFile = "ultra_ezrajal.sync";

        if (!UltraGeneral.IsQuestComplete(Bot, 8152))
            UltraGeneral.EnsureAcceptOnce(Bot, 8152);

        C.AddDrop("Ezrajal Insignia");

        Ultra.ClearSyncFile(waitSyncFile);
        Engine.Join(map);
        Bot.Sleep(2500);

        int armySize = GetBossParticipantCount();
        Ultra.WaitForArmy(armySize - 1, waitSyncFile);

        Engine.ChooseBestCell(boss);
        Bot.Player.SetSpawnPoint();
        Engine.EnableSkills();

        while (!Bot.ShouldExit)
        {
            if (!Bot.Player.Alive)
            {
                Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                continue;
            }

            if (Bot.TempInv.Contains("Ultra Ezrajal Defeated", 1))
            {
                C.Logger("Ultra Ezrajal defeated. Finishing quest.");
                Bot.UltraBossHelper.DisableCounterAttack();
                Engine.Join(map);
                Ultra.JoinHouse();
                C.EnsureComplete(8152);
                Bot.Sleep(30000);
                break;
            }

            if (
                Bot.Player.HasTarget
                && Bot.Target?.Auras?.Any(a => a != null && a.Name == "Counter Attack") == true
            )
            {
                Bot.Combat.CancelAutoAttack();
                Bot.Sleep(6300);
            }
            else
            {
                Bot.Combat.Attack(boss);
            }

            Pots.ActivateEquippedPotion();
            Bot.Sleep(100);
        }
    }
}
