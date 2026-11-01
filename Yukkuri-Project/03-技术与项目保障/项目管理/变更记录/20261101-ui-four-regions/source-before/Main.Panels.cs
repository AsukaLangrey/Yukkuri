using Godot;

public partial class Main
{
    private PanelContainer CreateGameplayDock()
    {
        var panel=new PanelContainer{Visible=false,AnchorLeft=1,AnchorRight=1,AnchorBottom=1,
            OffsetLeft=-442,OffsetRight=-22,OffsetTop=96,OffsetBottom=-116};
        panel.AddThemeStyleboxOverride("panel",PanelStyle("faf5e9"));_uiRoot.AddChild(panel);
        return panel;
    }
    private void ShowGameplayDock(PanelContainer panel)
    {
        if(_settingsPanel!=null&&_settingsPanel.Visible)CloseSettings();
        foreach(var other in new[]{_welcome,_diaryPanel,_helpPanel})
            if(other!=null)other.Visible=other==panel;
        panel.Visible=true;
        CancelGesture();_held=null;_foodInHand=false;_mapDragging=false;_force=0;
        UpdateUi();
    }
    private bool CloseGameplayDock()
    {
        foreach(var panel in new[]{_helpPanel,_diaryPanel,_welcome})
            if(panel!=null&&panel.Visible){panel.Visible=false;UpdateUi();return true;}
        return false;
    }
    private void HideGameplayDocks()
    {
        foreach(var panel in new[]{_welcome,_diaryPanel,_helpPanel})if(panel!=null)panel.Visible=false;
    }
}
