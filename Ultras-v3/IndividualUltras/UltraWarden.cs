/*
name: UltraWarden_v3
description: Ultra Warden helper for Ultras-v3. Uses synced class equip and solo/sync fight flow.
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

    private static readonly string[] UltraClasses = new[]
    {
        "Verus DoomKnight",
        "King's Echo",
        "StoneCrusher",
        "Lord Of Order"
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

    private const string BossParticipantSyncFile = "ultras_v3_participants.sync";
    private const int FixedArmySize = 4;

    private int GetBossParticipantCount() => FixedArmySize;

    private void EquipPresetClasses()
    {
        int armySize = GetBossParticipantCount();
        bool allowDuplicates = armySize > UltraClasses.Length;

        C.Logger($"[UltraWarden-v3] Equipping hardcoded ultra classes for army size {armySize}.");
        string[][] classSlots = new string[armySize][];

        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClasses.Length
                ? new[] { UltraClasses[i] }
                : UltraClasses;
        }

        Ultra.EquipClassSync(classSlots, armySize, "UltraWarden-v3.class_assign.sync", allowDuplicates);
    }

    private bool IsTaunter()
    {
        string? currentClass = Bot.Player.CurrentClass?.Name;
        return string.Equals(currentClass, "Verus DoomKnight", StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentClass, "Lord Of Order", StringComparison.OrdinalIgnoreCase);
    }

    private void Prep()
    {
        EquipPresetClasses();

        bool skipThird = IsTaunter();
        Enh.Apply();
        Pots.EnsureRecommendedPotions(skipThird: skipThird);
        Pots.UseRecommendedPotions(skipThird: skipThird, ensureStock: false);

        if (skipThird)
        {
            C.Logger("[UltraWarden-v3] Taunter detected, acquiring Scroll of Enrage after potion setup.");
            Ultra.GetScrollOfEnrage();
        }

        EquipPresetClasses();

        string? className = Bot.Player.CurrentClass?.Name;
        if (string.Equals(className, "Verus DoomKnight", StringComparison.OrdinalIgnoreCase))
            tauntOffsetSeconds = 0;
        else if (string.Equals(className, "Lord Of Order", StringComparison.OrdinalIgnoreCase))
            tauntOffsetSeconds = 5;
        else
            tauntOffsetSeconds = 0;

        C.Logger($"[UltraWarden-v3] Taunt offset: {tauntOffsetSeconds}s for class: {className}");
    }

    private void Fight()
    {
        const string map = "ultrawarden";
        const string boss = "Ultra Warden";
        const string waitSyncFile = "ultra_warden.sync";
        const string wipeDeadSyncFile = "UltraWardenWipeDead.sync";
        const string wipeAliveSyncFile = "UltraWardenWipeAlive.sync";

        if (!UltraGeneral.IsQuestComplete(Bot, 8153))
            UltraGeneral.EnsureAcceptOnce(Bot, 8153);

        C.AddDrop("Warden Insignia");
        Ultra.ClearSyncFile(waitSyncFile);
        Ultra.ClearSyncFile(wipeDeadSyncFile);
        Ultra.ClearSyncFile(wipeAliveSyncFile);

        Engine.Join(map);
        Bot.Sleep(2500);

        int armySize = GetBossParticipantCount();
        Ultra.WaitForArmy(armySize - 1, waitSyncFile);

        Engine.ChooseBestCell(boss);
        Bot.Player.SetSpawnPoint();
        Engine.EnableSkills();

        fightStartTime = DateTime.UtcNow;

        bool armyWipeDetected = false;

        while (!Bot.ShouldExit)
        {
            if (!Bot.Player.Alive)
            {
                Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                lastTauntTime = DateTime.MinValue;
                continue;
            }

            bool allDead = UltraGeneral.IsWholeArmyDead(Ultra, Bot, wipeDeadSyncFile);
            if (allDead)
            {
                if (!armyWipeDetected)
                    C.Logger("Army wipe detected — all clients dead.");
                armyWipeDetected = true;
            }

            if (armyWipeDetected)
            {
                bool allAlive = UltraGeneral.IsWholeArmyAlive(Ultra, Bot, wipeAliveSyncFile);
                if (allAlive)
                {
                    C.Logger("Army wipe recovered — all clients alive again.");
                    Ultra.ClearSyncFile(wipeDeadSyncFile);
                    Ultra.ClearSyncFile(wipeAliveSyncFile);
                    Bot.Combat.CancelTarget();
                    armyWipeDetected = false;
                    fightStartTime = DateTime.UtcNow;
                    lastTauntTime = DateTime.MinValue;
                    continue;
                }

                Bot.Combat.CancelTarget();
                C.Logger("Army wipe active — waiting for everyone to respawn before fighting.");
                Bot.Sleep(250);
                continue;
            }

            if (Ultra.CheckArmyProgressBool(() => Bot.TempInv.Contains("Ultra Warden Defeated", 1), waitSyncFile))
            {
                C.Logger("Ultra Warden defeated. Finishing quest.");
                Engine.Join(map);
                Ultra.JoinHouse();
                C.EnsureComplete(8153);
                Bot.Sleep(30000);
                break;
            }

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
                    Engine.Cast(5);
                }
            }

            Bot.Combat.Attack(boss);
            Pots.ActivateEquippedPotion();
            Bot.Sleep(100);
        }
    }
}
