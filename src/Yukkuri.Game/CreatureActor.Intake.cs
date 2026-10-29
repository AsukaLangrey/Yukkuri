using Godot;

public partial class CreatureActor
{
    private Node3D? _intakeMouth;
    private readonly Node3D[] _intakeFrames=new Node3D[ActorIntakeMouthSurface.FrameCount];
    private void BuildIntakeMouth()
    {
        _intakeMouth=new Node3D{Name="IntakeMouth"};_visual.AddChild(_intakeMouth);
        for(int n=0;n<_intakeFrames.Length;n++)
        {
            _intakeFrames[n]=new Node3D{Visible=false};_intakeMouth.AddChild(_intakeFrames[n]);
            foreach(var layer in ActorIntakeMouthSurface.Layers(n))
            {
                var a=new Vector2(layer.A.X,layer.A.Y);var b=new Vector2(layer.B.X,layer.B.Y);
                if(layer.Stroke)FaceStroke(_intakeFrames[n],a,b,layer.Width,layer.Color,layer.Offset);
                else FaceOval(_intakeFrames[n],a,b,layer.Color,layer.Offset);
            }
        }
        _intakeMouth.Visible=false;
    }
    private void PresentIntakeMouth(IntakeAppearance appearance)
    {
        if(_intakeMouth==null)BuildIntakeMouth();
        _intakeMouth!.Visible=appearance.Active;_mouth.Visible=!appearance.Active;
        for(int n=0;n<_intakeFrames.Length;n++)_intakeFrames[n].Visible=appearance.Active&&n==appearance.MouthFrame;
    }
}
