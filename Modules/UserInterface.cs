using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ImGuiNET;
using static System.Runtime.InteropServices.JavaScript.JSType;
using SDColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;

namespace EnhancedTooltip;

public sealed class UserInterface : PluginModule {
    public UserInterface(Plugin plugin) : base(plugin) { }
    public string CapturedNames = "";

    private float _buttonWidth = 70;
    public void Draw() {
        DXT.Button.Draw("ShowDBug", ref Settings.DXT.DBug.ShowToolbar, new DXT.Button.Options {
            Label = "DBug",
            Width = 100,
            Height = 22,
        });


        if (DXT.CollapsingHeader($"Tooltip Colors", ref Settings.ColorSetttingsOpen)) {
            ImGui.Indent();

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

            DXT.ColorSelect.Draw("Tier2Color", "Tier 2 Color", ref Settings.Tier2Color_Color);
            ImGui.SameLine();
            ImGui.Text("Tier 2 Color");

            DXT.ColorSelect.Draw("Tier3Color", "Tier 3 Color", ref Settings.Tier3Color_Color);
            ImGui.SameLine();
            ImGui.Text("Tier 3 Color");

            DXT.ColorSelect.Draw("LowTierColor", "Low Tier Color", ref Settings.LowTier_Color);
            ImGui.SameLine();
            ImGui.Text("Low Tier Color");

            DXT.Checkbox.Draw("##TierLevelColor", "Show Tier Level", ref Settings.ShowTierLevel);
            ImGui.SameLine();
            DXT.ColorSelect.Draw($"TierLevelColor", "Tier Level Color", ref Settings.TierLevel_Color);
            ImGui.SameLine();
            ImGui.Text($"Tier Level");

            ImGui.Unindent();
        }

        if (DXT.CollapsingHeader($"Mod Colors", ref Settings.ModColorSetttingsOpen)) {
            ImGui.Indent();
            Settings.DumpItemNames_Hotkey.DrawPickerButton($"Hotkey: {Settings.DumpItemNames_Hotkey.Value}");
            if(ImGui.IsItemHovered()) {
                DXT.Tooltip.Draw("Set Hotkey for hovered item name dump");
            }
            ImGui.SameLine();
            ImGui.Text("Captured Hover Item Mod Names");

            ImGui.InputTextMultiline("##CaptureML", ref CapturedNames, 10000, new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 100), ImGuiInputTextFlags.ReadOnly);

            for (int i = 0; i < Settings.CustomModColors.Count; i++) { DrawCustomMod(Settings.CustomModColors[i], i); }
            if (DXT.Button.Draw("AddMod", new() { Label = "Add Mod", Width = (int)_buttonWidth })) {
                Settings.CustomModColors.Add(new ModCustomColorSettings {
                    ModName = "Mod Name",
                    Color = Color.FromArgb(224, 64, 251)
                });
            }

            ImGui.Unindent();
        }






    }

    private void DrawCustomMod(ModCustomColorSettings modSettings, int index) {
        ImGui.PushID($"CustomMod_{index}");
        float spacing = ImGui.GetStyle().ItemSpacing.X;

        DXT.ColorSelect.Draw("ModColor", "Mod Text Color", ref modSettings.Color);
        ImGui.SameLine();
        string[] tierOptions = { "Any", "T1", "T2", "T3", "T4", "T5", "T6" };
        DXT.Select.Draw("ModTier", ref modSettings.SelectedTier, new() { Width = 60, Tooltip = DXT.Tooltip.BasicOptions("Mod Tier threshold"), Items = tierOptions.ToList() });
        ImGui.SameLine();
        DXT.Input.Draw("ModName", ref modSettings.ModName, new() { Width = (int)(ImGui.GetContentRegionAvail().X - spacing - spacing - _buttonWidth - spacing), Tooltip = DXT.Tooltip.BasicOptions("Name of Mod to color") } );
        ImGui.SameLine();
        if (DXT.Button.Draw("RemoveMod", new() { Label = "Remove", Width = (int)_buttonWidth, Color = DXTC.Colors.ControlRed } )) {
            Settings.CustomModColors.RemoveAt(index);
        }
    }




}






