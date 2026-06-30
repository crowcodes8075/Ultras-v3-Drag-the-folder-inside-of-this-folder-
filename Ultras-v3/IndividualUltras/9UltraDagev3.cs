/*
name: UltraDage_v3
description: Ultra Dage v3 — pulse-driven dual taunter with zone movement handler and army sync.
*/
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreEngine.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreUltra.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/UltraEnhancements.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/UltraPotions.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/UltraGeneral.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/UltraAsync.cs
//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreBots2.cs
//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreAdvanced2.cs

using System;
using System.Linq;
using System.Threading.Tasks;
using Skua.Core.Interfaces;

public class UltraDage_v3
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
    private const string Taunter2 = "ArchPaladin";
    private const string Decayer = "Lord Of Order";
    // DPS classes
    private const string Dps1 = "King's Echo";

    private static readonly string[][] UltraClassesByRole =
    {
        new[] { Taunter1 },
        new[] { Taunter2 },
        new[] { Decayer },
        new[] { Dps1 }
    };

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
            Bot.Events.ExtensionPacketReceived += UltraDageListener;
            try
            {
                Prep();
                Fight();
            }
            finally
            {
                Bot.Events.ExtensionPacketReceived -= UltraDageListener;
            }
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

    private bool IsDecayer()
    {
        string? className = Bot.Player.CurrentClass?.Name;
        return className == Decayer;
    }

    private void EquipPresetClasses()
    {
        int armySize = 4;
        bool allowDuplicates = armySize > UltraClassesByRole.Length;

        C.Logger($"[UltraDage-v3] Equipping role-based ultra classes for army size {armySize}.");
        string[][] classSlots = new string[armySize][];

        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClassesByRole.Length ? UltraClassesByRole[i] : UltraClassesByRole[0];
        }

        Ultra.EquipClassSync(classSlots, armySize, "ultra_dage_class-v3.sync", allowDuplicates);
    }

    private void Prep()
    {
        if (!C.isCompletedBefore(793))

        Bot.Quests.UpdateQuest(793);

        UltraGeneral.EquipWarriorClass();
        EquipPresetClasses();
        Bot.Sleep(3000);

        bool skipThird = IsTaunter();
        Enh.ApplyDage();
        Pots.EnsureRecommendedPotions(skipThird: skipThird, context: "Dage");
        Pots.UseRecommendedPotions(skipThird: skipThird, ensureStock: false, context: "Dage");

        if (skipThird)
        {
            C.Logger("[UltraDage-v3] Taunter detected, acquiring Scroll of Enrage after potion setup.");
            Ultra.GetScrollOfEnrage();
        }

        if (IsDecayer())
            Ultra.GetScrollOfDecay();

        string? className = Bot.Player.CurrentClass?.Name;
        C.Logger($"[UltraDage-v3] Role: {className}");
    }

    private void Fight()
    {
        const string map = "ultradage";
        const string boss = "Dage the Dark Lord";
        const string bossDefeatedTemp = "Dage the Dark Lord Defeated";

        const string waitSyncFile = "ultra_dage.sync";
        const string wipeDeadSyncFile = "UltraDageWipeDead.sync";
        const string wipeAliveSyncFile = "UltraDageWipeAlive.sync";
        const string fightTimeSyncFile = "UltraDageFightTime.sync";
        const string completionSyncFile = "UltraDageCompletion.sync";
        int armySize = 4;

        const int questId = 8547;
        
        if (!UltraGeneral.IsQuestGreen(Bot, questId))
            UltraGeneral.EnsureAcceptOnce(Bot, questId);

        Ultra.ClearSyncFile(waitSyncFile);
        Ultra.ClearSyncFile(wipeDeadSyncFile);
        Ultra.ClearSyncFile(wipeAliveSyncFile);
        Ultra.ClearSyncFile(Ultra.ResolveSyncPath(fightTimeSyncFile));
        Ultra.ClearSyncFile(Ultra.ResolveSyncPath(completionSyncFile));

        Engine.Join(map);
        Ultra.WaitForArmy(armySize - 1, waitSyncFile);

        Engine.ChooseBestCell(boss);
        Bot.Player.SetSpawnPoint();
        Bot.Sleep(2000);
        Engine.EnableSkills();

        // Pre-seed completion sync file so all 4 entries exist before the loop starts.
        string? _username = Bot.Player.Username;
        string? _className = Bot.Player.CurrentClass?.Name;
        if (!string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_className))
        {
            string _myKey = $"{_username}|{_className}".Replace(":", "-");
            Ultra.UpdateEntry(Ultra.ResolveSyncPath(completionSyncFile), _myKey, "0");
        }

        string fightTimeSyncPath = Ultra.ResolveSyncPath(fightTimeSyncFile);
        bool armyWipeDetected = false;
        bool bossWasAlive = false;

        // Set or retrieve fight start time, then launch the appropriate taunter loop
        string? className = Bot.Player.CurrentClass?.Name;
        if (className == Taunter1)
        {
            C.Logger("[UltraDage-v3] Taunter1 (Primary) — setting fight start time.");
            fightStartTime = UltraAsync.SetFightTime(C, fightTimeSyncPath);
            UltraAsync.StartTauntLoop(Bot, C, Engine, fightStartTime, 0);
        }
        else if (className == Taunter2)
        {
            C.Logger("[UltraDage-v3] Taunter2 (Secondary) — reading fight start time.");
            fightStartTime = UltraAsync.GetFightTime(Ultra, C, fightTimeSyncPath);
            UltraAsync.StartTauntLoop(Bot, C, Engine, fightStartTime, 1);
        }

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
                if (Bot.TempInv.Contains(bossDefeatedTemp, 1))
                    return true;
                var m = Bot.Monsters.CurrentAvailableMonsters.FirstOrDefault(x => x?.Name == boss);
                return m == null || !m.Alive || m.HP <= 0;
            }, completionSyncFile))
            {
                C.Logger("Dage the Dark Lord defeated. Finishing quest.");
                Engine.Join(map);
                Ultra.JoinHouse();
                UltraGeneral.CompleteQuest(Bot, questId);
                Bot.Sleep(30000);
                break;
            }

            Bot.Combat.Attack(boss);
            Pots.ActivateEquippedPotion();

            // Decayer logic — Verus DoomKnight spams skill 5 when Legionnaire aura is active
            if (IsDecayer() && Engine.HasAura("Legionnaire") && Engine.Left("Legionnaire", 12))
                _ = DecayAsync(boss);

            Bot.Sleep(500);
        }
    }

    public async void UltraDageListener(dynamic packet)
    {
        if (packet?["params"]?.type?.ToString() != "json")
            return;

        if (!Bot.Player.Alive)
            return;

        dynamic data = packet["params"].dataObj;

        if (data?.cmd?.ToString() != "event")
            return;

        string? zoneSet = data?.args?.zoneSet?.ToString();

        if (!string.IsNullOrEmpty(zoneSet))
        {
            int targetX =
                zoneSet.Equals("A", StringComparison.OrdinalIgnoreCase)
                    ? 122
                    : zoneSet.Equals("B", StringComparison.OrdinalIgnoreCase)
                        ? 856
                        : 0;

            if (targetX != 0)
            {
                _ = Task.Run(() =>
                {
                    Bot.Player.WalkTo(targetX, 420);

                    Bot.Wait.ForTrue(
                        () => Math.Abs(Bot.Player.X - targetX) < 40,
                        10
                    );

                    Bot.Sleep(2000);
                });

                return;
            }
        }

        if (string.IsNullOrEmpty(zoneSet))
        {
            _ = Task.Run(() =>
            {
                Bot.Sleep(5000);

                int middleX = 500;

                Bot.Player.WalkTo(middleX, 420);

                Bot.Wait.ForTrue(
                    () => Math.Abs(Bot.Player.X - middleX) < 40,
                    10
                );
            });

            return;
        }
    }

    private async Task DecayAsync(string bossName)
    {
        for (int i = 0; i < 60 && !Bot.ShouldExit; i++)
        {
            if (!Bot.Player.Alive)
                break;

            if (!Bot.Player.HasTarget)
                Bot.Combat.Attack(bossName);

            Engine.Cast(5);
            await Task.Delay(50);
        }
    }
}
