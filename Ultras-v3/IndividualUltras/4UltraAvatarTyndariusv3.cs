/*
name: UltraAvatarTyndarius_v3
description: Ultra Avatar Tyndarius v3 — TynTaunter1 + TynTaunter2AttackBall2 + Ball2Attacker1 + Ball2Attacker2 with pulse-driven targeting.
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
using Skua.Core.Interfaces;

public class UltraAvatarTyndarius_v3
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

    private const string TynTaunter1 = "ArchPaladin";
    private const string TynTaunter2AttackBall2 = "Lord Of Order";
    private const string Ball2Attacker1 = "Dragon of Time";
    private const string Ball2Attacker2 = "StoneCrusher";

    private static readonly string[][] UltraClassesByRole =
    {
        new[] { TynTaunter1 },
        new[] { TynTaunter2AttackBall2 },
        new[] { Ball2Attacker1 },
        new[] { Ball2Attacker2 }
    };

    private DateTime fightStartTime = DateTime.MinValue;
    private DateTime lastTauntTime = DateTime.MinValue;
    private string _role = "";

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

        C.Logger($"[UltraAvatarTyndarius-v3] Equipping role-based ultra classes for army size {armySize}.");
        string[][] classSlots = new string[armySize][];

        for (int i = 0; i < armySize; i++)
        {
            classSlots[i] = i < UltraClassesByRole.Length ? UltraClassesByRole[i] : UltraClassesByRole[0];
        }

        Ultra.EquipClassSync(classSlots, armySize, "ultra_tyndarius_class-v3.sync", allowDuplicates);
    }

    private bool IsTaunter() => !_role.StartsWith("Ball2Attacker");

    private void Prep()
    {
        UltraGeneral.EquipWarriorClass();
        EquipPresetClasses();
        Bot.Sleep(3000);

        string? className = Bot.Player.CurrentClass?.Name;
        if (className == TynTaunter1) _role = "TynTaunter1";
        else if (className == TynTaunter2AttackBall2) _role = "TynTaunter2AttackBall2";
        else if (className == Ball2Attacker1) _role = "Ball2Attacker1";
        else _role = "Ball2Attacker2";

        bool skipThird = IsTaunter();
        Enh.ApplyTyndarius();
        Pots.EnsureRecommendedPotions(skipThird: skipThird);
        Pots.UseRecommendedPotions(skipThird: skipThird, ensureStock: false);

        if (skipThird)
        {
            C.Logger("[UltraAvatarTyndarius-v3] Taunter detected, acquiring Scroll of Enrage after potion setup.");
            Ultra.GetScrollOfEnrage();
        }

        C.Logger($"[UltraAvatarTyndarius-v3] Role: {_role} ({className})");
    }

    private void Fight()
    {
        const string map = "ultratyndarius";
        const string boss = "Ultra Avatar Tyndarius";
        const string bossDefeatedTemp = "Ultra Avatar Tyndarius Defeated";

        const string waitSyncFile = "ultra_tyndarius.sync";
        const string wipeDeadSyncFile = "UltraAvatarTyndariusWipeDead.sync";
        const string wipeAliveSyncFile = "UltraAvatarTyndariusWipeAlive.sync";
        const string fightTimeSyncFile = "UltraAvatarTyndariusFightTime.sync";
        const string completionSyncFile = "UltraAvatarTyndariusCompletion.sync";

        const int questId = 8245;

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
        if (_role == "TynTaunter1")
        {
            C.Logger("[UltraAvatarTyndarius-v3] TynTaunter1 (Primary) — setting fight start time.");
            fightStartTime = UltraAsync.SetFightTime(C, fightTimeSyncPath);
            UltraAsync.StartTauntLoop(Bot, C, Engine, fightStartTime, 0, 2);
        }
        else if (_role == "TynTaunter2AttackBall2")
        {
            C.Logger("[UltraAvatarTyndarius-v3] TynTaunter2AttackBall2 (Secondary) — reading fight start time.");
            fightStartTime = UltraAsync.GetFightTime(Ultra, C, fightTimeSyncPath);
            UltraAsync.StartTauntLoop(Bot, C, Engine, fightStartTime, 1, 2);
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
                C.Logger("Ultra Avatar Tyndarius defeated. Finishing quest.");
                Engine.Join(map);
                Ultra.JoinHouse();
                UltraGeneral.CompleteQuest(Bot, questId);
                Bot.Sleep(30000);
                break;
            }

            if (_role.StartsWith("Ball2Attacker") || _role == "TynTaunter2AttackBall2")
            {
                // Attack right orb (MapID 3) → left orb (MapID 1) → Tyndarius (MapID 2)
                if (Bot.Monsters.CurrentAvailableMonsters.Any(x => x != null && x.MapID == 3 && x.HP > 0))
                {
                    if (Bot.Player.Target?.MapID != 3)
                        Bot.Combat.Attack(3);
                }
                else if (Bot.Monsters.CurrentAvailableMonsters.Any(x => x != null && x.MapID == 1 && x.HP > 0))
                {
                    if (Bot.Player.Target?.MapID != 1)
                        Bot.Combat.Attack(1);
                }
                else if (Bot.Player.Target?.MapID != 2)
                {
                    Bot.Combat.Attack(2);
                }
            }
            else
            {
                // TynTaunter1 — always on Tyndarius
                if (Bot.Player.Target?.Name != boss)
                    Bot.Combat.Attack(boss);
            }

            Pots.ActivateEquippedPotion();

            Bot.Sleep(500);
        }
    }
}
