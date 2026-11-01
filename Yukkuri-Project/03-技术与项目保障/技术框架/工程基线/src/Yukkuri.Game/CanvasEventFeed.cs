using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed record CanvasNotice(string Key,string Text,int Priority,string? TargetId=null,bool Transient=false);

// Transparent, non-modal notices. Full session history belongs to the right sidebar.
public partial class CanvasEventFeed : VBoxContainer
{
    private readonly Label _text;
    private readonly Button _locate;
    private Dictionary<string,CanvasNotice> _known=new();
    private readonly List<CanvasNotice> _pending=new();
    private readonly List<string> _history=new();
    private CanvasNotice? _current;
    private double _remaining;
    public event Action<string>? TargetRequested;
    public IReadOnlyList<string> History=>_history;

    public CanvasEventFeed()
    {
        MouseFilter=MouseFilterEnum.Ignore;Visible=false;
        _text=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,HorizontalAlignment=HorizontalAlignment.Center,
            MouseFilter=MouseFilterEnum.Ignore};
        _text.AddThemeColorOverride("font_color",new Color("fff8e6"));
        _text.AddThemeColorOverride("font_outline_color",new Color("253b32"));
        _text.AddThemeConstantOverride("outline_size",5);AddChild(_text);
        _locate=new Button{Text="定位对象",FocusMode=FocusModeEnum.None,SizeFlagsHorizontal=SizeFlags.ShrinkCenter};
        _locate.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());
        _locate.AddThemeStyleboxOverride("hover",new StyleBoxEmpty());
        _locate.AddThemeStyleboxOverride("pressed",new StyleBoxEmpty());
        _locate.AddThemeColorOverride("font_color",new Color("fff8e6"));
        _locate.AddThemeColorOverride("font_outline_color",new Color("253b32"));_locate.AddThemeConstantOverride("outline_size",5);
        _locate.Pressed+=()=>{if(_current?.TargetId is { } id)TargetRequested?.Invoke(id);};AddChild(_locate);
    }
    public bool BlocksPoint(Vector2 point)=>GameUiLayout.Hit(_locate,point);
    public void UpdateNotices(IEnumerable<CanvasNotice> notices)
    {
        var next=notices.GroupBy(n=>n.Key).ToDictionary(g=>g.Key,g=>g.OrderByDescending(n=>n.Priority).First());
        // Resolved requests remain in history, but must not appear later from the queue.
        _pending.RemoveAll(n=>!n.Transient&&!next.ContainsKey(n.Key));
        if(_current!=null&&!_current.Transient&&!next.ContainsKey(_current.Key))_remaining=Math.Min(_remaining,1);
        foreach(var notice in next.Values.OrderByDescending(n=>n.Priority))
        {
            if(_known.TryGetValue(notice.Key,out var old)&&old.Text==notice.Text&&old.Priority==notice.Priority)continue;
            Publish(notice);
        }
        _known=next;
    }
    public void Publish(CanvasNotice notice)
    {
        _history.Add(notice.Text);if(_history.Count>100)_history.RemoveAt(0);
        _pending.RemoveAll(n=>n.Key==notice.Key);
        if(_current?.Key==notice.Key){ShowNotice(notice);return;}
        if(_current==null||notice.Priority>_current.Priority)
        {
            if(_current!=null&&_remaining>0)_pending.Add(_current);
            ShowNotice(notice);
        }
        else _pending.Add(notice);
        _pending.Sort((a,b)=>b.Priority.CompareTo(a.Priority));
        if(_pending.Count>8)_pending.RemoveRange(8,_pending.Count-8);
    }
    public void ResetSession(){_known.Clear();_pending.Clear();_history.Clear();_current=null;Visible=false;}
    private void ShowNotice(CanvasNotice notice)
    {
        _current=notice;_remaining=notice.Priority>=2?8:5;
        _text.Text=notice.Text;_locate.Visible=notice.TargetId!=null;Modulate=Colors.White;Visible=true;
    }
    public override void _Process(double delta)
    {
        if(_current==null)return;
        _remaining-=delta;Modulate=new Color(1,1,1,(float)Math.Clamp(_remaining,0,1));
        if(_remaining>0)return;
        _current=null;Visible=false;
        if(_pending.Count>0){var next=_pending[0];_pending.RemoveAt(0);ShowNotice(next);}
    }
}
