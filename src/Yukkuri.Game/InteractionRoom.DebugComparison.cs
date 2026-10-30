using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class InteractionRoom
{
    private sealed record DebugParameter(string Path,string Value);
    private readonly Dictionary<string,DebugParameter> _debugParameters=new(StringComparer.Ordinal);
    private readonly Dictionary<string,DebugParameter> _debugBaseline=new(StringComparer.Ordinal);
    private Button _debugBaselineButton=null!,_debugCompareToggle=null!,_debugClearBaseline=null!;
    private Label _debugBaselineLabel=null!;
    private bool _debugChangesOnly;
    private double _debugBaselineWorldSeconds;

    private void BuildDebugComparisonControls(Control parent)
    {
        var row=Row(parent,5);
        _debugBaselineButton=Button("记下当前读数",CaptureDebugBaseline);row.AddChild(_debugBaselineButton);
        _debugCompareToggle=Button("只看变化",()=>
        {
            if(_debugBaseline.Count==0)return;
            _debugChangesOnly=!_debugChangesOnly;_debugPage=0;
            RefreshDebugComparisonControls();RenderDebugRows();
        });row.AddChild(_debugCompareToggle);
        _debugClearBaseline=Button("清除对照",()=>{ClearDebugComparison();RenderDebugRows();});row.AddChild(_debugClearBaseline);
        _debugBaselineLabel=Text("",12,"78816e");parent.AddChild(_debugBaselineLabel);
        RefreshDebugComparisonControls();
    }

    private void CaptureDebugBaseline()
    {
        // A frozen panel records the values actually on screen. A live panel
        // refreshes once; neither operation advances or restores game state.
        if(!_debugFrozen)RefreshDebugPanel(true);
        if(_debugSnapshotJson.Length==0)return;
        _debugBaseline.Clear();
        foreach(var pair in _debugParameters)_debugBaseline.Add(pair.Key,pair.Value);
        _debugBaselineWorldSeconds=_debugWorldSeconds;
        _debugChangesOnly=true;_debugPage=0;
        RefreshDebugComparisonControls();RenderDebugRows();
    }

    private void ClearDebugComparison()
    {
        _debugBaseline.Clear();_debugChangesOnly=false;_debugPage=0;
        RefreshDebugComparisonControls();
    }

    private void RefreshDebugComparisonControls()
    {
        if(_debugBaselineButton==null)return;
        bool hasBaseline=_debugBaseline.Count>0;
        _debugBaselineButton.Disabled=_debugSnapshotJson.Length==0;
        _debugCompareToggle.Disabled=_debugClearBaseline.Disabled=!hasBaseline;
        _debugCompareToggle.Text=_debugChangesOnly?"查看全部":"只看变化";
        _debugBaselineLabel.Text=hasBaseline?
            $"对照：世界时间 {_debugBaselineWorldSeconds:0.0} 秒的 {_debugBaseline.Count} 项读数。显示“之前 → 当前”；不会回退游戏。":
            "可先记下读数，再操作，查看哪些参数发生变化。切换个体、范围或读档会清除对照。";
    }

    private List<string> DebugChangedRows()
    {
        var rows=new List<string>();
        foreach(var pair in _debugParameters)
        {
            if(!_debugBaseline.TryGetValue(pair.Key,out var previous))
                rows.Add("新增 · "+FormatDebugParameter(pair.Value.Path,pair.Value.Value));
            else if(previous.Value!=pair.Value.Value)
                rows.Add(FormatDebugParameter(pair.Value.Path,previous.Value+" → "+pair.Value.Value));
        }
        foreach(var pair in _debugBaseline)
            if(!_debugParameters.ContainsKey(pair.Key))
                rows.Add("已移除（之前路径） · "+FormatDebugParameter(pair.Value.Path,pair.Value.Value+" → （不存在）"));
        return rows;
    }

    private static string FormatDebugParameter(string path,string value)
    {
        string label=string.Join(" / ",path.Split('.').Select(part=>DebugParameterLabel(part.Split('[')[0]))
            .Where(part=>part.Length>0).Distinct());
        return (label.Length>0?label+" · ":"")+(path.Length==0?"当前范围":path)+" = "+value;
    }

    private static string DebugKeyPart(string part)=>part.Replace("~","~0").Replace("/","~1");

    private static void CollectDebugParameters(JsonElement value,string path,string key,Dictionary<string,DebugParameter> parameters)
    {
        if(value.ValueKind==JsonValueKind.Object)
        {
            bool any=false;
            foreach(var property in value.EnumerateObject())
            {
                any=true;
                CollectDebugParameters(property.Value,path.Length==0?property.Name:path+"."+property.Name,
                    key+"/p:"+DebugKeyPart(property.Name),parameters);
            }
            if(!any)parameters.Add(key,new(path,"{}"));
        }
        else if(value.ValueKind==JsonValueKind.Array)
        {
            // Stable unique IDs keep world-list insertion/removal from making
            // another oil appear to inherit the previous oil's measurements.
            var ids=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var item in value.EnumerateArray())
                if(DebugObjectId(item) is { } id)ids[id]=ids.GetValueOrDefault(id)+1;
            int index=0;
            foreach(var item in value.EnumerateArray())
            {
                string? id=DebugObjectId(item);
                string segment=id!=null&&ids[id]==1?"/id:"+DebugKeyPart(id):"/i:"+index;
                CollectDebugParameters(item,path+"["+index+"]",key+segment,parameters);index++;
            }
            if(index==0)parameters.Add(key,new(path,"[]"));
        }
        else parameters.Add(key,new(path,value.GetRawText()));
    }

    private static string? DebugObjectId(JsonElement value)
    {
        if(value.ValueKind!=JsonValueKind.Object)return null;
        if(value.TryGetProperty("Id",out var id)&&id.ValueKind==JsonValueKind.String&&!string.IsNullOrEmpty(id.GetString()))
            return "Id:"+id.GetString();
        // Independent broods retain the original pregnancy identity rather than
        // inventing a second creature ID. Keep their world comparisons stable too.
        if(value.TryGetProperty("EventId",out var eventId)&&eventId.ValueKind==JsonValueKind.String&&!string.IsNullOrEmpty(eventId.GetString()))
            return "EventId:"+eventId.GetString();
        return null;
    }
}
