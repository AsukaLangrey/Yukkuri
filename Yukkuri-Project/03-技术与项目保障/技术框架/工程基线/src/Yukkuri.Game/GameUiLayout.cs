using Godot;

// One logical coordinate system for panels, canvas input and transparent widgets.
internal static class GameUiLayout
{
    public const float Margin=14,Gap=12,Top=88,LeftWidth=280,RightWidth=266;
    public static readonly Vector2I MinimumWindow=new(1440,900);
    public static void Configure(Window window)=>window.MinSize=MinimumWindow;
    private static float ContentTop(Control root)=>root.HasMeta("no_top_bar")?Margin:root.HasMeta("compact_top_bar")?64:Top;
    public const float CollapsedLeftWidth=52;
    public const float SingleLeftWidth=180;
    private static float CurrentLeftWidth(Control root)=>root.HasMeta("left_sidebar_collapsed")&&root.GetMeta("left_sidebar_collapsed").AsBool()?CollapsedLeftWidth:
        root.HasMeta("left_sidebar_single")&&root.GetMeta("left_sidebar_single").AsBool()?SingleLeftWidth:LeftWidth;
    public static float CurrentRightWidth(Control root)=>root.HasMeta("right_sidebar_hidden")&&root.GetMeta("right_sidebar_hidden").AsBool()?0:
        root.HasMeta("right_sidebar_width")?(float)root.GetMeta("right_sidebar_width").AsDouble():RightWidth;
    public static Rect2 Canvas(Control root)=>new(CurrentLeftWidth(root)+Margin+Gap,ContentTop(root),
        root.Size.X-CurrentLeftWidth(root)-CurrentRightWidth(root)-2*(Margin+Gap),root.Size.Y-ContentTop(root)-Margin);
    public static void TopBar(Control panel,bool compact=false)
    {
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        panel.OffsetLeft=Margin;panel.OffsetRight=-Margin;panel.OffsetTop=8;panel.OffsetBottom=compact?56:76;
    }
    public static void Left(Control panel,bool collapsed=false)
    {
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide);
        panel.OffsetLeft=Margin;panel.OffsetRight=Margin+CurrentLeftWidth((Control)panel.GetParent());panel.OffsetTop=ContentTop((Control)panel.GetParent());panel.OffsetBottom=-Margin;
    }
    public static void Right(Control panel)
    {
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.RightWide);
        panel.OffsetLeft=-Margin-CurrentRightWidth((Control)panel.GetParent());panel.OffsetRight=-Margin;panel.OffsetTop=ContentTop((Control)panel.GetParent());panel.OffsetBottom=-Margin;
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
