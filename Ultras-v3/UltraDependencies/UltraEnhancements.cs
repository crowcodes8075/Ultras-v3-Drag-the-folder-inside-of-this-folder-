/*
name: UltraEnhancements
description: Centralised enhancement presets for all Ultra boss scripts.
tags: ultra, enhancements
*/

//cs_include Scripts/Ultras-v3/CoreDependencies/CoreBots2.cs
//cs_include Scripts/Ultras-v3/CoreDependencies/CoreFarms2.cs
//cs_include Scripts/Ultras-v3/CoreDependencies/CoreAdvanced2.cs

using Skua.Core.Interfaces;
using Skua.Core.Models.Items;

public class UltraEnhancements
{
    public IScriptInterface Bot => IScriptInterface.Instance;
    private CoreBots C => CoreBots.Instance;

    private static CoreAdvanced Adv
    {
        get => _Adv ??= new CoreAdvanced();
        set => _Adv = value;
    }
    private static CoreAdvanced _Adv;

    /// <summary>
    /// Applies the recommended enhancement preset for the currently equipped class.
    /// Falls back to SmartEnhance if the class has no explicit preset.
    /// </summary>
    public void Apply()
    {
        string className = Bot.Player?.CurrentClass?.Name ?? string.Empty;
        if (string.IsNullOrEmpty(className))
            return;

        C.Logger($"[UltraEnhancements] Enhancing for: {className}");

        switch (className)
        {
            // ── Taunters / Support ────────────────────────────────────────────

            case "ArchPaladin":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Fighter,
                    hSpecial: HelmSpecial.Forge,
                    wSpecial: WeaponSpecial.Valiance,
                    cSpecial: CapeSpecial.Absolution
                );
                break;

            case "Lord Of Order":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Fighter,
                    wSpecial: WeaponSpecial.Health_Vamp,
                    cSpecial: CapeSpecial.Absolution
                );
                break;

            case "StoneCrusher":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Fighter,
                    hSpecial: HelmSpecial.Anima,
                    wSpecial: WeaponSpecial.Awe_Blast,
                    cSpecial: CapeSpecial.Absolution
                );
                break;


            case "Verus DoomKnight":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Fighter,
                    hSpecial: HelmSpecial.Forge,
                    wSpecial: WeaponSpecial.Arcanas_Concerto,
                    cSpecial: CapeSpecial.Absolution
                );
                break;

            case "King's Echo":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Healer,
                    hSpecial: HelmSpecial.Examen,
                    wSpecial: WeaponSpecial.Elysium,
                    cSpecial: CapeSpecial.Absolution
                );
                break;

            // ── Fallback ──────────────────────────────────────────────────────

            default:
                C.Logger($"[UltraEnhancements] No preset for '{className}', using SmartEnhance.");
                Adv.SmartEnhance(className);
                break;
        }
    }

    /// <summary>
    /// Applies the Ultra Dage enhancement strategy for all classes.
    /// Uses fixed Lucky across all gear with Health Vamp on weapon — no Forge anywhere,
    /// since healing is detrimental in the Dage fight.
    /// </summary>
    public void ApplyDage()
    {
        string className = Bot.Player?.CurrentClass?.Name ?? string.Empty;
        if (string.IsNullOrEmpty(className))
            return;

        C.Logger($"[UltraEnhancements] Dage enhancing for: {className} using fixed Lucky preset");

        switch (className)
        {
            case "King's Echo":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Healer,
                    hSpecial: HelmSpecial.Examen,
                    wSpecial: WeaponSpecial.Health_Vamp,
                    cSpecial: CapeSpecial.Lament
                );
                break;

            default:
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    wSpecial: WeaponSpecial.Health_Vamp,
                    cSpecial: CapeSpecial.Vainglory
                );
                break;
        }
    }

    public void ApplyGramiel()
    {
        string className = Bot.Player?.CurrentClass?.Name ?? string.Empty;
        if (string.IsNullOrEmpty(className))
            return;

        C.Logger($"[UltraEnhancements] Gramiel enhancing for: {className}");

        switch (className)
        {

            default:
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    wSpecial: WeaponSpecial.Health_Vamp,
                    cSpecial: CapeSpecial.Absolution
                );
                break;
        }
    }

    public void ApplyAzalith()
    {
        string className = Bot.Player?.CurrentClass?.Name ?? string.Empty;
        if (string.IsNullOrEmpty(className))
            return;

        C.Logger($"[UltraEnhancements] Azalith enhancing for: {className}");

        switch (className)
        {
            case "King's Echo":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    hSpecial: HelmSpecial.Examen,
                    wSpecial: WeaponSpecial.Praxis,
                    cSpecial: CapeSpecial.Lament
                );
                break;

            case "Lord Of Order":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    wSpecial: WeaponSpecial.Mana_Vamp
                );
                break;

            case "StoneCrusher":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    wSpecial: WeaponSpecial.Lacerate
                );
                break;

            case "DragonSoul Shinobi":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    hSpecial: HelmSpecial.Vim,
                    wSpecial: WeaponSpecial.Mana_Vamp
                );
                break;
            
            case "Yami no Ronin":
                Adv.EnhanceEquipped(
                    type: EnhancementType.Lucky,
                    hSpecial: HelmSpecial.Vim,
                    wSpecial: WeaponSpecial.Mana_Vamp
                );
                break;

            default:
                C.Logger($"[UltraEnhancements] No preset for '{className}', using SmartEnhance.");
                Adv.SmartEnhance(className);
                break;
        }
    }

    public void ApplyNoVainglory()
    {
        Apply();

        string className = Bot.Player?.CurrentClass?.Name ?? string.Empty;
        if (string.IsNullOrEmpty(className))
            return;

        InventoryItem? cape = Bot.Inventory.Items.Find(i => i.Equipped && i.Category == ItemCategory.Cape);
        if (cape == null)
        {
            C.Logger($"[UltraEnhancements] No equipped cape found for Nulgath override ({className}).");
            return;
        }

        C.Logger($"[UltraEnhancements] NoVainglory enhancing for: {className} and forcing Forge cape");
        Adv.EnhanceItem(cape.Name, EnhancementType.Lucky, cSpecial: CapeSpecial.Absolution);

        // InventoryItem? weapon = Bot.Inventory.Items.Find(i => i.Equipped && (
        //     i.Category == ItemCategory.Sword
        //     || i.Category == ItemCategory.Axe
        //     || i.Category == ItemCategory.Dagger
        //     || i.Category == ItemCategory.Gun
        //     || i.Category == ItemCategory.HandGun
        //     || i.Category == ItemCategory.Rifle
        //     || i.Category == ItemCategory.Bow
        //     || i.Category == ItemCategory.Mace
        //     || i.Category == ItemCategory.Gauntlet
        //     || i.Category == ItemCategory.Polearm
        //     || i.Category == ItemCategory.Staff
        //     || i.Category == ItemCategory.Wand
        //     || i.Category == ItemCategory.Whip
        // ));
        // if (weapon != null)
        // {
        //     C.Logger($"[UltraEnhancements] Enhancing equipped weapon '{weapon.Name}' with Health_Vamp");
        //     Adv.EnhanceItem(weapon.Name, EnhancementType.Healer, wSpecial: WeaponSpecial.Health_Vamp);
        // }
        // else
        // {
        //     C.Logger("[UltraEnhancements] No equipped weapon found for Health_Vamp enhancement.");
        // }
    }
}

