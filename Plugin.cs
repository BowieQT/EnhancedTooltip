using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ImGuiNET;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using static DieselExileTools.Common.DXTC.Palettes.Material;
using static DieselExileTools.DXT;
using static DieselExileTools.ExileCore2.DXT;
using static DieselExileTools.ExileCore2.DXT.Tooltip;
using static EnhancedTooltip.TooltipParser;
using static ExileCore2.PoEMemory.Elements.Village.CurrencyExchangePanelOrderElement;
using SColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace EnhancedTooltip;

public class Plugin : BaseSettingsPlugin<Settings> {
    //--| Properties |-------------------------------------------------------------------------------------------------
    private UserInterface _userInterface;
    public UserInterface UserInterface => _userInterface ??= new UserInterface(this);

    private TooltipParser _tooltiParser;
    public TooltipParser TooltipParser => _tooltiParser ??= new TooltipParser(this);

    //--| Initialise |--------------------------------------------------------------------------------------------------
    public override bool Initialise() {
        CanUseMultiThreading = true;
        Initialise_DXT();

        return base.Initialise();
    }
    private void Initialise_DXT() {

        DXT.Initialise(new DXT.Config
        {
            PluginName = Name,
            PluginDirectory = DirectoryFullName,
            GameController = GameController,
            Graphics = Graphics,
            Settings = Settings.DXT,
        });

        DBug.LogHeader = (width, height) => {
            //DXT.Button.Draw($"{Name}User", ref Settings.DebugUser, new DXT.Button.Options { Label = "User", Width = 80, Height = 22 }); ImGui.SameLine();
        };
    }
    //--| Draw Settings |-----------------------------------------------------------------------------------------------
    public override void DrawSettings() {
        UserInterface.Draw();

    }
    //--| Tick |-------------------------------------------------------------------------------------------------------
    public override void Tick() {

    }
    //--| Render |-----------------------------------------------------------------------------------------------------
    public override void Render() {

        if (Settings.DumpItemNames_Hotkey.PressedOnce()) DumpModNames();

        DrawToolTip();

        DBug.Render();

        TooltipParser.Render();        
    }

    //--| Tooltip |----------------------------------------------------------------------------------------------------

    private void DrawToolTip() {
        var hoverItem = GameController.Game.IngameState.UIHover?.AsObject<HoverItemIcon>();
        if (hoverItem == null) return;

        var item = hoverItem.Item;
        if (item == null || !item.IsValid) return; 

        var tooltip = hoverItem.Tooltip;
        if (tooltip == null || !tooltip.IsVisible) return;
        var tooltipRect = tooltip.GetClientRect();
        if (tooltipRect.Width <= 0 || tooltipRect.Height <= 0) return;

        var linesToDraw = new List<ColoredText>();
        var needSeperator = false;

        // WEAPON DPS
        var (totalDPS, physDPS, eleDPS, chaosDPS) = GetWeaponDPS(item);
        if (totalDPS > 0) {
            linesToDraw.Add(new ColoredText(new List<ColorSegment> {
                new ("Total DPS: "),
                new (totalDPS.ToString("0.0"), Settings.Roll_Color),
                new (" Phys DPS: "),
                new (physDPS.ToString("0.0"), Settings.Roll_Color),
                new (" Ele DPS: "),
                new (eleDPS.ToString("0.0"), Settings.Roll_Color),
                new (" Chaos DPS: "),
                new (chaosDPS.ToString("0.0"), Settings.Roll_Color)
            }));
            needSeperator = true;
        }

        // MODS
        var itemMods = TooltipParser.ExtractMods(tooltip);
        int prefixCount = itemMods.Count(m => m.Category == ModCategory.Prefix);
        int suffixCount = itemMods.Count(m => m.Category == ModCategory.Suffix);
        var modDrawCount = prefixCount + suffixCount;
        bool drawnPrefixHeader = false;
        bool drawnSuffixHeader = false;

        if (modDrawCount > 0) {
            // SPACER
            if (modDrawCount > 0 && needSeperator) {
                needSeperator = false;
                linesToDraw.Add(new ColoredText(" "));
            }

            foreach (var mod in itemMods) {
                if (mod.Category == ModCategory.Prefix && !drawnPrefixHeader) {
                    linesToDraw.Add(new ColoredText($"Prefixes: {prefixCount}", Settings.PrefixHeader_Color));
                    drawnPrefixHeader = true;
                }
                if (mod.Category == ModCategory.Suffix && !drawnSuffixHeader) {
                    linesToDraw.Add(new ColoredText($"Suffixes: {suffixCount}", Settings.SuffixHeader_Color));
                    drawnSuffixHeader = true;
                }

                if(mod.Category == ModCategory.Suffix || mod.Category == ModCategory.Prefix) {
                    var builtModLines = BuildModLines(mod);
                    foreach (var line in builtModLines) linesToDraw.Add(line);
                }
            }          
        }
        if (linesToDraw.Count() < 1) return;
        // TOOLTIP LAYOUT VARS
        var textSize = Graphics.MeasureText("X");
        var tooltipOffset = 10;
        var tooltipPadding = 10;
        // DRAW BG
        var myTooltipRect = new ExileCore2.Shared.RectangleF(tooltipRect.Left, tooltipRect.Bottom + tooltipOffset, tooltipRect.Width, textSize.Y * linesToDraw.Count + (tooltipPadding * 2));
        Graphics.DrawBox(myTooltipRect, Settings.TooltipBG_Color); 
        Graphics.DrawFrame(myTooltipRect, Settings.TooltipBorder_Color, 1); 
        // DRAW LINES
        float currentY = myTooltipRect.Top + tooltipPadding;
        float startX = myTooltipRect.Left + tooltipPadding;
        foreach (var renderable in linesToDraw) {
            renderable.Draw(Graphics, new SVector2(startX, currentY), Settings.DefaultText_Color);
            currentY += renderable.Height;
        }
    }

