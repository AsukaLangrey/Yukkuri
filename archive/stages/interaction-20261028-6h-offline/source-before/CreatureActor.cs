using Godot;
using System;
using Yukkuri.Simulation;
using Expression = Yukkuri.Simulation.Expression;

public partial class CreatureActor : RigidBody3D
{
    public string CreatureId {get;private set;}="";
    public Expression DisplayedExpression {get;private set;}
    public string PoseTag {get;private set;}="idle";
    public Vector3 VisualMotionOffset=>_visual.Position;
    public float BodyScale {get;private set;}=1;
    private Node3D _visual=null!,_mouth=null!,_brows=null!,_tearRoot=null!,_blushRoot=null!;
    private MeshInstance3D _ring=null!,_leftEye=null!,_rightEye=null!,_leftBrow=null!,_rightBrow=null!,_bandage=null!;
    private Label3D _name=null!,_speech=null!;
    private float _phase,_reactionAge,_poseAge,_lastDownSpeed;
    private int _lastSerial=-1;
    private Vector3 _lastPosition;
    private bool _contactActive;
    private bool _hatAccessory;
    private ContactAction _contactAction;
    private float _contactForce;
    private Expression _lastMouth=(Expression)(-1);
    private readonly System.Collections.Generic.Dictionary<Expression,Node3D> _mouthShapes=new();
    public Action<string,double>? Impact;

