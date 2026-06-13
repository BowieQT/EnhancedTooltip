using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ImGuiNET;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using SDColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace EnhancedTooltip;

public enum ModCategory { // Doubles as sort order for list of mods
    Enchant,
    Implicit,
    Prefix,
    Suffix,
    Unique,
    Unknown,
}

public sealed class TooltipParser(Plugin plugin) : PluginModule(plugin) {

    public class RollRange {
        public double Current { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
    }
    public class ModLine {
        public List<RollRange> Rolls { get; set; } = new();
        public string Body { get; set; } = "";
        public string RollPrefix { get; set; } = "";
        public string RollSuffix { get; set; } = "";
    }
    public class ModInfo {
        public int Index { get; set; }
        public string TierString { get; set; }
        public int TierNum { get; set; }
        public ModCategory Category { get; set; }
        public string TextID { get; set; }
        public string Text { get; set; }
        public string TextNoTags { get; set; }
        public bool IsCrafted { get; set; }
        public bool IsDesecrated { get; set; }
        public bool IsVeiled { get; set; }

        public List<ModLine> Lines { get; set; } = new List<ModLine>();
    }
    
    public void Render() {
        var hoverItem = GameController.Game.IngameState.UIHover?.AsObject<HoverItemIcon>();
        if (hoverItem == null) return;

        var tooltip = hoverItem.Tooltip;
        if (tooltip == null || !tooltip.IsVisible) return;

        if (Settings.DumpTooltip_Hotkey.PressedOnce()) {
            DumpTooltipToFile(tooltip);
        }

        if (Settings.DumpMods_Hotkey.PressedOnce()) {
           var results =  ExtractMods(tooltip);
            DumpModsToFile(results);
        }
    }


    private object GetElementData(Element e) {
        return new {
            e.Text,
            e.TextNoTags,
            e.PathFromRoot,
            Children = e.Children?.Select(GetElementData).ToList()
        };
    }
    private void DumpTooltipToFile(Element tooltip, string fileName = "element_dump.json") {
        string path = Path.Combine(Plugin.DirectoryFullName, fileName);

        var node = GetElementData(tooltip);

        string json = JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
    private void DumpModsToFile(List<ModInfo> mods, string fileName = "mod_extraction_dump.json") {
        string path = Path.Combine(Plugin.DirectoryFullName, fileName);

        string json = JsonSerializer.Serialize(mods, new JsonSerializerOptions {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() } 
        });

        File.WriteAllText(path, json);
    }

    private void ExtractModRolls_OLD(ModInfo mod) {
        foreach (string line in mod.TextNoTags.Split('\n')) {
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
                // Prefix
                modLine.RollPrefix = line.Substring(0, matches[0].Index).Trim();
                // Text: Grab everything after the very last roll
                var lastMatch = matches[matches.Count - 1];
                string after = line.Substring(lastMatch.Index + lastMatch.Length).Trim();
                // Clean "to" out of the text
                var textMatch = Regex.Match(after, @"^(?<suffix>[%\s]*)(to\s+)?(?<text>.*)", RegexOptions.IgnoreCase);
                modLine.RollSuffix = textMatch.Groups["suffix"].Value.Trim();
                modLine.Body = textMatch.Groups["text"].Value.Trim();
            }
            else {
                modLine.Body = line;
            }
            mod.Lines.Add(modLine);
        }
    }