    private List<ColoredText> BuildModLines(ModInfo modInfo) {
        var linesList = new List<ColoredText>();
        bool isFirstLine = true;
        const string AlignmentPadding = "      ";

        foreach (var line in modInfo.Lines) {
            var text = "";
            var textColor = Settings.DefaultText_Color;
            // FORMAT TEXT
            if (modInfo.IsVeiled) {
                textColor = Settings.Desecrated_Color;
                text = "Veiled Mod";
            }
            else {
                text = line.Body;
            }
            // CUSTOM MOD COLOR 
            foreach (var customModColor in Settings.CustomModColors) {
                if (modInfo.TextID.Equals(customModColor.ModName, StringComparison.OrdinalIgnoreCase)) {
                    if (customModColor.SelectedTier == 0 || modInfo.TierNum <= customModColor.SelectedTier) {
                        textColor = customModColor.Color;
                        break;
                    }
                }
            }
            // BUILD LINE
            var coloredLine = new List<ColorSegment>();
            // MOD TYPE LABEL
            if (isFirstLine) {
                // DESECRATED
                if (modInfo.IsDesecrated) {
                    coloredLine.Add(new(" D ", Settings.Desecrated_Color));
                } // CRAFTED
                else if (modInfo.IsCrafted) {
                    coloredLine.Add(new(" C ", Settings.Crafted_Color));
                }
                else {
                    coloredLine.Add(new("   "));
                }
            }
            // TIER (Only for first line)
            if (isFirstLine) {
                var tierColor = modInfo.TierNum switch {
                    1 => Settings.Tier1Color_Color,
                    2 => Settings.Tier2Color_Color,
                    3 => Settings.Tier3Color_Color,
                    _ => Settings.LowTier_Color
                };
                coloredLine.Add(new($"T{modInfo.TierNum}".PadRight(3), tierColor));
            }
            else {
                coloredLine.Add(new(AlignmentPadding));
            }
            // PREFIX
            if (!string.IsNullOrEmpty(line.RollPrefix)) {
                coloredLine.Add(new(line.RollPrefix + (line.RollPrefix.Length > 1 ? " " : ""), textColor));
            }
            // ROLLS
            for (int i = 0; i < line.Rolls.Count; i++) {
                if (i == 1) coloredLine.Add(new(" to ", textColor));
                coloredLine.Add(new(line.Rolls[i].Current.ToString(), Settings.Roll_Color));
            }
            // SUFFIX
            if (!string.IsNullOrEmpty(line.RollSuffix)) {
                coloredLine.Add(new(line.RollSuffix, textColor));
            }
            // TEXT
            coloredLine.Add(new( (line.Rolls.Count > 0 ? " " : "") + text, textColor));
            // ADD
            linesList.Add(new ColoredText(coloredLine));
            isFirstLine = false;
        }
        return linesList;
    }
    private (float dps, float phys, float ele, float chaos) GetWeaponDPS(Entity item) {
        if (!item.TryGetComponent<Weapon>(out var weapon) || !item.TryGetComponent<LocalStats>(out var localStats)) {
            return (0, 0, 0, 0);
        }
        // QUALITY
        item.TryGetComponent<Quality>(out var qualityComp);
        var quality = 1;
        if (qualityComp != null) { quality = qualityComp.ItemQuality; }
        // APS
        float aps = 1000f / weapon.AttackTime;
        // physical
        float physMin = weapon.DamageMin;
        float physMax = weapon.DamageMax;
        float physMultiplier = 1.0f;
        // elemental totals
        float fireMin = 0;
        float fireMax = 0;
        float coldMin = 0;
        float coldMax = 0;
        float lightningMin = 0;
        float lightningMax = 0;
        // chaos
        float chaosMin = 0;
        float chaosMax = 0;
        // SCAN MODS
        foreach (var stat in localStats.StatDictionary) {
            switch (stat.Key) {
                // phys %
                case GameStat.LocalPhysicalDamagePct:
                    physMultiplier += stat.Value / 100f;
                    break;
                // flat phys 
                case GameStat.LocalMinimumAddedPhysicalDamage:
                    physMin += stat.Value;
                    break;
                case GameStat.LocalMaximumAddedPhysicalDamage:
                    physMax += stat.Value;
                    break;
                // attack speed
                case GameStat.LocalAttackSpeedPct:
                    aps *= (100f + stat.Value) / 100f;
                    break;
                // fire
                case GameStat.LocalMinimumAddedFireDamage:
                    fireMin += stat.Value;
                    break;
                case GameStat.LocalMaximumAddedFireDamage:
                    fireMax += stat.Value;
                    break;
                // cold 
                case GameStat.LocalMinimumAddedColdDamage:
                    coldMin += stat.Value;
                    break;
                case GameStat.LocalMaximumAddedColdDamage:
                    coldMax += stat.Value;
                    break;
                // lightning
                case GameStat.LocalMinimumAddedLightningDamage:
                    lightningMin += stat.Value;
                    break;
                case GameStat.LocalMaximumAddedLightningDamage:
                    lightningMax += stat.Value;
                    break;
                // chaos
                case GameStat.LocalMinimumAddedChaosDamage:
                    chaosMin += stat.Value;
                    break;
                case GameStat.LocalMaximumAddedChaosDamage:
                    chaosMax += stat.Value;
                    break;
            }
        }
        // CALC
        float basePhysMin = physMin;
        float basePhysMax = physMax;
        // APPLY PHYS SCALING
        physMin *= physMultiplier;
        physMax *= physMultiplier;
        // APPLY quality
        float qualityMult = (1f + quality / 100f);
        physMin *= qualityMult;
        physMax *= qualityMult;
        // DPS
        float pdps = ((physMin + physMax) / 2f) * aps;
        // fire DPS
        float fdps = ((fireMin + fireMax) / 2f) * aps;
        // cold DPS
        float cdps = ((coldMin + coldMax) / 2f) * aps;
        // lightning DPS
        float ldps = ((lightningMin + lightningMax) / 2f) * aps;
        // chaos DPS
        float chaosdps = ((chaosMin + chaosMax) / 2f) * aps;
        // elemental DPS
        float edps = fdps + cdps + ldps;
        // totlal DPS
        float totalDPS = pdps + edps + chaosdps;
        // MONITOR
        if (Settings.DXT.DBug.ShowMonitor) {
            DBug.Monitor("DPS", "quality", quality);
            DBug.Monitor("DPS", "physMultiplier", physMultiplier);
            DBug.Monitor("DPS", "physMin", physMin);
            DBug.Monitor("DPS", "physMax", physMax);
            DBug.Monitor("DPS", "aps", aps);
            DBug.Monitor("DPS", "phys dps", pdps);
            DBug.Monitor("DPS", "fire dps", fdps);
            DBug.Monitor("DPS", "cold dps", cdps);
            DBug.Monitor("DPS", "lightning dps", ldps);
            DBug.Monitor("DPS", "chaosdps", chaosdps);
            DBug.Monitor("DPS", "elemental dps", edps);
            DBug.Monitor("DPS", "total dps", totalDPS);
        }
        // RUNES ** not implemented yet **
        float ironL = (((basePhysMin * physMultiplier * qualityMult) * (1f + 0.14f)) + ((basePhysMax * physMultiplier * qualityMult) * (1f + 0.14f))) / 2f * aps - pdps;
        float ironN = (((basePhysMin * physMultiplier * qualityMult) * (1f + 0.16f)) + ((basePhysMax * physMultiplier * qualityMult) * (1f + 0.16f))) / 2f * aps - pdps;
        float ironG = (((basePhysMin * physMultiplier * qualityMult) * (1f + 0.18f)) + ((basePhysMax * physMultiplier * qualityMult) * (1f + 0.18f))) / 2f * aps - pdps;

        float desertL = ((5f + 8f) / 2f) * aps;
        float desertN = ((7f + 11f) / 2f) * aps;
        float desertG = ((13f + 16f) / 2f) * aps;

        float glacialL = ((4f + 7f) / 2f) * aps;
        float glacialN = ((6f + 10f) / 2f) * aps;
        float glacialG = ((9f + 15f) / 2f) * aps;

        float stormL = ((1f + 14f) / 2f) * aps;
        float stormN = ((1f + 20f) / 2f) * aps;
        float stormG = ((1f + 29f) / 2f) * aps;



        return (totalDPS, pdps, edps, chaosdps);
    }

    private void DumpModNames() {
        var hoverItem = GameController.Game.IngameState.UIHover?.AsObject<HoverItemIcon>();
        if (hoverItem == null) return;

        var tooltip = hoverItem.Tooltip;
        if (tooltip == null || !tooltip.IsVisible) return;

        var itemMods = TooltipParser.ExtractMods(tooltip);

        var sb = new System.Text.StringBuilder();
        foreach (var mod in itemMods) {
            if (mod.Category == ModCategory.Prefix || mod.Category == ModCategory.Suffix) sb.AppendLine(mod.TextID);
        }
        UserInterface.CapturedNames = sb.ToString().TrimEnd(); // TrimEnd removes the last extra newline
    }

}
