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
        if(marisa)hair="d8ad60";
        SceneArt.Ball(_visual,Vector3.Zero,new(1.14f,.91f,.96f),"f8e3c0");
        SceneArt.Ball(_visual,new(0,.20f,-.06f),new(1.17f,.71f,1.02f),hair);
        SceneArt.Ball(_visual,new(0,-.01f,.24f),new(1.01f,.73f,.61f),"ffeaca");
        foreach(float x in new[]{-.48f,.48f})
            SceneArt.Ball(_visual,new(x,.02f,.14f),new(.22f,.65f,.55f),hair);
        for(int i=0;i<5;i++)
        {
            float x=(i-2)*.18f;
            var fringe=SceneArt.Ball(_visual,new(x,.31f+(i%2)*.015f,.405f),new(.25f,.32f,.19f),hair);
            fringe.RotationDegrees=new(0,0,(i-2)*-9);
        }
        _leftEye=SceneArt.Ball(_visual,new(-.23f,.04f,.541f),new(.16f,.24f,.038f),"352b2a");
        _rightEye=SceneArt.Ball(_visual,new(.23f,.04f,.541f),new(.16f,.24f,.038f),"352b2a");
        foreach(var eye in new[]{_leftEye,_rightEye})
        {
            SceneArt.Ball(eye,new(-.14f,.21f,.45f),new(.32f,.27f,.5f),"ffffff");
            SceneArt.Ball(eye,new(.12f,-.23f,.45f),new(.17f,.12f,.45f),"d2b7a0");
        }
        _brows=new Node3D();_visual.AddChild(_brows);
        _leftBrow=SceneArt.Box(_brows,new(-.235f,.218f,.535f),new(.20f,.022f,.025f),hair);
        _rightBrow=SceneArt.Box(_brows,new(.235f,.218f,.535f),new(.20f,.022f,.025f),hair);
        _blushRoot=new Node3D();_visual.AddChild(_blushRoot);
        foreach(float x in new[]{-.36f,.36f})
        {
            SceneArt.Ball(_blushRoot,new(x,-.10f,.512f),new(.19f,.075f,.022f),"e4a39b");
            for(int i=0;i<3;i++){var line=SceneArt.Box(_blushRoot,new(x+(i-1)*.035f,-.10f,.532f),new(.009f,.043f,.008f),"ce837e");line.RotationDegrees=new(0,0,-18);}
        }
        _mouth=new Node3D{Position=new(0,-.16f,.56f)};_visual.AddChild(_mouth);
        _tearRoot=new Node3D();_visual.AddChild(_tearRoot);
        foreach(float x in new[]{-.26f,.26f})SceneArt.Ball(_tearRoot,new(x,-.11f,.555f),new(.048f,.16f,.025f),"94c9db");
        _tearRoot.Visible=false;
        _bandage=SceneArt.Box(_visual,new(.34f,-.17f,.526f),new(.24f,.085f,.015f),"e7cf9c");_bandage.RotationDegrees=new(0,0,-20);_bandage.Visible=false;
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
        foreach(float sign in new[]{-1f,1f})
        {
            Triangle(ribbon,new(sign*.04f,0,.01f),new(sign*.45f,.20f,.01f),new(sign*.42f,-.17f,.01f),"a83d43");
            Triangle(ribbon,new(sign*.13f,0,.025f),new(sign*.40f,.14f,.025f),new(sign*.38f,-.11f,.025f),"e2c9af");
            Triangle(ribbon,new(sign*.16f,0,.035f),new(sign*.36f,.10f,.035f),new(sign*.34f,-.07f,.035f),"bc4b4d");
            var tail=SceneArt.Box(ribbon,new(sign*.10f,-.15f,-.015f),new(.14f,.35f,.065f),"b5494c");tail.RotationDegrees=new(0,0,sign*15);
        }
        SceneArt.Ball(ribbon,Vector3.Zero,new(.18f,.18f,.13f),"b74d50");
        foreach(float x in new[]{-.48f,.48f})
        {
            var tie=SceneArt.Box(_visual,new(x,-.10f,.25f),new(.21f,.15f,.22f),"b84e4d");tie.RotationDegrees=new(0,0,x>0?10:-10);
            SceneArt.Box(tie,new(0,-.08f,.02f),new(.2f,.045f,.22f),"eee1c8");
        }
    }
    private void BuildHat()
    {
        var hat=new Node3D{Position=new(.02f,.39f,-.02f),RotationDegrees=new(-5,0,-6)};_visual.AddChild(hat);
        SceneArt.Mesh(hat,new CylinderMesh{TopRadius=.66f,BottomRadius=.63f,Height=.065f,RadialSegments=32},Vector3.Zero,"3b3941");
        SceneArt.Mesh(hat,new CylinderMesh{TopRadius=.035f,BottomRadius=.40f,Height=.65f,RadialSegments=24},new(.035f,.32f,0),"41404a");
        SceneArt.Mesh(hat,new CylinderMesh{TopRadius=.34f,BottomRadius=.39f,Height=.13f,RadialSegments=24},new(0,.08f,0),"e9dfc5");
        var bow=new Node3D{Position=new(.29f,.10f,.31f),RotationDegrees=new(0,0,12)};hat.AddChild(bow);
        Triangle(bow,new(0,0,0),new(-.18f,.12f,0),new(-.17f,-.11f,0),"fff1d4");
        Triangle(bow,new(0,0,0),new(.18f,.12f,0),new(.17f,-.11f,0),"fff1d4");
        SceneArt.Ball(bow,Vector3.Zero,new(.10f,.10f,.06f),"f5e6c8");
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
            _=>Expression.Neutral
        };
        if(_lastMouth==expression)return;_lastMouth=expression;
        foreach(var shape in _mouthShapes.Values)shape.Visible=false;
        if(_mouthShapes.TryGetValue(expression,out var cached)){cached.Visible=true;return;}
        var mouthShape=new Node3D();_mouth.AddChild(mouthShape);_mouthShapes[expression]=mouthShape;
        if(expression is Expression.Calling or Expression.Frightened or Expression.Hurt or Expression.Delighted)
        {
            var size=expression==Expression.Delighted?new Vector3(.18f,.14f,.025f):new Vector3(.12f,.18f,.025f);
            SceneArt.Ball(mouthShape,new(0,-.018f,0),size,"7e4543");
            if(expression==Expression.Delighted)SceneArt.Ball(mouthShape,new(0,-.051f,.015f),new(.115f,.045f,.012f),"dc8d85");
        }
        else if(expression is Expression.Defiant or Expression.Angry)
        {
            StrokeLine(mouthShape,new(-.09f,.01f,0),new(0,-.012f,0),.013f,"8d5047");
            StrokeLine(mouthShape,new(0,-.012f,0),new(.10f,.028f,0),.013f,"8d5047");
        }
        else
        {
            for(int i=0;i<8;i++)
            {
                float x1=-.095f+i*.19f/8,x2=-.095f+(i+1)*.19f/8;
                float y1=(x1*x1/.009f-1)*.028f,y2=(x2*x2/.009f-1)*.028f;
                StrokeLine(mouthShape,new(x1,y1,0),new(x2,y2,0),.012f,"9b5c4d");
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
        else if(c.Expression is Expression.Hurt or Expression.Frightened or Expression.Shy){browLeft=17;browRight=-17;}
        if(Mathf.Sin(_phase*1.6f)>.994f&&c.Expression==Expression.Neutral)eyes=.1f;
        _leftEye.Scale=new(.16f,.24f*eyes,.038f);_rightEye.Scale=_leftEye.Scale;
        _leftBrow.RotationDegrees=new(0,0,browLeft);_rightBrow.RotationDegrees=new(0,0,browRight);
        _brows.Position=new(0,c.Expression==Expression.Frightened?.03f:0,0);
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
            visualScale=new(1.13f,.63f,1.07f);_leftEye.Scale=new(.16f,.02f,.038f);_rightEye.Scale=_leftEye.Scale;PoseTag=c.Condition.ToString().ToLowerInvariant();
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
