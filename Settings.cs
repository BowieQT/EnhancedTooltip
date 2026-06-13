using DieselExileTools.Common;
using DieselExileTools.ExileCore2;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using System.Security.Cryptography.X509Certificates;
using SColor = System.Drawing.Color;
using SVector4 = System.Numerics.Vector4;

namespace EnhancedTooltip;

public class ModCustomColorSettings {
    public string ModName;
    public int SelectedTier;
    public Color Color;
}

public sealed class Settings : ISettings {
    public ToggleNode Enable { get; set; } = new(true);

    public DXTSettings DXT { get; set; } = new();
    public bool Debug = false;

    public bool ShowTierLevel = true;

    public bool ColorSetttingsOpen = true;
    public bool ModColorSetttingsOpen = true;


    public SColor TooltipBG_Color = SColor.FromArgb(200, 0, 0, 0);
    public SColor TooltipBorder_Color = SColor.FromArgb(0, 0, 0); 

    public SColor PrefixHeader_Color = SColor.FromArgb(84, 110, 122);
    public SColor SuffixHeader_Color = SColor.FromArgb(84, 110, 122);

    public SColor DefaultText_Color = SColor.FromArgb(207, 216, 220);    

    public SColor Roll_Color = SColor.FromArgb(0, 176, 255);

    public SColor Tier1Color_Color = SColor.FromArgb(255, 23, 68);
    public SColor Tier2Color_Color = SColor.FromArgb(255, 145, 0);
    public SColor Tier3Color_Color = SColor.FromArgb(255, 234, 0);
    public SColor TierLevel_Color = SColor.FromArgb(55, 71, 79);


    public SColor LowTier_Color = SColor.FromArgb(176, 190, 197);

    public SColor ModText_Color = SColor.FromArgb(0, 0, 0);

    public SColor Desecrated_Color = SColor.FromArgb(56, 142, 60);
    public SColor Essence_Color = SColor.FromArgb(77, 208, 225);
    public SColor Crafted_Color = SColor.FromArgb(128, 222, 234);


    public HotkeyNodeV2 DumpItemNames_Hotkey { get; set; } = new HotkeyNodeV2(Keys.F8);
    public HotkeyNodeV2 DumpTooltip_Hotkey { get; set; } = new HotkeyNodeV2(Keys.F8);
    public HotkeyNodeV2 DumpMods_Hotkey { get; set; } = new HotkeyNodeV2(Keys.F8);




    public List<ModCustomColorSettings> CustomModColors { get; set; } = new();




}