    private void ExtractModRolls(ModInfo mod) {
        foreach (string rawLine in mod.TextNoTags.Split('\n')) {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var modLine = new ModLine();

            // RANGE ROLLS
            var matches = Regex.Matches(line,
                @"(?<current>\d+(?:\.\d+)?)\((?<min>\d+(?:\.\d+)?)-(?<max>\d+(?:\.\d+)?)\)");

            if (matches.Count > 0) {
                foreach (Match m in matches) {
                    modLine.Rolls.Add(new RollRange {
                        Current = double.Parse(m.Groups["current"].Value),
                        Min = double.Parse(m.Groups["min"].Value),
                        Max = double.Parse(m.Groups["max"].Value)
                    });
                }
                modLine.RollPrefix = line.Substring(0, matches[0].Index).Trim();

                var lastMatch = matches[^1];
                string after = line.Substring(lastMatch.Index + lastMatch.Length).Trim();

                var textMatch = Regex.Match(after,
                    @"^(?<suffix>[%\s]*)(to\s+)?(?<text>.*)",
                    RegexOptions.IgnoreCase);

                modLine.RollSuffix = textMatch.Groups["suffix"].Value.Trim();
                modLine.Body = textMatch.Groups["text"].Value.Trim();
            }
            // FLAT ROLLS (+50%, 50%, etc.)
            else {
                var flatMatch = Regex.Match(line,
                    @"^(?<prefix>[+\-]?)\s*(?<value>\d+(?:\.\d+)?)(?<suffix>%?)\s*(?<body>.*)$");

                if (flatMatch.Success) {
                    var value = double.Parse(flatMatch.Groups["value"].Value);

                    modLine.RollPrefix = flatMatch.Groups["prefix"].Value;

                    modLine.Rolls.Add(new RollRange {
                        Current = value,
                        Min = value,
                        Max = value
                    });

                    modLine.RollSuffix = flatMatch.Groups["suffix"].Value;
                    modLine.Body = flatMatch.Groups["body"].Value.Trim();
                }
                else {
                    // fallback: no numbers at all
                    modLine.Body = line;
                }
            }

            mod.Lines.Add(modLine);
        }
    }

    public List<ModInfo> ExtractMods(Element tooltip) {
        var results = new List<ModInfo>();
        if (tooltip == null) return results;

        TraverseForMods(tooltip, results);

        results.Sort((a, b) => a.Category.CompareTo(b.Category));

        return results;
    }
    private void TraverseForMods(Element e, List<ModInfo> results) {
        if (e?.Children == null) return;

        foreach (var child in e.Children) {
            var lastChild = child?.Children?.LastOrDefault();
            var categoryText = lastChild?.Children?.FirstOrDefault()?.TextNoTags;

            if (categoryText == "Prefix" || categoryText == "Suffix" || categoryText == "Unique" || categoryText == "Implicit") {
                var category = categoryText == "Prefix" ? ModCategory.Prefix :
                               categoryText == "Suffix" ? ModCategory.Suffix :
                               categoryText == "Unique" ? ModCategory.Unique :
                               ModCategory.Implicit;

                for (int i = 0; i < child.Children.Count - 1; i++) {
                    var modNode = child.Children[i];
                    if (modNode?.Children == null || modNode.Children.Count == 0) continue;

                    string text = modNode.Children[0]?.Text;
                    string textNoTags = modNode.Children[0]?.TextNoTags;
                    string tier = FindDeep(modNode, n => n.Text?.Contains("<smaller>") == true)?.TextNoTags;

                    if (string.IsNullOrEmpty(textNoTags) && tier == "D") textNoTags = "Veiled";

                    if (string.IsNullOrEmpty(textNoTags)) continue;

                    int tierNum = 1;
                    if (!string.IsNullOrEmpty(tier) &&
                        tier.StartsWith("T") &&
                        int.TryParse(tier.Substring(1), out int parsedTier)) {
                        tierNum = parsedTier;
                    }

                    var mod = new ModInfo {
                        Text = text,
                        TextNoTags = textNoTags,
                        TierString = tier,
                        Category = category,
                        IsVeiled = tier == "D",
                        IsDesecrated = (modNode.Children.Count == 3 && (modNode.Children[1]?.Children == null || modNode.Children[1].Children.Count == 0)),
                        IsCrafted = tier == "C",
                        TierNum = tierNum,
                    };
                    ExtractModRolls(mod);
                    mod.TextID = string.Concat(mod.Lines.Select(l => l.Body ?? ""));

                    results.Add(mod);
                }
            }
            else {
                TraverseForMods(child, results);
            }
        }
    }
    private Element FindDeep(Element e, Func<Element, bool> predicate) {
        if (e == null) return null;
        if (predicate(e)) return e;
        foreach (var child in e.Children ?? []) {
            var found = FindDeep(child, predicate);
            if (found != null) return found;
        }
        return null;
    }
     
}