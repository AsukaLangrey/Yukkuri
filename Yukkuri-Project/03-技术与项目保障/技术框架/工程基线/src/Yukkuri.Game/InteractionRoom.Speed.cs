using System;
using System.Globalization;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private static readonly double[] CourtyardSpeedChoices={1,2,3,5,10};

    private void AddCourtyardSpeedChoice()
    {
        _modalContents.AddChild(Text("小院速度（近看表现保持1倍）",14));
        var choice=new InlineChoice{Name="CourtyardSpeedChoice"};
        foreach(double speed in CourtyardSpeedChoices)choice.AddItem(speed+"倍");
        double current=Sim.State.Settings.Speed;
        int selected=Array.IndexOf(CourtyardSpeedChoices,current);
        if(selected<0)
        {
            // Historical continuous speeds remain valid; viewing settings is not a migration.
            choice.AddItem(current.ToString("G",CultureInfo.InvariantCulture)+"倍（当前旧档值）");
            selected=CourtyardSpeedChoices.Length;
        }
        choice.Select(selected);
        choice.ItemSelected+=index=>
        {
            if(index<0||index>=CourtyardSpeedChoices.Length)return;
            Sim.State.Settings.Speed=CourtyardSpeedChoices[(int)index];
            ShowSettings();
        };
        _modalContents.AddChild(choice);
    }
}