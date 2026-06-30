/*
name: UltraDrago_v3
description: Ultra King Drago v3 — summon targeting with timer-based taunt rotation and army sync.
*/
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreEngine2.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreUltra2.cs
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

public class UltraDrago_v3
{
    private static IScriptInterface Bot => IScriptInterface.Instance;
    private static CoreBots2 C => CoreBots2.Instance;
    private static CoreEngine2 Engine => _Engine ??= new CoreEngine2();
    private static CoreEngine2 _Engine;
    private static CoreUltra2 Ultra => _Ultra ??= new CoreUltra2();
    private static CoreUltra2 _Ultra;
    private static UltraEnhancements Enh => _Enh ??= new UltraEnhancements();
    private static UltraEnhancements _Enh;
    private static UltraPotions Pots => _Pots ??= new UltraPotions();
    private static UltraPotions _Pots;

    private const string Taunter1 = "Lord Of Order";
    private const string Taunter2 = "Verus DoomKnight";
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

        C.Logger($"[UltraDrago-v3] Equipping role-based ultra classes for army size {armySize}.");
        string[][] classSlots = new string[armySize][];

        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClassesByRole.Length ? UltraClassesByRole[i] : UltraClassesByRole[0];
        }

        Ultra.EquipClassSync(classSlots, armySize, "ultra_drago_class-v3.sync", allowDuplicates);
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
            C.Logger("[UltraDrago-v3] Taunter detected, acquiring Scroll of Enrage after potion setup.");
            Ultra.GetScrollOfEnrage();
        }

        string? className = Bot.Player.CurrentClass?.Name;
        C.Logger($"[UltraDrago-v3] Class: {className}");
    }

    private void Fight()
    {
        const string map = "ultradrago";
        const string boss = "King Drago";
        const string bossDefeatedTemp = "Drago Dethroned";
        const string leftSummon = "Executioner Dene";
        const string rightSummon = "Bowmaster Algie";

        const string waitSyncFile = "ultra_drago.sync";
        const string wipeDeadSyncFile = "UltraDragoWipeDead.sync";
        const string wipeAliveSyncFile = "UltraDragoWipeAlive.sync";
        const string fightTimeSyncFile = "UltraDragoFightTime.sync";
        const string completionSyncFile = "UltraDragoCompletion.sync";

        const int questId = 8397;
        

        if (!Bot.Quests.IsUnlocked(questId))
            Bot.Quests.UpdateQuest(8395);

        if (!UltraGeneral.IsQuestGreen(Bot, questId))
            UltraGeneral.EnsureAcceptOnce(Bot, questId);

        Ultra.ClearSyncFile(waitSyncFile);
        Ultra.ClearSyncFile(wipeDeadSyncFile);
        Ultra.ClearSyncFile(wipeAliveSyncFile);
        Ultra.ClearSyncFile(Ultra.ResolveSyncPath(fightTimeSyncFile));
        Ultra.ClearSyncFile(Ultra.ResolveSyncPath(completionSyncFile));

        Engine.Join(map);

        int armySize = 4;
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
            C.Logger("[UltraDrago-v3] Taunter1 (Primary) — setting fight start time.");
            fightStartTime = UltraAsync.SetFightTime(C, fightTimeSyncPath);
            UltraAsync.StartTauntLoop(Bot, C, Engine, fightStartTime, 0);
        }
        else if (className == Taunter2)
        {
            C.Logger("[UltraDrago-v3] Taunter2 (Secondary) — reading fight start time.");
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
                C.Logger("King Drago defeated. Finishing quest.");
                Engine.Join(map);
                Ultra.JoinHouse();
                UltraGeneral.CompleteQuest(Bot, questId);
                Bot.Sleep(30000);
                break;
            }

            if (IsTaunter())
            {
                if (Ultra.MonsterAlive(leftSummon))
                {
                    if (Bot.Player.Target?.Name != leftSummon)
                        Bot.Combat.Attack(leftSummon);
                }
                else if (Ultra.MonsterAlive(rightSummon))
                {
                    if (Bot.Player.Target?.Name != rightSummon)
                        Bot.Combat.Attack(rightSummon);
                }
                else
                {
                    if (Bot.Player.Target?.Name != boss)
                        Bot.Combat.Attack(boss);
                }
            }
            else
            {
                if (Ultra.MonsterAlive(rightSummon))
                {
                    if (Bot.Player.Target?.Name != rightSummon)
                        Bot.Combat.Attack(rightSummon);
                }
                else if (Ultra.MonsterAlive(leftSummon))
                {
                    if (Bot.Player.Target?.Name != leftSummon)
                        Bot.Combat.Attack(leftSummon);
                }
                else
                {
                    if (Bot.Player.Target?.Name != boss)
                        Bot.Combat.Attack(boss);
                }
            }
            Pots.ActivateEquippedPotion();

            Bot.Sleep(500);
        }
    }

}
