using Godot;

public partial class InteractionRoom
{
    private HBoxContainer _contactProgressRow=null!;
    private Label _contactProgressText=null!;
    private ProgressBar _contactProgressMeter=null!;

    private void BuildContactProgress(Control parent)
    {
        _contactProgressRow=Row(parent,8);
        _contactProgressRow.Visible=false;
        _contactProgressRow.MouseFilter=Control.MouseFilterEnum.Ignore;
        _contactProgressText=Text("",13);
        _contactProgressText.AutowrapMode=TextServer.AutowrapMode.Off;
        _contactProgressRow.AddChild(_contactProgressText);
        _contactProgressMeter=NeedMeter(_contactProgressRow,"8aa576",out _);
        _contactProgressMeter.CustomMinimumSize=new(100,8);
        _contactProgressMeter.SizeFlagsVertical=Control.SizeFlags.ShrinkCenter;
    }

    private void RefreshContactProgress()
    {
        double? progress=Sim.InteractionCompletionProgress;
        bool visible=progress.HasValue;
        _contactProgressRow.Visible=visible;
        if(progress is not { } amount)return;
        _forceLabel.Visible=_forceSlider.Visible=false;
        _contactProgressMeter.Value=amount*100;
        _contactProgressText.Text=amount>=1?"已完成 · 松手":"保持接触";
        // This reads uninterrupted contact time from the simulation, not pointer
        // hold duration. Leaving the surface therefore resets the displayed bar.
        _hint.Text=amount>=1?"这一次已经完成。松开后才能开始下一次操作。":
            "保持工具与目标接触；移开会重新计时，松手可停止。";
    }
}
