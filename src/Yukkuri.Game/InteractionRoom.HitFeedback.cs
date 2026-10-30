using Godot;

public partial class InteractionRoom
{
    private Label? _hitFeedback;
    private string? _hitTarget;
    private ulong _lastHitUsec;
    private int _hitCount;
    private const double HitChainSeconds=1.5;

    private void RegisterHit(string id)
    {
        ulong now=Time.GetTicksUsec();
        _hitCount=id==_hitTarget&&(now-_lastHitUsec)/1_000_000.0<=HitChainSeconds?_hitCount+1:1;
        _hitTarget=id;_lastHitUsec=now;
        // Count confirmed effects only. Misses, blocked contact and refusals never create hits.
        UpdateHitFeedback();
    }

    private void UpdateHitFeedback()
    {
        if(_hitFeedback==null)
        {
            _hitFeedback=new Label{MouseFilter=Control.MouseFilterEnum.Ignore,Visible=false};
            _hitFeedback.AddThemeFontSizeOverride("font_size",23);
            _hitFeedback.AddThemeColorOverride("font_color",new Color("a65b42"));
            _hitFeedback.AddThemeStyleboxOverride("normal",RoomStyle("fff3dfeb",8,7));
            _ui.AddChild(_hitFeedback);
        }
        double age=(Time.GetTicksUsec()-_lastHitUsec)/1_000_000.0;
        bool visible=!_modalOpen&&_windowFocused&&_hitTarget!=null&&_actors.TryGetValue(_hitTarget,out var actor)&&actor.Visible&&age<HitChainSeconds;
        _hitFeedback.Visible=visible;
        if(!visible)return;
        var target=_actors[_hitTarget!];
        Vector2 screen=_camera.UnprojectPosition(target.ContactPoint(Yukkuri.Simulation.BodyPart.Head));
        if(!IsScenePoint(screen)){_hitFeedback.Visible=false;return;}
        _hitFeedback.Text=$"{_hitCount} hits";
        _hitFeedback.Position=screen+new Vector2(28,-34-(float)System.Math.Min(age,.3)*25);
        float fade=Mathf.Clamp((float)((HitChainSeconds-age)/.35),0,1);
        _hitFeedback.Modulate=new Color(1,1,1,fade);
    }
}
