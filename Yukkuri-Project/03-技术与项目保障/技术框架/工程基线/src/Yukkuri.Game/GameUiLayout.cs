using Godot;

// One logical coordinate system for panels, canvas input and transparent widgets.
internal static class GameUiLayout
{
    public const float Margin=14,Gap=12,Top=88,LeftWidth=280,RightWidth=380;
    public static readonly Vector2I MinimumWindow=new(1440,900);
    public static void Configure(Window window)=>window.MinSize=MinimumWindow;
    public static Rect2 Canvas(Control root)=>new(LeftWidth+Margin+Gap,Top,
        root.Size.X-LeftWidth-RightWidth-2*(Margin+Gap),root.Size.Y-Top-Margin);
    public static void TopBar(Control panel)
    {
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        panel.OffsetLeft=Margin;panel.OffsetRight=-Margin;panel.OffsetTop=12;panel.OffsetBottom=76;
    }
    public static void Left(Control panel)
    {
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide);
        panel.OffsetLeft=Margin;panel.OffsetRight=Margin+LeftWidth;panel.OffsetTop=Top;panel.OffsetBottom=-Margin;
    }
    public static void Right(Control panel)
    {
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.RightWide);
        panel.OffsetLeft=-Margin-RightWidth;panel.OffsetRight=-Margin;panel.OffsetTop=Top;panel.OffsetBottom=-Margin;
    }
    public static void CanvasWidget(Control root,Control widget,bool bottom,float height)
    {
        var area=Canvas(root);float width=Mathf.Min(520,area.Size.X-32);
        widget.Position=new(area.Position.X+(area.Size.X-width)/2,bottom?area.End.Y-height-12:area.Position.Y+12);
        widget.Size=new(width,height);
    }
    public static bool Hit(Control control,Vector2 point)=>control.IsVisibleInTree()&&control.GetGlobalRect().HasPoint(point);
    public static void CenterCamera(Camera3D camera,Control root)
    {
        var size=root.Size;var center=Canvas(root).GetCenter();
        float units=camera.Size/Mathf.Max(1,camera.KeepAspect==Camera3D.KeepAspectEnum.Height?size.Y:size.X);
        camera.HOffset=(size.X*.5f-center.X)*units;
        camera.VOffset=(center.Y-size.Y*.5f)*units;
    }
}
