using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private async void StartStationInspectorVerificationIfRequested(string[] args)
    {
        int flag=Array.IndexOf(args,StationInspectorVerification.Flag);
        if(flag<0)return;
        var results=new List<object>();int failures=0;
        void Require(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
        void Check(string name,Action action)
        {
            try{action();results.Add(new{name,passed=true});GD.Print("PASS "+name);}
            catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.Message,header=_who.Text,
                details=_objectDetails.Text,detailsVisible=_objectDetails.Visible,stationPanelVisible=_supplyStationPanel.Visible,
                selectedObject=_selectedRoomObjectId,observedStem=_observedStemMotherId,selectedStation=_selectedSupplyStationId});
                GD.Print("FAIL "+name+": "+e.Message);}
        }
        string Resources()=>JsonSerializer.Serialize(new{Sim.State.Money,Sim.State.Food,Sim.State.StationWaterPortions,
            Sim.State.FoodStationStock,Sim.State.WaterStationStock,Sim.State.ShopStock,
            objects=Sim.State.RoomObjects.Select(o=>new{o.Id,o.Portions,o.Stored}),
            stations=Sim.State.SupplyStations.Select(s=>new{s.Id,s.StoredPortions,s.OutletObjectId})});
        void Click(Node root,string label)
        {
            var button=root.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text==label);
            Require(!button.Disabled,"Disabled button: "+label);button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        void Press(Vector2 p)=>_UnhandledInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=p});
        void Release(Vector2 p)=>_Input(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=p});
        void Present(){_windowFocused=true;_Process(0);UpdateRoomCamera(1);RefreshRoomUi();}
        void Tool(string category,string? action=null){Click(_groupList,category);if(action!=null)Click(_choiceList,action);}
        Vector2 ObjectPoint(RoomObject obj)
        {
            var center=_camera.UnprojectPosition(new Vector3((float)obj.X,(float)obj.Height+.1f,(float)obj.Z));
            int scene=0,hits=0;
            for(int y=-60;y<=60;y+=4)for(int x=-80;x<=80;x+=4)
            {var p=center+new Vector2(x,y);if(IsScenePoint(p))scene++;if(ObjectAt(p)?.Id==obj.Id){hits++;if(IsScenePoint(p))return p;}}
            throw new InvalidOperationException($"No projected object point. center={center}, canvas={GameUiLayout.Canvas(_ui)}, viewport={GetViewport().GetVisibleRect()}, scene={scene}, hits={hits}, visible={_objects[obj.Id].Visible}");
        }
        Vector2 StationPoint(SupplyStationState station)
        {
            var center=_camera.UnprojectPosition(new Vector3((float)station.X,.35f,(float)station.Z));
            for(int y=-60;y<=60;y+=3)for(int x=-65;x<=65;x+=3)
            {var p=center+new Vector2(x,y);if(IsScenePoint(p)&&ObjectAt(p)==null&&ContactAt(p) is {TargetKind:InteractionTargetKind.SupplyStation} hit&&hit.Id==station.Id)return p;}
            throw new InvalidOperationException("No unobscured projected station point.");
        }
        void Observe(RoomObject obj)
        {
            Tool("观察");Present();var p=ObjectPoint(obj);Press(p);Release(p);
            Require(_selectedRoomObjectId==obj.Id&&_selectedSupplyStationId==null&&_objectDetails.Visible&&_who.Text==RoomResourceText.Label(obj,Sim.State),"Object observation fixture failed.");
        }
        SupplyStationState Place(SupplyStationKind kind)
        {
            int before=kind==SupplyStationKind.Food?Sim.State.FoodStationStock:Sim.State.WaterStationStock;
            int other=kind==SupplyStationKind.Food?Sim.State.WaterStationStock:Sim.State.FoodStationStock;
            int money=Sim.State.Money,food=Sim.State.Food,water=Sim.State.StationWaterPortions,count=Sim.State.SupplyStations.Count;
            Tool("食物",kind==SupplyStationKind.Food?"食物供给器":"饮水器");
            Require(_placingSupplyStationKind==kind,"Toolbar did not arm station placement.");
            Vector2? point=null;
            for(float z=-5;z<=5&&point==null;z+=.5f)for(float x=-5;x<=5;x+=.5f)
            {
                var p=_camera.UnprojectPosition(new Vector3(x,0,z));
                if(IsScenePoint(p)&&ContactAt(p)==null&&ObjectAt(p)==null&&TryGroundPoint(p,0,out var ground)&&Sim.CanPlaceSupplyStationAt(kind,ground.X,ground.Z)){point=p;break;}
            }
            Require(point.HasValue,"No valid visible station placement point.");Press(point!.Value);Release(point.Value);Present();
            Require(Sim.State.SupplyStations.Count==count+1&&_placingSupplyStationKind==null,"Projected placement did not create exactly one station.");
            Require((kind==SupplyStationKind.Food?Sim.State.FoodStationStock:Sim.State.WaterStationStock)==before-1&&
                (kind==SupplyStationKind.Food?Sim.State.WaterStationStock:Sim.State.FoodStationStock)==other&&Sim.State.Money==money&&Sim.State.Food==food&&Sim.State.StationWaterPortions==water,
                "Placement did not consume exactly the intended empty device.");
            return Sim.State.SupplyStations.Last();
        }
        void Inspector(SupplyStationState station)
        {
            Require(_selectedSupplyStationId==station.Id,"Wrong station identity.");
            Require(_selectedRoomObjectId==null&&_observedStemMotherId==null,"Stale object/stem selection survives station selection.");
            Require(_who.Text==SupplyStationName(station.Kind),"Station header overwritten: "+_who.Text);
            Require(_supplyStationPanel.Visible&&!_objectDetails.Visible&&_objectDetails.Text==""&&!_objectActions.Visible,"Stale extra properties or actions remain visible.");
            Require(_supplyStationDescription.Text.Contains("总量")&&_closeButton.Disabled&&_familyPanelButton.Disabled,"Station inspector controls do not match selection.");
        }
        void Frequency(SupplyStationState station)
        {
            string resources=Resources();var others=Sim.State.SupplyStations.Where(s=>s.Id!=station.Id).Select(s=>(s.Id,s.RefillIntervalSeconds)).ToArray();
            int before=station.RefillIntervalSeconds;Click(_stationFrequency,"›");
            Require(station.RefillIntervalSeconds!=before&&others.All(old=>Sim.State.SupplyStations.Single(s=>s.Id==old.Id).RefillIntervalSeconds==old.RefillIntervalSeconds),"Frequency changed another station or failed to change the current station.");
            Require(Resources()==resources,"Frequency selection changed inventory.");
        }
        void Fresh(bool stem=false)
        {
            CancelGesture();CloseActivityPanel();Sim=WorldSimulation.CreateCourtyardEconomy();BuildActors();
            _selectedSupplyStationId=_selectedShelterId=_selectedDetachedStemEventId=null;_autoClock=-10000;_windowFocused=true;
            // Use real sale confirmation callbacks and exact purchase transactions to unlock the catalogue.
            foreach(var oil in Sim.State.Creatures.Skip(1).ToArray())
            {
                Sim.State.SelectedId=oil.Id;ShowCreatureMarket(confirmingId:oil.Id,expectedPrice:CourtyardEconomy.Quote(oil).Price);
                Click(_panelContents,"确认卖给商店");
            }
            CloseActivityPanel();Require(Sim.ShopUnlockReason("food-station")==null,"Real two-sale fixture did not unlock stations.");
            foreach(string id in new[]{"food-station","water-station"})
            {
                int money=Sim.State.Money,stock=Sim.ShopQuantity(id);var product=CourtyardShop.Products.Single(p=>p.Id==id);
                Require(Sim.BuyShopProduct(id,2)&&Sim.State.Money==money-2*product.Price&&Sim.ShopQuantity(id)==stock+2,"Real station purchase failed.");
            }
            Sim.State.SelectedId=Sim.State.Creatures[0].Id;
            if(stem)
            {
                // Existing-pregnancy component fixture, not a full breeding journey. The father is a known sold identity.
                Sim.Selected!.Family.Pregnancy=new(){Phase=PregnancyPhase.Gestating,Method=BirthMethod.Stem,
                    FatherId=Sim.State.CreatureSales[0].Creature.Id,EventId="station-inspector-stem",PlannedChildren=2,Elapsed=40,
                    Stem=new(){EventId="station-inspector-stem"}};
            }
            SaveStore.Validate(Sim.State);DisarmTool();Present();
            // The baseline has no cushion hit geometry. Use a normally placed finite meal instead.
            int food=Sim.State.Food,count=Sim.State.RoomObjects.Count;Vector2? mealPoint=null;
            for(float z=-3;z<=4&&mealPoint==null;z+=.5f)for(float x=-3;x<=4;x+=.5f)
            {
                var p=_camera.UnprojectPosition(new Vector3(x,0,z));
                if(IsScenePoint(p)&&ContactAt(p)==null&&ObjectAt(p)==null&&Sim.CanPlaceSupplyStationAt(SupplyStationKind.Food,x,z)){mealPoint=p;break;}
            }
            Require(mealPoint.HasValue,"No ground meal fixture point.");Tool("食物","饭饭");Press(mealPoint!.Value);Release(mealPoint.Value);Present();
            Require(Sim.State.Food==food-1&&Sim.State.RoomObjects.Count==count+1&&Sim.State.RoomObjects.Last().Kind==RoomObjectKind.Meal,"Real meal tool placement did not consume one portion.");
        }
        void ObserveStem()
        {
            Tool("观察");Present();var mother=Sim.Selected!;
            var center=_camera.UnprojectPosition(new Vector3((float)mother.X,.7f,(float)mother.Z));Vector2? point=null;
            for(int y=-140;y<=90&&point==null;y+=2)for(int x=-90;x<=90;x+=2)
            {var p=center+new Vector2(x,y);if(IsScenePoint(p)&&ObjectAt(p)==null&&ContactAt(p) is {TargetKind:InteractionTargetKind.Stem} hit&&hit.Id==mother.Id){point=p;break;}}
            Require(point.HasValue,$"No live projected external stem contact. center={center}, canvas={GameUiLayout.Canvas(_ui)}, status={Sim.StemCareStatus(mother.Id)!=null}");Press(point!.Value);Release(point.Value);
            Require(_observedStemMotherId==mother.Id&&_who.Text=="外茎与幼芽"&&_objectDetails.Visible,"Stem observation fixture failed.");
        }
        try
        {
            SetProcess(false);_autoClock=-10000;_muted=true;_sound.Stop();_roomMusic.Stop();
            await RenderFrames(4);
            foreach(var kind in Enum.GetValues<SupplyStationKind>())
            foreach(string route in new[]{"placement","grab","observe","stem-placement","stem-grab","stem-observe"})
            {
                string name=kind+" / "+route;
                Check(name+" fixture and transition",()=>
                {
                    bool stem=route.StartsWith("stem",StringComparison.Ordinal);Fresh(stem);
                    var prior=Place(kind);var groundObject=Sim.State.RoomObjects.Single(o=>o.Kind==RoomObjectKind.Meal);
                    if(stem)ObserveStem();else Observe(groundObject);
                    string resources=Resources();SupplyStationState selected=prior;
                    if(route.EndsWith("placement",StringComparison.Ordinal))selected=Place(kind);
                    else
                    {
                        if(route.EndsWith("grab",StringComparison.Ordinal))Tool("空手","抓取");else Tool("观察");
                        var p=StationPoint(prior);Press(p);
                        if(route.EndsWith("grab",StringComparison.Ordinal))Require(prior.HeldByHand&&_sceneDragId==prior.Id,"Grab callback did not pick up station.");
                        Release(p);Present();Require(Resources()==resources,"Selection/grab changed inventory.");
                    }
                    // Independent checks retain targeting/control evidence even when the header regression fails.
                    Check(name+" header and details",()=>Inspector(selected));
                    Check(name+" frequency target and inventory",()=>Frequency(selected));
                    Check(name+" repeated refresh",()=>{RefreshRoomUi();RefreshRoomUi();Inspector(selected);});
                    Check(name+" select previous object",()=>
                    {
                        string before=Resources();if(stem)ObserveStem();else Observe(groundObject);
                        Require(_selectedSupplyStationId==null&&!_supplyStationPanel.Visible&&Resources()==before,"Returning to previous object kept station UI or changed inventory.");
                    });
                    SaveStore.Validate(Sim.State);
                });
                await RenderFrames(2);
            }
        }
        catch(Exception e){failures++;results.Add(new{name="diagnostic setup",passed=false,error=e.Message});}
        File.WriteAllText(args[flag+1],JsonSerializer.Serialize(new{success=failures==0,failures,checks=results.Count,
            renderer=DisplayServer.GetName(),scope="Headless real toolbar/button signals and projected production input callbacks; real sale unlock, station purchase and meal placement; controlled validated pregnancy fixture. No native UI or visual acceptance.",results},new JsonSerializerOptions{WriteIndented=true}));
        GD.Print($"STATION_INSPECTOR_CHECKS: {results.Count-failures}/{results.Count}; failures={failures}");
        GetTree().Quit(failures==0?0:1);
    }
}
