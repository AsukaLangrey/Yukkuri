using Godot;
using System;
using System.Collections.Generic;

// In-place selection: changing an item never creates a PopupMenu or Window.
public partial class InlineChoice : HBoxContainer
{
    private readonly List<(string text,int id)> _items=new();
    private readonly Button _previous,_next;
    private readonly Label _value;
    private int _selected=-1;
    private bool _disabled;
    public event Action<long>? ItemSelected;
    public int Selected=>_selected;
    public int ItemCount=>_items.Count;
    public bool Disabled { get=>_disabled; set{_disabled=value;RefreshChoice();} }

    public InlineChoice()
    {
        SizeFlagsHorizontal=SizeFlags.ExpandFill;
        MouseFilter=MouseFilterEnum.Stop;
        AddThemeConstantOverride("separation",4);
        _previous=new Button{Text="‹",CustomMinimumSize=new(28,32),FocusMode=FocusModeEnum.None};
        _value=new Label{Text="暂无选项",ClipText=true,TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal=SizeFlags.ExpandFill,CustomMinimumSize=new(40,32),
            HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,
            MouseFilter=MouseFilterEnum.Ignore};
        _next=new Button{Text="›",CustomMinimumSize=new(28,32),FocusMode=FocusModeEnum.None};
        AddChild(_previous);AddChild(_value);AddChild(_next);
        _previous.Pressed+=()=>Step(-1);_next.Pressed+=()=>Step(1);
        RefreshChoice();
    }

    public void AddItem(string text,int id=-1)
    {
        _items.Add((text,id<0?_items.Count:id));
        if(_selected<0)_selected=0;
        RefreshChoice();
    }
    public void Clear(){_items.Clear();_selected=-1;RefreshChoice();}
    // Like OptionButton.Select, model-driven synchronization must not fire a command.
    public void Select(int index){_selected=index>=0&&index<_items.Count?index:-1;RefreshChoice();}
    public string GetItemText(int index)=>_items[index].text;
    public int GetItemId(int index)=>_items[index].id;
    private void Step(int direction)
    {
        if(_disabled||_items.Count<2)return;
        _selected=_selected<0?(direction>0?0:_items.Count-1):(_selected+direction+_items.Count)%_items.Count;
        RefreshChoice();ItemSelected?.Invoke(_selected);
    }
    private void RefreshChoice()
    {
        _previous.Disabled=_next.Disabled=_disabled||_items.Count<2;
        _value.Text=_selected>=0?_items[_selected].text:"暂无选项";
    }
}
