/*
name: UltraDarkon_v3
description: Ultra Darkon v3 — turn-based dual taunter with sync file coordination and army sync.
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
using System.Threading.Tasks;
using Skua.Core.Interfaces;

public class UltraDarkon_v3
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
    private const string Dps1 = "StoneCrusher";
    private const string Dps2 = "King's Echo";

    private static readonly string[][] UltraClassesByRole =
    {
        new[] { Taunter1 },
        new[] { Taunter2 },
        new[] { Dps1 },
        new[] { Dps2 }
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

    private void EquipPresetClasses()
    {
        int armySize = 4;
        bool allowDuplicates = armySize > UltraClassesByRole.Length;

        C.Logger($"[UltraDarkon-v3] Equipping role-based ultra classes for army size {armySize}.");
        string[][] classSlots = new string[armySize][];

        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClassesByRole.Length ? UltraClassesByRole[i] : UltraClassesByRole[0];
        }

        Ultra.EquipClassSync(classSlots, armySize, "ultra_darkon_class-v3.sync", allowDuplicates);
    }

    private void Prep()
    {
        UltraGeneral.EquipWarriorClass();
        EquipPresetClasses();
        Bot.Sleep(3000);

        bool skipThird = IsTaunter();
        Enh.ApplyDarkon();
        Pots.EnsureRecommendedPotions(skipThird: skipThird);
        Pots.UseRecommendedPotions(skipThird: skipThird, ensureStock: false);

        if (skipThird)
        {
            C.Logger("[UltraDarkon-v3] Taunter detected, acquiring Scroll of Enrage after potion setup.");
            Ultra.GetScrollOfEnrage();
        }

        string? className = Bot.Player.CurrentClass?.Name;
        C.Logger($"[UltraDarkon-v3] Role: {className}");
    }

    private void Fight()
    {
        const string map = "ultradarkon";
        const string boss = "Darkon the Conductor";
        const string drop = "Darkon Insignia";
        const string BossDefeated = "Darkon the Conductor Defeated";

        const string waitSyncFile = "Ultra_Darkon.sync";
        const string wipeDeadSyncFile = "UltraDarkonWipeDead.sync";
        const string wipeAliveSyncFile = "UltraDarkonWipeAlive.sync";
        const string tauntSyncFile = "UltraDarkonTauntTurn.sync";

        const int questId = 8746;
        
        // Prerequisite quest check
        if (!C.isCompletedBefore(8733))
            Bot.Quests.UpdateQuest(8733);

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

        string tauntSyncPath = Ultra.ResolveSyncPath(tauntSyncFile);
        // Initialize: T1 goes first, fires immediately (ticks = 0)
        File.WriteAllText(tauntSyncPath, "1:0");
        C.Logger($"[UltraDarkon-v3] Taunt sync initialized — T1 goes first.");

        fightStartTime = DateTime.UtcNow;
        bool armyWipeDetected = false;
        bool bossWasAlive = false;

        // Launch the appropriate taunter loop
        string? className = Bot.Player.CurrentClass?.Name;
        if (className == Taunter1)
            _ = RunTaunter1LoopAsync(tauntSyncPath);
        else if (className == Taunter2)
            _ = RunTaunter2LoopAsync(tauntSyncPath);

        while (!Bot.ShouldExit)
        {
            if (!Bot.Player.Alive)
            {
                Bot.Wait.ForTrue(() => Bot.Player.Alive, 20);
                continue;
            }

            if (UltraGeneral.ArmyWipeHelperWithTaunters(Ultra, Bot, wipeDeadSyncFile, wipeAliveSyncFile, ref armyWipeDetected, ref fightStartTime, ref lastTauntTime))
            {
                C.Logger("[UltraDarkon-v3] Wipe recovery — resetting taunt turn to T1.");
                File.WriteAllText(tauntSyncPath, "1:0");
                continue;
            }

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
                C.Logger("Darkon the Conductor defeated. Finishing quest.");
                Engine.Join(map);
                Ultra.JoinHouse();
                UltraGeneral.CompleteQuest(Bot, questId);
                Bot.Sleep(30000);
                break;
            }

            Bot.Combat.Attack(boss);
            Pots.ActivateEquippedPotion();

            Bot.Sleep(500);
        }
    }

    private async Task RunTaunter1LoopAsync(string tauntSyncPath)
    {
        C.Logger("[T1] Taunter loop started. Staggering 1s...");
        try { await Task.Delay(1000); } catch { }

        int pollCount = 0;
        while (!Bot.ShouldExit)
        {
            try
            {
                // Wait until it's our turn
                while (!Bot.ShouldExit)
                {
                    if (!Bot.Player.Alive)
                    {
                        await Task.Delay(200);
                        continue;
                    }

                    pollCount++;
                    if (pollCount % 200 == 0)
                        C.Logger($"[T1] Still waiting... (pollCount={pollCount})");

                    string[] lines = Ultra.ReadLines(tauntSyncPath);
                    if (lines.Length > 0)
                    {
                        string[] parts = lines[0].Split(':');
                        if (parts.Length >= 2 && parts[0] == "1"
                            && long.TryParse(parts[1], out long targetTicks))
                        {
                            if (targetTicks == 0)
                            {
                                C.Logger("[T1] Turn signal '1:0' — immediate, breaking.");
                                break;
                            }
                            double targetSecs = (new DateTime(targetTicks, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds;
                            if (targetSecs <= 0)
                                break;
                            if (pollCount % 50 == 0)
                                C.Logger($"[T1] Our turn, waiting {targetSecs:F2}s until target time.");
                        }
                    }
                    await Task.Delay(100);
                }

                if (Bot.ShouldExit) return;
                if (!Bot.Player.Alive)
                {
                    C.Logger("[T1] Not alive after turn signal — skipping taunt.");
                    continue;
                }

                C.Logger("[T1] Turn confirmed — taunting now.");
                var t1Start = DateTime.UtcNow;
                await DarkonTauntAsync();

                // Hand off to T2 — set target time 5s from taunt START, not from now
                if (!Bot.ShouldExit)
                {
                    long handoffTicks = t1Start.AddSeconds(5).Ticks;
                    File.WriteAllText(tauntSyncPath, $"2:{handoffTicks}");
                    double actualGap = (new DateTime(handoffTicks, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds;
                    C.Logger($"[T1] Handoff to T2 — target in {actualGap:F2}s (5s from taunt start).");
                }
            }
            catch (Exception ex)
            {
                C.Logger($"[T1] Error in loop: {ex.Message}");
                await Task.Delay(1000);
            }
        }
        C.Logger("[T1] Loop exited.");
    }

    private async Task RunTaunter2LoopAsync(string tauntSyncPath)
    {
        C.Logger("[T2] Taunter loop started.");

        int pollCount = 0;
        while (!Bot.ShouldExit)
        {
            try
            {
                // Wait until it's our turn
                while (!Bot.ShouldExit)
                {
                    if (!Bot.Player.Alive)
                    {
                        await Task.Delay(200);
                        continue;
                    }

                    pollCount++;
                    if (pollCount % 200 == 0)
                        C.Logger($"[T2] Still waiting... (pollCount={pollCount})");

                    string[] lines = Ultra.ReadLines(tauntSyncPath);
                    if (lines.Length > 0)
                    {
                        string[] parts = lines[0].Split(':');
                        if (parts.Length >= 2 && parts[0] == "2"
                            && long.TryParse(parts[1], out long targetTicks))
                        {
                            if (targetTicks == 0)
                            {
                                C.Logger("[T2] Turn signal '2:0' — immediate, breaking.");
                                break;
                            }
                            double targetSecs = (new DateTime(targetTicks, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds;
                            if (targetSecs <= 0)
                                break;
                            if (pollCount % 50 == 0)
                                C.Logger($"[T2] Our turn, waiting {targetSecs:F2}s until target time.");
                        }
                    }
                    await Task.Delay(100);
                }

                if (Bot.ShouldExit) return;
                if (!Bot.Player.Alive)
                {
                    C.Logger("[T2] Not alive after turn signal — skipping taunt.");
                    continue;
                }

                C.Logger("[T2] Turn confirmed — taunting now.");
                var t2Start = DateTime.UtcNow;
                await DarkonTauntAsync();

                // Hand off to T1 — set target time 5s from taunt START, not from now
                if (!Bot.ShouldExit)
                {
                    long handoffTicks = t2Start.AddSeconds(5).Ticks;
                    File.WriteAllText(tauntSyncPath, $"1:{handoffTicks}");
                    double actualGap = (new DateTime(handoffTicks, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds;
                    C.Logger($"[T2] Handoff to T1 — target in {actualGap:F2}s (5s from taunt start).");
                }
            }
            catch (Exception ex)
            {
                C.Logger($"[T2] Error in loop: {ex.Message}");
                await Task.Delay(1000);
            }
        }
        C.Logger("[T2] Loop exited.");
    }

    private async Task DarkonTauntAsync()
    {
        var start = DateTime.UtcNow;
        C.Logger("[Taunt] Starting 50 presses...");
        int pressed = 0;
        for (int i = 0; i < 50 && !Bot.ShouldExit; i++)
        {
            if (!Bot.Player.Alive)
            {
                C.Logger("[Taunt] Died during taunt — aborting.");
                return;
            }
            Bot.Skills.UseSkill(5);
            pressed++;
            await Task.Delay(20);
        }
        double took = (DateTime.UtcNow - start).TotalSeconds;
        C.Logger($"[Taunt] Done — {pressed} presses in {took:F2}s.");
    }
}
