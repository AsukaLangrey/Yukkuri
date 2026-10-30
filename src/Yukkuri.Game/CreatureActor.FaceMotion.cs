using Godot;
using System.Collections.Generic;
using Yukkuri.Simulation;
using Expression=Yukkuri.Simulation.Expression;

public partial class CreatureActor
{
    private readonly Dictionary<Expression,float> _mouthWeights=new();
    private readonly Dictionary<Expression,List<MeshInstance3D>> _mouthFadeMeshes=new();
    private readonly List<MeshInstance3D> _blushFadeMeshes=new();
    private bool _faceReady;
    private float _smoothEye=1,_smoothEyeArc,_smoothBrowLeft,_smoothBrowRight,_smoothBrowHeight,_blushAmount;

    private void PrepareMouthFade(Expression expression,Node3D root)
    {
        var meshes=new List<MeshInstance3D>();
        foreach(var child in root.GetChildren())if(child is MeshInstance3D mesh)
        {SceneArt.EnableSurfaceFade(mesh);meshes.Add(mesh);}
        _mouthFadeMeshes[expression]=meshes;
        float weight=_mouthWeights.Count==0?1:0;_mouthWeights[expression]=weight;
        foreach(var mesh in meshes)SceneArt.SetSurfaceOpacity(mesh,weight);
        root.Visible=weight>0;
    }

    private void PresentFaceTransition(Creature c,Expression expression,float eyes,EyeCurve curve,float left,float right,float dt)
    {
        CompanyEyes(expression,ref eyes,ref curve,ref left,ref right);
        WaterConcernEyes(expression,ref eyes,ref left,ref right);
        HungerFeedingEyes(expression,ref eyes,ref curve,ref left,ref right);
        // Food can be welcome while the body is still afraid. Keep the eyes/brows wary
        // independently of the chewing mouth and the latest short-lived response.
        float caution=Mathf.Clamp(Mathf.Max((float)(c.Fear-20)/65,(float)c.Interaction.Emotion.Alarm/100*.85f),0,1);
        if(caution>.001f&&expression is Expression.Content or Expression.Delighted or Expression.Curious or Expression.Shy)
        {
            eyes=Mathf.Lerp(eyes,.94f,caution);left=Mathf.Lerp(left,14,caution);right=Mathf.Lerp(right,-14,caution);
            if(caution>.5f)curve=EyeCurve.Open;
        }
        float height=expression==Expression.Frightened?ActorBrowSurface.FrightHeight:0;
        float arc=ActorEyeSurface.Arc(curve);
        bool urgent=expression is Expression.Hurt or Expression.Frightened;
        bool initial=!_faceReady;
        float amount=initial?1:dt>0?1-Mathf.Exp(-dt*(urgent?32:15)):0;
        if(!_faceReady)
        {_smoothEye=eyes;_smoothEyeArc=arc;_smoothBrowLeft=left;_smoothBrowRight=right;_smoothBrowHeight=height;_faceReady=true;}
        // An invalidated threat must not leave its added eye/brow pressure frozen
        // on the face during a pause. Ordinary mouth/tears still use their clocks.
        float eyeAmount=_hungerCueInvalidated?1:amount;_hungerCueInvalidated=false;
        _smoothEye=Mathf.Lerp(_smoothEye,eyes,eyeAmount);
        _smoothEyeArc=Mathf.Lerp(_smoothEyeArc,arc,eyeAmount);
        _smoothBrowLeft=Mathf.Lerp(_smoothBrowLeft,left,eyeAmount);_smoothBrowRight=Mathf.Lerp(_smoothBrowRight,right,eyeAmount);
        _smoothBrowHeight=Mathf.Lerp(_smoothBrowHeight,height,eyeAmount);
        // Quantize the authored surfaces so a long session cannot cache every float blend.
        SetSurfaceEyes(Mathf.Floor(_smoothEye*48)/48,Mathf.Round(_smoothEyeArc/.0015f)*.0015f);
        SetSurfaceBrows(Mathf.Round(_smoothBrowLeft/2)*2,Mathf.Round(_smoothBrowRight/2)*2,Mathf.Round(_smoothBrowHeight*200)/200);
        foreach(var pair in _mouthShapes)
        {
            float target=pair.Key==_lastMouth?1:0;
            float weight=Mathf.Lerp(_mouthWeights[pair.Key],target,amount);
            if(Mathf.Abs(weight-target)<.005f)weight=target;
            _mouthWeights[pair.Key]=weight;pair.Value.Visible=weight>.001f;
            foreach(var mesh in _mouthFadeMeshes[pair.Key])SceneArt.SetSurfaceOpacity(mesh,weight);
        }
        if(_blushFadeMeshes.Count==0)
            foreach(var child in _blushRoot.GetChildren())if(child is MeshInstance3D mesh)
            {SceneArt.EnableSurfaceFade(mesh);_blushFadeMeshes.Add(mesh);}
        float blush=expression is Expression.Content or Expression.Delighted or Expression.Shy?1-caution:0;
        if(c.Condition==PhysicalCondition.Dead)blush=0;
        _blushAmount=Mathf.Lerp(_blushAmount,blush,initial?1:dt>0?1-Mathf.Exp(-dt*8):0);
        _blushRoot.Visible=_blushAmount>.005f;
        foreach(var mesh in _blushFadeMeshes)SceneArt.SetSurfaceOpacity(mesh,_blushAmount);
    }
}
