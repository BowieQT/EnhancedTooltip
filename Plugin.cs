using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ImGuiNET;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Numerics;
using System.Text.RegularExpressions;
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

        DrawToolTip();

        DBug.Render();
    }


    //--| Tooltip |----------------------------------------------------------------------------------------------------

    public class RollRange {
        public double Current { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
    } 
    public enum ModCategory { // Doubles as sort order for list of mods
        Enchant,
        Implicit,
        Prefix,
        Suffix,
        Unique,
        Unknown,
    }
    public class ModLine {
        public List<RollRange> Rolls = new List<RollRange>();
        public string Text;
        public string RollPrefix = "";
        public string RollSuffix = "";
    }
    public class ModInfo {
        public int Index { get; set; }
        public string Tier { get; set; }
        public ModCategory Category { get; set; }
        public string Description { get; set; }
        public List<ModLine> Lines { get; set; } = new List<ModLine>();
    }
    private string CleanLine(string input) {
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

        return text.Trim();
    }
    private void ParseModText(ModInfo mod) {
        foreach (string rawLine in mod.Description.Split('\n')) {
            string line = CleanLine(rawLine);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var modLine = new ModLine();

            // 1. Identify all ranges
            var matches = Regex.Matches(line, @"(?<current>\d+(?:\.\d+)?)\((?<min>\d+(?:\.\d+)?)-(?<max>\d+(?:\.\d+)?)\)");

            if (matches.Count > 0) {
                // Store all rolls
                foreach (Match m in matches) {
                    modLine.Rolls.Add(new RollRange {
                        Current = double.Parse(m.Groups["current"].Value),
                        Min = double.Parse(m.Groups["min"].Value),
                        Max = double.Parse(m.Groups["max"].Value)
                    });
                }

                // 2. Prefix: Grab everything before the very first roll
                modLine.RollPrefix = line.Substring(0, matches[0].Index).Trim();

                // 3. Text: Grab everything after the very last roll
                var lastMatch = matches[matches.Count - 1];
                string after = line.Substring(lastMatch.Index + lastMatch.Length).Trim();

                // 4. Clean the "to" out of the text if it was caught there
                // This regex removes the leading "to " if the text starts with it
                var textMatch = Regex.Match(after, @"^(?<suffix>[%\s]*)(to\s+)?(?<text>.*)", RegexOptions.IgnoreCase);
                modLine.RollSuffix = textMatch.Groups["suffix"].Value.Trim();
                modLine.Text = textMatch.Groups["text"].Value.Trim();
            }
            else {
                modLine.Text = line;
            }
            mod.Lines.Add(modLine);
        }
    }
    public List<ModInfo> ExtractMods(Element root) {
        var allElements = new List<Element>();
        void Flatten(Element e) {
            if (e == null) return;
            allElements.Add(e);
            if (e.Children != null) foreach (var child in e.Children) Flatten(child);
        }
        Flatten(root);

        var results = new List<ModInfo>();

        // First Pass: Find all mods
        for (int i = 0; i < allElements.Count; i++) {
            var e = allElements[i];
            if (e.Text != null && e.Text.Contains("(") && !e.Text.Contains("Requires:")) {

                string tier = null;
                for (int j = i + 1; j < Math.Min(i + 8, allElements.Count); j++) {
                    if (allElements[j].Text != null && allElements[j].Text.Contains("<smaller>")) {
                        tier = allElements[j].Text.Replace("<smaller>{", "").Replace("}", "").Trim();
                        break;
                    }
                }

                if (tier != null) {
                    var mod = new ModInfo {
                        Description = e.Text.Trim(),
                        Index = i,
                        Tier = tier
                    };
                    mod.Category = tier switch {
                        "I" => ModCategory.Implicit,
                        "U" => ModCategory.Unique,
                        "E" => ModCategory.Enchant,
                        _ => ModCategory.Unknown // Keep it Unknown so the header logic can take over
                    };
                    ParseModText(mod);

                    results.Add(mod);
                }
            }
        }

        // Second Pass: Assign Category based on where the Header exists in the list
        var headers = allElements.Where(e => e.Text == "Prefix" || e.Text == "Suffix").ToList();
        foreach (var mod in results) {
            if (mod.Category != ModCategory.Unknown) continue;
            var header = headers.FirstOrDefault(h => allElements.IndexOf(h) > mod.Index);
            if (header != null) mod.Category = (header.Text == "Prefix") ? ModCategory.Prefix : ModCategory.Suffix;
        }

        results.Sort((a, b) => a.Category.CompareTo(b.Category));

        return results;
    }

    public void DrawToolTip() {
        var hoverItem = GameController.Game.IngameState.UIHover?.AsObject<HoverItemIcon>();
        if (hoverItem == null) return;

        var tooltip = hoverItem.Tooltip;
        if (tooltip == null || !tooltip.IsVisible) return;
        var tooltipRect = tooltip.GetClientRect();
        if (tooltipRect.Width <= 0 || tooltipRect.Height <= 0) return;

        var linesToDraw = new List<ColoredText>();

        // WEAPON DPS
        var item = hoverItem.Item;
        var (totalDPS, physDPS, eleDPS, chaosDPS) = GetWeaponDPS(hoverItem.Item);
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
        }

        // MODS
        var itemMods = ExtractMods(tooltip);
        int prefixCount = itemMods.Count(m => m.Category == ModCategory.Prefix);
        int suffixCount = itemMods.Count(m => m.Category == ModCategory.Suffix);
        if (prefixCount + suffixCount > 0) {
            if (linesToDraw.Count > 0 )
                linesToDraw.Add(new ColoredText(" ")); // space from weapon dps

            bool drawnPrefixHeader = false;
            bool drawnSuffixHeader = false;

            foreach (var mod in itemMods) {
                if (mod.Category == ModCategory.Prefix && !drawnPrefixHeader) {
                    linesToDraw.Add( new ColoredText($"Prefixes: {prefixCount}", Settings.PrefixHeader_Color) );
                    drawnPrefixHeader = true;
                }
                if (mod.Category == ModCategory.Suffix && !drawnSuffixHeader) {
                    linesToDraw.Add( new ColoredText($"Suffixes: {suffixCount}", Settings.SuffixHeader_Color) );
                    drawnSuffixHeader = true;
                }

                var builtModLines = BuildModLines(mod);
                foreach (var line in builtModLines) linesToDraw.Add(line); 
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
        if (item == null || !item.IsValid || !item.TryGetComponent<Weapon>(out var weapon) || !item.TryGetComponent<LocalStats>(out var localStats)) {
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

    private List<ColoredText> BuildModLines(ModInfo mod) {
        var linesList = new List<ColoredText>();
        bool isFirstLine = true;
        const string AlignmentPadding = "    ";

        foreach (var line in mod.Lines) {
            if (mod.Category != ModCategory.Prefix && mod.Category != ModCategory.Suffix) continue;

            var coloredLine = new List<ColorSegment>();
            // Padding
            coloredLine.Add(new("  "));
            // TIER (Only for first line)
            if (isFirstLine) {
                var tierColor = mod.Tier switch {
                    "T1" => Settings.Tier1Color_Color,
                    "T2" => Settings.Tier2Color_Color,
                    "T3" => Settings.Tier3Color_Color,
                    _ => Settings.LowTier_Color
                };
                coloredLine.Add(new(mod.Tier.PadRight(3), tierColor));
            }
            else {
                coloredLine.Add(new(AlignmentPadding));
            }
            // PREFIX
            if (!string.IsNullOrEmpty(line.RollPrefix)) {
                coloredLine.Add(new(line.RollPrefix + (line.RollPrefix.Length > 1 ? " " : "")));
            }
            // ROLLS
            for (int i = 0; i < line.Rolls.Count; i++) {
                if (i == 1) coloredLine.Add(new(" to "));
                coloredLine.Add(new(line.Rolls[i].Current.ToString(), Settings.Roll_Color));
            }
            // SUFFIX & MOD TEXT
            string suffixAndText = (string.IsNullOrEmpty(line.RollSuffix) ? "" : line.RollSuffix) + " " + line.Text;
            coloredLine.Add(new(suffixAndText));

            linesList.Add(new ColoredText(coloredLine));
            isFirstLine = false;
        }
        return linesList;
    }



}
