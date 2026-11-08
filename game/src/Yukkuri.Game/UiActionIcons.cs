using Godot;
using System.Collections.Generic;

// Shared vector icons: readable labels stay visible; no tooltip popup is needed.
internal static class UiActionIcons
{
    private static readonly Dictionary<string,Texture2D> Textures=new();
    public static void Apply(Button button,string name)
    {
        if(!Textures.TryGetValue(name,out var texture))
            Textures[name]=texture=GD.Load<Texture2D>($"res://Assets/Icons/Actions/{name}.svg");
        button.Icon=texture;
        button.AddThemeConstantOverride("icon_max_width",18);
        button.AddThemeConstantOverride("h_separation",6);
        Tint(button,false);
    }
    public static void Tint(Button button,bool active)
    {
        var color=new Color(active?"fff7df":"405247");
        button.AddThemeColorOverride("icon_normal_color",color);
        button.AddThemeColorOverride("icon_hover_color",color);
        button.AddThemeColorOverride("icon_pressed_color",new Color("405247"));
        button.AddThemeColorOverride("icon_disabled_color",new Color("919889"));
    }
}
