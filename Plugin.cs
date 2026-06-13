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
using static ExileCore2.PoEMemory.Elements.Village.CurrencyExchangePanelOrderElement;
using SColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace EnhancedTooltip;

public class Plugin : BaseSettingsPlugin<Settings> {
    //--| Properties |-------------------------------------------------------------------------------------------------
    private UserInterface _userInterface;
    private UserInterface UserInterface => _userInterface ??= new UserInterface(this);





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
    }


    //--| Tooltip |----------------------------------------------------------------------------------------------------

    //+3.74% to Critical Hit Chance
    //Adds 33 to 63 Physical Damage
    //Adds 79 to 123 Cold Damage
    //2% chance to gain Onslaught on Killing Hits with this Weapon
    //17% increased[Attack] Speed
    //Companions have +13% to all Elemental Resistances
    //13% increased Rarity of Items found
    //Adds 14 to 24 Physical Damage to Attacks
    //Adds 20 to 25 Cold damage to Attacks
    //+26 to Intelligence
    //+131 to maximum Life
    //Leech 8.89% of Physical Attack Damage as Mana

    private void DumpModNames() {
        var hoverItem = GameController.Game.IngameState.UIHover?.AsObject<HoverItemIcon>();
        if (hoverItem == null) return;

        var item = hoverItem.Item;
        if (item == null || !item.IsValid) return;

        if (!item.TryGetComponent<Mods>(out var itemModComp)) return;

        var sb = new System.Text.StringBuilder();
        foreach (var mod in itemModComp.ItemMods) {
            sb.AppendLine(mod.Name);
        }
        UserInterface.CapturedNames = sb.ToString().TrimEnd(); // TrimEnd removes the last extra newline
    }
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
        if (item.TryGetComponent<Mods>(out var itemModComp)) {
            List<ItemMod> prefixes = [];
            List<ItemMod> suffixes = [];
            foreach (var mod in itemModComp.ExplicitMods) {
                if (mod.ModRecord.AffixType == ModType.Prefix) prefixes.Add(mod);
                if (mod.ModRecord.AffixType == ModType.Suffix) suffixes.Add(mod);
            }
            var modDrawCount = prefixes.Count + suffixes.Count;
            // SPACER
            if (modDrawCount > 0 && needSeperator) {
                needSeperator = false;
                linesToDraw.Add(new ColoredText(" "));
            }
            // PREFIXES
            if (prefixes.Count > 0) {
                linesToDraw.Add(new ColoredText($"Prefixes: {prefixes.Count}", Settings.PrefixHeader_Color));
                foreach (var mod in prefixes) {
                    var builtModLines = BuildModLines(mod);
                    foreach (var line in builtModLines) linesToDraw.Add(line);
                }
            }
            // SUFFIXES
            if (suffixes.Count > 0) {
                linesToDraw.Add(new ColoredText($"Suffixes: {suffixes.Count}", Settings.SuffixHeader_Color));
                foreach (var mod in suffixes) {
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
    public (float dps, float phys, float ele, float chaos) GetWeaponDPS(Entity item) {
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

    private List<ColoredText> BuildModLines(ItemMod itemMod) {
        var linesList = new List<ColoredText>();
        bool isFirstLine = true;

        foreach (string modLine in itemMod.Translation.Split('\n')) {
            // FORMAT LINE
            var (cleanLine, isUnknown) = CleanLine(modLine);
            var rolls = itemMod.Values;
            var rollPrefix = "";
            var rollSuffix = "";
            var text = "";
            var textColor = Settings.DefaultText_Color;

            if (itemMod.Group == "VeiledPrefix") {
                textColor = Settings.Desecrated_Color;
                text = "Veiled Mod";
                rolls = [];
            }
            else if(isUnknown) {
                if (string.IsNullOrEmpty(itemMod.Name)) {
                    text = "UNKNOWN!!!";
                }
                else {
                    if (itemMod.ModRecord?.StatNames != null && itemMod.ModRecord.StatNames.Count() > 0 && itemMod.ModRecord.StatNames[0] != null) {
                        if (itemMod.ModRecord.StatNames[0].Type == StatType.Percents) rollSuffix = "%";
                    }
                    text = Regex.Replace(itemMod.Name, @"(Local|Percent)", "", RegexOptions.IgnoreCase);
                    text = Regex.Replace(text, @"(?<=[a-z])([A-Z])", " $1").Trim();
                }
            }
            else {
                if (string.IsNullOrWhiteSpace(cleanLine)) continue;
                var matches = Regex.Matches(cleanLine, @"\d+(?:\.\d+)?");
                if (matches.Count > 0) {
                    // Prefix: Everything before the first number
                    rollPrefix = cleanLine.Substring(0, matches[0].Index).Trim();
                    // Text/Suffix: Everything after the last number
                    var lastMatch = matches[matches.Count - 1];
                    string after = cleanLine.Substring(lastMatch.Index + lastMatch.Length).Trim();
                    // Cleans the "to" suffix logic
                    var textMatch = Regex.Match(after, @"^(?<suffix>[%\s]*)(to\s+)?(?<text>.*)", RegexOptions.IgnoreCase);
                    rollSuffix = textMatch.Groups["suffix"].Value.Trim();
                    text = textMatch.Groups["text"].Value.Trim();
                }
                else
                    text = cleanLine;

                // Custom Color
                foreach (var customModColor in Settings.CustomModColors) {
                    if (itemMod.Name.Contains(customModColor.ModName, StringComparison.OrdinalIgnoreCase)) {
                        textColor = customModColor.Color;
                        break;
                    }
                }

            }

            // BUILD LINE
            var coloredLine = new List<ColorSegment>();

            string AlignmentPadding = "    " + (Settings.ShowTierLevel ? "   " : "");
            // INDENT LINE
            coloredLine.Add(new("  "));
            // TIER (Only for first line)
            int modTier = GetModTier(itemMod);
            if (isFirstLine) {
                var tierColor = modTier switch {
                    1 => Settings.Tier1Color_Color,
                    2 => Settings.Tier2Color_Color,
                    3 => Settings.Tier3Color_Color,
                    _ => Settings.LowTier_Color
                };
                coloredLine.Add(new($"T{modTier}".PadRight(3), tierColor));
                if (Settings.ShowTierLevel) {
                    if (itemMod.Level > 0)
                        coloredLine.Add(new($"{itemMod.Level}".PadRight(3), Settings.TierLevel_Color));
                    else
                        coloredLine.Add(new("   "));
                }
            }
            else {
                coloredLine.Add(new(AlignmentPadding));
            }
            // PREFIX
            if (!string.IsNullOrEmpty(rollPrefix)) {
                coloredLine.Add(new(rollPrefix + (rollPrefix.Length > 1 ? " " : ""), textColor));
            }
            // ROLLS
            for (int i = 0; i < rolls.Count; i++) {
                if (i == 1) coloredLine.Add(new(" to "));
                coloredLine.Add(new(itemMod.Values[i].ToString(), Settings.Roll_Color));
            }
            // SUFFIX & MOD TEXT
            string suffixAndText = (string.IsNullOrEmpty(rollSuffix) ? "" : rollSuffix) + " " + text;
            coloredLine.Add(new(suffixAndText, textColor));
            // MOD TYPE LABEL
            if (isFirstLine) {
                // DESECRATED
                if(itemMod.ModRecord.Domain == ModDomain.Unveiled) {
                    coloredLine.Add(new(" Desecrated", Settings.Desecrated_Color));
                } // ESSENCE
                else if(itemMod.ModRecord.IsEssence) {
                    coloredLine.Add(new(" Essence", Settings.Essence_Color));
                }             
            }

            linesList.Add(new ColoredText(coloredLine));
            isFirstLine = false;
        }
        return linesList;
    }


    private (string Text, bool Unknown) CleanLine(string input) {
        // catch unknown
        if (input.Contains("<unknown")) return ("", true);

        // Remove standard <tag> patterns
        string text = Regex.Replace(input, @"<[^>]*>", "");
        // Strip all curly braces and their content recursively
        while (text.Contains("{")) {
            int start = text.LastIndexOf('{'); // Find the innermost '{'
            int end = text.IndexOf('}', start); // Find the matching '}'
            if (end != -1) {
                text = text.Remove(start, end - start + 1);
            }
            else {
                // No matching '}', just remove the stray '{'
                text = text.Remove(start, 1);
            }
        }
        // Process [Tag|Name] patterns 
        int s = text.IndexOf('[');
        while (s != -1) {
            int end = text.IndexOf(']', s);
            if (end != -1) {
                string fullBlock = text.Substring(s, (end - s) + 1);
                string content = text.Substring(s + 1, end - s - 1);
                var parts = content.Split('|');
                text = text.Replace(fullBlock, parts.Length == 2 ? parts[1] : parts[0]);
            }
            s = text.IndexOf('[', s + 1);
        }

        return (text.Trim(), false);
    }
    public int GetModTier(ItemMod itemMod) {
        var key = Tuple.Create(itemMod.ModRecord.Group, itemMod.ModRecord.AffixType);
        var recordsByTier = GameController.Files.Mods.recordsByTier;

        if (recordsByTier.TryGetValue(key, out var modFamily)) {
            var filteredList = modFamily
                .Where(m => m.TypeName == itemMod.ModRecord.TypeName)
                // Group by the name
                .GroupBy(m => m.UserFriendlyName)
                // For each duplicate UserFriendlyName, keep the record that matches itemMod Hash, otherwise just take the first one
                .Select(g => g.Any(m => m.Hash32 == itemMod.ModRecord.Hash32)
                             ? g.First(m => m.Hash32 == itemMod.ModRecord.Hash32)
                             : g.First())
                .ToList();

            int index = filteredList.FindIndex(m => m.Hash32 == itemMod.ModRecord.Hash32);

            return (index >= 0) ? index + 1 : 0;
        }

        return 0;
    }



    // <unknown LocalPhysicalDamagePct:128>

}
