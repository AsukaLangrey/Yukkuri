using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private bool _waterRescueToolArmed;
    private void ArmWaterRescueTool()
    {
        CancelGesture();_toolArmed=false;_waterRescueToolArmed=true;
        _message=Sim.State.WaterRescueTools>0?
            $"已拿出救援用品，剩余 {Sim.State.WaterRescueTools} 份。点击落水的油，将它救到岸上。":
            $"救援用品已经用完。先在用品面板购买，每份 {WaterEnvironment.RescueToolPrice} 元。";
        RefreshRoomUi();
    }
}
