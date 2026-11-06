using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Invoked only by the existing guarded, opt-in headless Poke audit.
    private async Task RunPokePropAudit(Action<string,bool,object> check)
    {
        void Need(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        void Button(Node root,string label)
        {
            var button=root.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==label&&b.IsVisibleInTree());
            Need(!button.Disabled,"disabled "+label);button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        void Press(Vector2 p)=>_UnhandledInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=p,GlobalPosition=p});
        void Release(Vector2 p)=>_Input(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=p,GlobalPosition=p});
        bool BallDistance(RoomObject ball,Vector2 point,out float distance)
        {
            var origin=_camera.ProjectRayOrigin(point);var direction=_camera.ProjectRayNormal(point);
            return InteractionPropContact.RayHit(ball.Kind,new((float)ball.X,(float)ball.Height,(float)ball.Z),
                new(origin.X,origin.Y,origin.Z),new(direction.X,direction.Y,direction.Z),out distance,RoomBallEnvironment.CenterHeight(Sim.State,ball));
        }
        object RayAt(RoomObject ball,Vector2 point)
        {
            var body=ContactAt(point);bool intersects=BallDistance(ball,point,out float distance);var foreground=ObjectAt(point);
            return new{point=new[]{point.X,point.Y},bodyId=body?.Id,bodyPart=body?.Part.ToString(),bodyDistance=body?.Distance,
                bodyBlocked=body?.Blocked,bodyKind=body?.TargetKind.ToString(),ballIntersected=intersects,
                ballDistance=intersects?(float?)distance:null,foregroundObjectId=foreground?.Id};
        }
        Vector2 FindHead(string id)
        {
            var center=_camera.UnprojectPosition(_actors[id].ContactPoint(BodyPart.Head));
            for(int radius=0;radius<=100;radius+=2)for(int x=-radius;x<=radius;x+=2)for(int y=-radius;y<=radius;y+=2)
            {
                if(Math.Max(Math.Abs(x),Math.Abs(y))!=radius)continue;var point=center+new Vector2(x,y);
                if(IsScenePoint(point)&&ContactAt(point) is {Blocked:false,TargetKind:InteractionTargetKind.Body,Part:BodyPart.Head} hit&&hit.Id==id&&ObjectAt(point)==null)return point;
            }
            throw new InvalidOperationException("No exposed head in bounded 100px search");
        }
        Vector2? FindOverlap(string id,RoomObject ball,bool foreground)
        {
            var center=_camera.UnprojectPosition(new Vector3((float)ball.X,RoomBallEnvironment.CenterHeight(Sim.State,ball),(float)ball.Z));
            for(int y=-30;y<=30;y++)for(int x=-30;x<=30;x++)
            {
                var point=center+new Vector2(x,y);
                if(!IsScenePoint(point)||ContactAt(point) is not {Blocked:false,TargetKind:InteractionTargetKind.Body} hit||hit.Id!=id||
                    !BallDistance(ball,point,out float distance))continue;
                if(foreground?distance<hit.Distance&&ObjectAt(point)?.Id==ball.Id:distance>hit.Distance&&ObjectAt(point)==null)return point;
            }
            return null;
        }
        object StateOf(Creature c)=>new{c.Health,c.Pain,c.Fear,c.Trust,c.Resentment,c.ReactionSerial,c.LastPlayerTouchAt,c.Interaction.LastContact,pose=c.Interaction.ReactionPose};
        (double health,double pain,double fear,double trust,double resentment,int serial) Fingerprint(Creature c)=>(c.Health,c.Pain,c.Fear,c.Trust,c.Resentment,c.ReactionSerial);
        async Task<(Creature c,RoomObject ball,Vector2 clear,Vector2 covered)> Setup()
        {
            CancelGesture();Sim=WorldSimulation.CreateCourtyardEconomy();ResetRoomUiSession();BuildActors();DisarmTool();
            _windowFocused=true;_recovered=false;_yardFocus=Vector3.Zero;_zoom=16;_autoClock=-10000;
            UpdateRoomCamera(1);_Process(0);RefreshRoomUi();await RenderFrames(3);
            Need(!Sim.State.Settings.Pause&&Sim.State.Mode==PlayMode.Management,"ordinary unpaused yard required");
            var c=Sim.Selected!;Need(!c.IsYoung&&c.Condition==PhysicalCondition.Active&&c.Interaction.Restraint==RestraintKind.None,"ordinary adult required");
            int money=Sim.State.Money,stock=Sim.ShopQuantity("ball");
            Need(Sim.BuyShopProduct("ball")&&Sim.State.Money==money-12&&Sim.ShopQuantity("ball")==stock+1,"ordinary 12-yuan shop purchase failed");
            Button(_groupList,"玩具");Button(_choiceList,"软球");await RenderFrames(2);
            var ground=new Vector3((float)c.X,0,(float)c.Z+.82f);var point=_camera.UnprojectPosition(ground);
            Need(IsScenePoint(point)&&ContactAt(point)==null&&ObjectAt(point)==null,"placement ray must be empty ground");
            Press(point);Release(point);_Process(0);await RenderFrames(2);
            var ball=Sim.State.RoomObjects.SingleOrDefault(o=>o.Kind==RoomObjectKind.Ball&&!o.Stored);
            Need(ball!=null&&Sim.ShopQuantity("ball")==stock,"production callback did not place purchased ball");
            Need(Math.Abs(ball!.X-ground.X)<.01&&Math.Abs(ball.Z-ground.Z)<.01,"placement position mismatch");
            Button(_groupList,"空手");Button(_choiceList,"戳碰");await RenderFrames(2);
            var covered=FindOverlap(c.Id,ball,true);Need(covered.HasValue,"ordinary foreground ball/body overlap missing");
            return(c,ball,FindHead(c.Id),covered!.Value);
        }
        foreach(string mode in new[]{"prop-clear-release-control","prop-direct-press-control","prop-covered-release","prop-behind-body-control"})
        {
            var(c,ball,clear,covered)=await Setup();Vector2 down=mode=="prop-direct-press-control"?covered:clear;
            Vector2 up=mode=="prop-clear-release-control"?clear:covered;double actorMove=0;
            if(mode=="prop-behind-body-control")
            {
                // Explicit actor-position fixture, as in the original stationary-pointer audit.
                // The purchased/legally placed ball remains unchanged. No Contact is fabricated.
                double originalZ=c.Z;Vector2? behind=null;
                foreach(double shift in new[]{.6,.8,1.0,1.2,1.4,1.6})
                {
                    c.Z=originalZ+shift;
                    _actors[c.Id].RefreshInteraction(c,true,false,0,InteractionPresentation.Pose(c),_force,Vector2.Zero,true,SurfaceSide.Whole,Sim.State.RoomObjects);
                    behind=FindOverlap(c.Id,ball,false);if(behind.HasValue){actorMove=shift;break;}
                }
                Need(behind.HasValue,"no raw ball ray behind current body in bounded actor-position fixture");
                down=FindHead(c.Id);up=behind!.Value;
            }
            var before=StateOf(c);var signature=Fingerprint(c);var pressRay=RayAt(ball,down);var releaseRay=RayAt(ball,up);
            var releaseHit=ContactAt(up);Press(down);bool armed=_pokeContact!=null;
            Need(mode=="prop-direct-press-control"?!armed:armed&&!_pulled,"unexpected pending poke state");
            Release(up);bool changed=Fingerprint(c)!=signature;bool expected=mode is "prop-clear-release-control" or "prop-behind-body-control";
            bool currentSurface=!expected||_tapContact==releaseHit;
            check(mode,changed==expected&&currentSurface&&_pokeContact==null&&!_swingArmed,
                new{changed,expectedChange=expected,armed,currentSurface,cachedCleared=_pokeContact==null,before,after=StateOf(c),pressRay,releaseRay,
                    ball=new{ball.Id,ball.X,ball.Z,ball.Height},actorMoveZ=actorMove,
                    delta=new{health=c.Health-signature.health,pain=c.Pain-signature.pain,fear=c.Fear-signature.fear,trust=c.Trust-signature.trust,resentment=c.Resentment-signature.resentment,reactionSerial=c.ReactionSerial-signature.serial}});
        }
    }
}