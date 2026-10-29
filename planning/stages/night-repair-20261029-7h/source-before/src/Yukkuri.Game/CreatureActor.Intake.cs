using Godot;

public partial class CreatureActor
{
    private Node3D? _intakeMouth;
    private readonly Node3D[] _intakeFrames=new Node3D[5];
    private void BuildIntakeMouth()
    {
        _intakeMouth=new Node3D{Name="IntakeMouth"};_visual.AddChild(_intakeMouth);
        for(int n=0;n<_intakeFrames.Length;n++)
        {_intakeFrames[n]=new Node3D{Visible=false};_intakeMouth.AddChild(_intakeFrames[n]);}
        FaceOval(_intakeFrames[0],new(0,-.175f),new(.085f,.09f),"7e4543",.012f);
        FaceStroke(_intakeFrames[1],new(-.055f,-.175f),new(.055f,-.175f),.018f,"9b5c4d",.012f);
        FaceOval(_intakeFrames[2],new(0,-.175f),new(.11f,.065f),"7e4543",.012f);
        FaceOval(_intakeFrames[2],new(.015f,-.19f),new(.048f,.023f),"dc8d85",.018f);
        FaceOval(_intakeFrames[3],new(0,-.175f),new(.032f,.045f),"7e4543",.012f);
        FaceStroke(_intakeFrames[4],new(-.03f,-.175f),new(.03f,-.175f),.012f,"9b5c4d",.012f);
        _intakeMouth.Visible=false;
    }
    private void PresentIntakeMouth(IntakeAppearance appearance)
    {
        if(_intakeMouth==null)BuildIntakeMouth();
        _intakeMouth!.Visible=appearance.Active;_mouth.Visible=!appearance.Active;
        for(int n=0;n<_intakeFrames.Length;n++)_intakeFrames[n].Visible=appearance.Active&&n==appearance.MouthFrame;
    }
}