    public void Setup(Creature c,int index,Font font)
    {
        CreatureId=c.Id;Name="Creature_"+c.Id;Mass=c.IsYoung?.65f:1;Freeze=true;FreezeMode=FreezeModeEnum.Kinematic;
        BodyScale=c.IsYoung?.73f:c.IsElder?1.03f:1;
        CollisionLayer=2;CollisionMask=3;LinearDamp=.6f;AngularDamp=10;LockRotation=true;ContactMonitor=true;MaxContactsReported=4;
        PhysicsMaterialOverride=new PhysicsMaterial{Friction=.8f,Bounce=.12f};
        AddChild(new CollisionShape3D{Shape=new SphereShape3D{Radius=.43f*BodyScale}});
        _visual=new Node3D();AddChild(_visual);
        if(Main.SimpleActors){SceneArt.Ball(_visual,Vector3.Zero,new(1,.9f,1),"f8e3c0");Position=new((float)c.X,.46f*BodyScale,(float)c.Z);return;}
        string hair=c.Body.SpeciesId=="marisa"?"d8ad60":"493330";
        bool marisa=c.Body.SpeciesId=="marisa"||c.Body.SpeciesId=="prototype-common"&&index%2!=0;
        _hatAccessory=marisa;
        if(marisa)hair="d8ad60";
        _skinContact=SceneArt.Ball(_visual,new(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ),
            new(ActorFaceSurface.RadiusX*2,ActorFaceSurface.RadiusY*2,ActorFaceSurface.RadiusZ*2),"ffeaca");
        _hairContact=new Node3D();_visual.AddChild(_hairContact);
        SceneArt.Ball(_hairContact,new(0,.20f,-.06f),new(1.17f,.71f,1.02f),hair);
        foreach(float x in new[]{-.48f,.48f})
            BuildHairLock(_hairContact,new(x,.30f,.08f),new(x,-.31f,.23f),.135f,.13f,hair,x*.05f);
        for(int i=0;i<5;i++)
        {
            float x=(i-2)*.18f;
            // Embed the fringe roots in the hair cap so an oblique view cannot reveal floating locks.
            BuildHairLock(_hairContact,new(x*.92f,.43f,.20f),new(x,.22f+(i%2)*.025f,.465f),.14f,.063f,hair,(i-2)*.012f);
        }
        BuildSurfaceEyes();
        _brows=new Node3D();_visual.AddChild(_brows);
        _leftBrow=FaceStroke(_brows,new(-.325f,.205f),new(-.145f,.205f),.018f,hair);
        _rightBrow=FaceStroke(_brows,new(.145f,.205f),new(.325f,.205f),.018f,hair);
        _blushRoot=new Node3D();_visual.AddChild(_blushRoot);
        foreach(float x in new[]{-.36f,.36f})
        {
            FaceOval(_blushRoot,new(x,-.10f),new(.19f,.075f),"e4a39b",.007f);
            for(int i=0;i<3;i++)FaceStroke(_blushRoot,new(x+(i-1)*.035f-.006f,-.12f),new(x+(i-1)*.035f+.006f,-.08f),.008f,"ce837e",.013f);
        }
        _mouth=new Node3D();_visual.AddChild(_mouth);
        _tearRoot=new Node3D();_visual.AddChild(_tearRoot);
        foreach(float x in new[]{-.26f,.26f})FaceOval(_tearRoot,new(x,-.11f),new(.048f,.16f),"94c9db",.012f);
        _tearRoot.Visible=false;
        _bandage=FaceStroke(_visual,new(.235f,-.20f),new(.405f,-.14f),.072f,"e7cf9c",.014f);_bandage.Visible=false;
        if(marisa)BuildHat();else BuildRibbon();
        _ring=SceneArt.Mesh(this,new TorusMesh{InnerRadius=.50f*BodyScale,OuterRadius=.54f*BodyScale,Rings=32,RingSegments=6},new(0,-.39f*BodyScale,0),"edbe68");
        _ring.Visible=false;
        if(Main.NameplatesEnabled){
        _name=new Label3D{Text=c.Name,Position=new(0,1.25f*BodyScale,0),Font=font,FontSize=38,PixelSize=.005f,
            Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=new Color("fff8df"),OutlineModulate=new Color("3c5145"),OutlineSize=9,NoDepthTest=true};
        AddChild(_name);
        _speech=new Label3D{Text="",Position=new(0,1.65f*BodyScale,0),Font=font,FontSize=34,PixelSize=.0045f,
            Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=new Color("fff3cf"),OutlineModulate=new Color("425044"),OutlineSize=10,NoDepthTest=true};
        AddChild(_speech);
        }
        Position=new((float)c.X,.46f*BodyScale,(float)c.Z);_lastPosition=Position;_phase=index*.8f;
        BuildMouth(Expression.Neutral);
        if(System.Array.Exists(OS.GetCmdlineUserArgs(),arg=>arg=="--merge-actor"))
            SceneArt.CombineStatic(_visual,_leftEye,_rightEye,_brows,_blushRoot,_mouth,_tearRoot,_bandage);
    }
    private MeshInstance3D Triangle(Node3D parent,Vector3 a,Vector3 b,Vector3 c,string color)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetNormal(Vector3.Back);surface.AddVertex(a);surface.AddVertex(b);surface.AddVertex(c);
        return SceneArt.Mesh(parent,surface.Commit(),Vector3.Zero,color);
    }
    private void BuildRibbon()
    {
        var ribbon=new Node3D{Position=new(0,.50f,.10f),RotationDegrees=new(-8,0,-7)};_visual.AddChild(ribbon);
        _accessoryRoot = ribbon;
        BuildClothBow(ribbon,"b84e51","ead6bc");
        BuildAccessoryWear(ribbon,false);
        foreach(float x in new[]{-.48f,.48f})
        {
            var tie=SceneArt.Box(_hairContact!,new(x,-.10f,.25f),new(.21f,.15f,.22f),"b84e4d");tie.RotationDegrees=new(0,0,x>0?10:-10);
            SceneArt.Box(tie,new(0,-.08f,.02f),new(.2f,.045f,.22f),"eee1c8");
        }
    }
    private void BuildHat()
    {
        var hat=new Node3D{Position=new(.02f,.48f,-.02f),RotationDegrees=new(-5,0,-6)};_visual.AddChild(hat);
        _accessoryRoot = hat;
        BuildSculptedHat(hat);
        BuildAccessoryWear(hat,true);
    }
    private void StrokeLine(Node3D parent,Vector3 a,Vector3 b,float radius,string color)
    {
        var delta=b-a;
        var line=SceneArt.Mesh(parent,new CylinderMesh{TopRadius=radius,BottomRadius=radius,Height=delta.Length(),RadialSegments=8},(a+b)*.5f,color);
        if(delta.Length()>.0001f)line.Quaternion=new Quaternion(Vector3.Up,delta.Normalized());
    }
    private void BuildMouth(Expression expression)
    {
        expression=expression switch
        {
            Expression.Calling or Expression.Frightened or Expression.Hurt=>Expression.Hurt,
            Expression.Delighted=>Expression.Delighted,
            Expression.Defiant or Expression.Angry=>Expression.Angry,
            Expression.Worried=>Expression.Worried,
            Expression.Exhausted or Expression.Sleeping=>Expression.Exhausted,
            Expression.Curious=>Expression.Curious,
            _=>Expression.Neutral
        };
        if(_lastMouth==expression)return;_lastMouth=expression;
        foreach(var shape in _mouthShapes.Values)shape.Visible=false;
        if(_mouthShapes.TryGetValue(expression,out var cached)){cached.Visible=true;return;}
        var mouthShape=new Node3D();_mouth.AddChild(mouthShape);_mouthShapes[expression]=mouthShape;
        if(expression is Expression.Calling or Expression.Frightened or Expression.Hurt or Expression.Delighted)
        {
            var size=expression==Expression.Delighted?new Vector3(.18f,.14f,.025f):new Vector3(.12f,.18f,.025f);
            FaceOval(mouthShape,new(0,-.178f),new(size.X,size.Y),"7e4543");
            if(expression==Expression.Delighted)FaceOval(mouthShape,new(0,-.211f),new(.115f,.045f),"dc8d85",.015f);
        }
        else if(expression is Expression.Defiant or Expression.Angry)
        {
            FaceStroke(mouthShape,new(-.09f,-.15f),new(0,-.172f),.022f,"8d5047");
            FaceStroke(mouthShape,new(0,-.172f),new(.10f,-.132f),.022f,"8d5047");
        }
        else if(expression==Expression.Curious)
            FaceOval(mouthShape,new(0,-.17f),new(.055f,.058f),"8d5047");
        else if(expression==Expression.Exhausted)
            FaceStroke(mouthShape,new(-.055f,-.17f),new(.055f,-.17f),.016f,"9b5c4d");
        else
        {
            for(int i=0;i<8;i++)
            {
                float x1=-.095f+i*.19f/8,x2=-.095f+(i+1)*.19f/8;
                float y1=(x1*x1/.009f-1)*.028f,y2=(x2*x2/.009f-1)*.028f;
                if(expression==Expression.Worried){y1=-y1;y2=-y2;}
                FaceStroke(mouthShape,new(x1,y1-.16f),new(x2,y2-.16f),.020f,"9b5c4d");
            }
        }
    }
    public void SetContactPose(ContactAction action,bool active,float force=0)
    {
        if(action!=_contactAction||active!=_contactActive)_poseAge=0;
        _contactAction=action;_contactActive=active;_contactForce=force;
    }
    public override void _PhysicsProcess(double delta)
    {
        if(Freeze){_lastDownSpeed=0;return;}
        if(GetContactCount()>0&&_lastDownSpeed>4)Impact?.Invoke(CreatureId,_lastDownSpeed);
        _lastDownSpeed=Mathf.Max(0,-LinearVelocity.Y);
    }
    public void Refresh(Creature c,bool selected,bool close,float delta,float force,bool suspended=false)
    {
        if(Main.SimpleActors){Freeze=!close||suspended;if(!close)Position=new((float)c.X,.46f*BodyScale,(float)c.Z);return;}
        _phase+=delta;_reactionAge+=delta;_poseAge+=delta;
        if(c.ReactionSerial!=_lastSerial){_lastSerial=c.ReactionSerial;_reactionAge=0;}
        DisplayedExpression=c.Expression;
        _ring.Visible=selected&&!close;
        if(Main.NameplatesEnabled){
        _name.Visible=selected&&!close;_name.Text=c.Name+(c.Condition==PhysicalCondition.Critical?" · 待救":"");
        _speech.Text=c.Speech;_speech.Visible=!close&&c.Speech.Length>0&&_reactionAge<6;
        }
        _bandage.Visible=c.Health<75||c.Condition==PhysicalCondition.Recovering;
        _tearRoot.Visible=c.Expression is Expression.Hurt or Expression.Frightened;
        _blushRoot.Visible=c.Expression is Expression.Content or Expression.Delighted or Expression.Shy;
        BuildMouth(c.Expression);
        float eyes=1,browLeft=0,browRight=0;
        if(c.Expression is Expression.Content or Expression.Sleeping)eyes=.12f;
        else if(c.Expression==Expression.Delighted)eyes=.20f;
        else if(c.Expression is Expression.Defiant or Expression.Angry){eyes=.65f;browLeft=-17;browRight=17;}
        else if(c.Expression is Expression.Hurt or Expression.Frightened or Expression.Shy or Expression.Worried){browLeft=17;browRight=-17;}
        else if(c.Expression==Expression.Exhausted){eyes=.42f;browLeft=9;browRight=-9;}
        if(Mathf.Sin(_phase*1.6f)>.994f&&c.Expression==Expression.Neutral)eyes=.1f;
        SetSurfaceEyes(eyes);
        SetSurfaceBrows(browLeft,browRight,c.Expression==Expression.Frightened?.018f:0);
        Vector3 visualScale=Vector3.One;Vector3 visualOffset=Vector3.Zero;
        if(!close)
        {
            Freeze=true;Position=Position.Lerp(new((float)c.X,.46f*BodyScale,(float)c.Z),Mathf.Min(1,delta*12));
            var direction=Position-_lastPosition;
            bool walking=direction.LengthSquared()>.00002f&&c.Condition==PhysicalCondition.Active;
            if(walking)_visual.Rotation=new(0,Mathf.LerpAngle(_visual.Rotation.Y,Mathf.Atan2(direction.X,direction.Z),delta*8),0);
            float bounce=walking?Mathf.Abs(Mathf.Sin(_phase*7))*.055f:Mathf.Sin(_phase*2)*.008f;
            visualOffset.Y=bounce;visualScale=new(1+bounce*.25f,1-bounce*.5f,1);
            PoseTag=walking?"hop":"idle";
        }
        else
        {
            Freeze=suspended;_visual.Rotation=Vector3.Zero;
            float squash=Mathf.Clamp(force/150,0,.18f);
            visualScale=new(1+squash,1-squash,1+squash*.4f);PoseTag="close";
        }
        if(_contactActive&&close)
        {
            float rhythm=Mathf.Sin(_poseAge*8);
            switch(_contactAction)
            {
                case ContactAction.Stroke:visualScale=new(1.03f, .97f-Mathf.Abs(rhythm)*.025f,1);_visual.RotationDegrees=new(0,0,rhythm*2);PoseTag="stroke";break;
                case ContactAction.Hug:visualScale=new(1.06f,.94f,1.04f);visualOffset.Y=.08f+Mathf.Sin(_poseAge*3)*.015f;PoseTag="hug";break;
                case ContactAction.Nuzzle:visualOffset.X=rhythm*.07f;_visual.RotationDegrees=new(0,rhythm*5,rhythm*7);PoseTag="nuzzle";break;
                case ContactAction.Discipline:visualScale=new(1+_contactForce*.12f,1-_contactForce*.16f,1);PoseTag="pressure";break;
                case ContactAction.Train:visualOffset.Y=Mathf.Abs(Mathf.Sin(_poseAge*4))*.08f;_visual.RotationDegrees=new(0,0,Mathf.Sin(_poseAge*4)*4);PoseTag="practice";break;
                case ContactAction.Treat:visualScale=new(1,1+Mathf.Sin(_poseAge*3)*.018f,1);PoseTag="nursing";break;
                default:PoseTag=_contactAction.ToString().ToLowerInvariant();break;
            }
        }
        else if(c.Expression is Expression.Hurt or Expression.Frightened)
        {visualOffset.X+=Mathf.Sin(_phase*35)*.012f;visualScale.Y*=.93f;PoseTag="tremble";}
        else if(c.Expression==Expression.Defiant)
        {_visual.RotationDegrees=new Vector3(-5,_visual.RotationDegrees.Y,Mathf.Sin(_phase*2)*2);PoseTag="defiant";}
        if(c.Condition!=PhysicalCondition.Active)
        {
            visualScale=new(1.13f,.63f,1.07f);SetSurfaceEyes(.08f);PoseTag=c.Condition.ToString().ToLowerInvariant();
            if(c.Condition==PhysicalCondition.Dead)
            {
                visualOffset=Vector3.Zero;
                _visual.RotationDegrees=new(0,_visual.RotationDegrees.Y,0);
            }
        }
        _visual.Scale=visualScale*BodyScale;_visual.Position=visualOffset*BodyScale;
        _lastPosition=Position;
    }
}
