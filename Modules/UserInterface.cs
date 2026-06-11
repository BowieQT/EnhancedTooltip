using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ImGuiNET;
using SDColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace EnhancedTooltip;


public sealed class UserInterface : PluginModule {
    public UserInterface(Plugin plugin) : base(plugin) { }

    public void Draw() {
        DXT.Button.Draw("ShowDBug", ref Settings.DXT.DBug.ShowToolbar, new DXT.Button.Options {
            Label = "DBug",
            Width = 100,
            Height = 22,
        });

        DXT.ColorSelect.Draw($"BGColor", "Tooltip BG Color", ref Settings.TooltipBG_Color);
        ImGui.SameLine();
        ImGui.Text($"Tooltip BG Color");

        DXT.ColorSelect.Draw($"BorderColor", "Tooltip Border Color", ref Settings.TooltipBorder_Color);
        ImGui.SameLine();
        ImGui.Text($"Tooltip Border Color");

        DXT.ColorSelect.Draw($"DefaultTextColor", "Default Text Color", ref Settings.DefaultText_Color);
        ImGui.SameLine();
        ImGui.Text($"Default Text Color");

        DXT.ColorSelect.Draw($"PrefixColor", "Prefix Header Color", ref Settings.PrefixHeader_Color);
        ImGui.SameLine();
        ImGui.Text($"Header Header Color");

        DXT.ColorSelect.Draw($"SuffixColor", "Suffix Header Color", ref Settings.SuffixHeader_Color);
        ImGui.SameLine();
        ImGui.Text($"Suffix Header Color");

        DXT.ColorSelect.Draw($"RollColor", "Roll value Color", ref Settings.Roll_Color);
        ImGui.SameLine();
        ImGui.Text($"Mod Rolls Color");

        DXT.ColorSelect.Draw($"Tier1Color", "Tier 1 Color", ref Settings.Tier1Color_Color);
        ImGui.SameLine();
        ImGui.Text($"Tier 1 Color");

        DXT.ColorSelect.Draw($"Tier2Color", "Tier 2 Color", ref Settings.Tier2Color_Color);
        ImGui.SameLine();
        ImGui.Text($"Tier 2 Color");

        DXT.ColorSelect.Draw($"Tier3Color", "Tier 3 Color", ref Settings.Tier3Color_Color);
        ImGui.SameLine();
        ImGui.Text($"Tier 3 Color");

        DXT.ColorSelect.Draw($"LowTierColor", "Low Tier Color", ref Settings.LowTier_Color);
        ImGui.SameLine();
        ImGui.Text($"Low Tier Color");

       // DXT.ColorSelect.Draw($"ModTextColor", "Mod Text Color", ref Settings.ModText_Color);
       // ImGui.SameLine();
       // ImGui.Text($"Mod Text Color");


    }



}






