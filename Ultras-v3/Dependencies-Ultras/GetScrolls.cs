/*
name: GetScrolls
description: Provides methods for acquiring combat scrolls (Enrage, Decay).
tags: ultra,scrolls,enrage,decay
*/

//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreEngine2.cs
//cs_include Scripts/Ultras-v3/Dependencies-Ultras/CoreUltra2.cs
//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreBots2.cs
//cs_include Scripts/Ultras-v3/Dependencies-Core/CoreAdvanced2.cs

using System.Linq;
using Skua.Core.Interfaces;

public class GetScrolls
{
    private static IScriptInterface Bot => IScriptInterface.Instance;
    private static CoreEngine2 Core => CoreEngine2.Instance;
    private static CoreBots2 C => CoreBots2.Instance;

    /// <summary>
    /// Farms and equips Scroll of Enrage (up to 200). Requires SpellCrafting rank 5.
    /// </summary>
    public void GetScrollOfEnrage()
    {
        const int desiredCount = 200;

        if (!Core.Faction("SpellCrafting", 5))
            return;

        const string parchment = "Mystic Parchment";
        const string ink = "Zealous Ink";
        const string scroll = "Scroll of Enrage";

        while (!C.CheckInventory(scroll, desiredCount))
        {
            // Mats
            Core.ForItem("Undead Infantry", "underworld", parchment, 2);
            Core.BuyItem(ink, 549, "dragonrune", 5, calculateRemaining: false);

            // Craft
            Core.Join("spellcraft");
            Bot.Drops.Add(scroll);
            Bot.Send.Packet("%xt%zm%crafting%1%spellOnStart%7%1555%Spell%");
            Bot.Sleep(5000);
            Bot.Send.Packet("%xt%zm%crafting%1%spellComplete%7%2330%Enrage%");

            Core.WaitForDrop(scroll, 10000);
            Core.Pickup(scroll);
            Bot.Drops.Remove(scroll);

            if (Bot.ShouldExit)
                break;
        }

    }

    /// <summary>
    /// Farms Scroll of Decay (up to 50). Requires SpellCrafting rank 5.
    /// Does NOT equip — call CoreEngine.EquipConsumable("Scroll of Decay") separately.
    /// </summary>
    public void GetScrollOfDecay()
    {
        if (!Core.Faction("SpellCrafting", 5))
            return;

        const string parchment = "Mystic Parchment";
        const string ink = "Zealous Ink";
        const string scroll = "Scroll of Decay";

        while (!C.CheckInventory(scroll, 50))
        {
            Core.ForItem("Undead Infantry", "underworld", parchment, 2);
            Core.BuyItem(ink, 549, "dragonrune", 5, calculateRemaining: false);

            Core.Join("spellcraft");
            Bot.Drops.Add(scroll);
            Bot.Send.Packet("%xt%zm%crafting%1%spellOnStart%7%1555%Spell%");
            Bot.Sleep(5000);
            Bot.Send.Packet("%xt%zm%crafting%1%spellComplete%7%2331%Decay%");

            Core.WaitForDrop(scroll, 5000);
            Core.Pickup(scroll);
        }
    }
}
