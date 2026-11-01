using Godot;
using System.Linq;

public partial class InteractionRoom
{
    private Label? _hitFeedback;
    private string? _hitTarget;
    private const double HitChainSeconds=1.5;

    private void RegisterHit(string id)
    {
        _hitTarget=id;
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
        var life=Sim.State.Creatures.FirstOrDefault(c=>c.Id==_hitTarget)?.Interaction;
        // The simulation owns each target's chain and clock. Pausing or switching
        // targets must not create a second, contradictory counter in the view.
        double remaining=life?.HitRemaining??0;
        int count=System.Math.Clamp(life?.HitCount??0,0,999);
        double age=System.Math.Clamp(HitChainSeconds-remaining,0,HitChainSeconds);
        bool visible=!_modalOpen&&_windowFocused&&_hitTarget!=null&&_actors.TryGetValue(_hitTarget,out var actor)&&actor.Visible&&remaining>0&&count>0;
        _hitFeedback.Visible=visible;
        if(!visible)return;
        var target=_actors[_hitTarget!];
        Vector2 screen=_camera.UnprojectPosition(target.ContactPoint(Yukkuri.Simulation.BodyPart.Head));
        if(!IsScenePoint(screen)){_hitFeedback.Visible=false;return;}
        _hitFeedback.Text=$"{count} hits";
        _hitFeedback.Position=screen+new Vector2(28,-34-(float)System.Math.Min(age,.3)*25);
        float fade=Mathf.Clamp((float)(remaining/.35),0,1);
        _hitFeedback.Modulate=new Color(1,1,1,fade);
    }
}
