namespace Yukkuri.Simulation;

// Authored fixture dimensions, shared by placement and the renderer.
public static class InteractionFixtureGeometry
{
    public const float StandingFootprint=.78f,SupineFootprint=1.36f;
    public const float BallRadius=.24f,YardFenceInner=8.14f;
    public const float SupineAngle=-105,SupineOffsetZ=-.08f;
    public const float FrameWidth=1.52f,FrameDepth=1.36f;
    public const float CushionWidth=1.8f,CushionDepth=1.35f;
    public const double HangingMinHeight=.35,HangingMaxHeight=1.55,HangingInitialHeight=1.3;
    public static double ClampSupportHeight(double height,double bodyScale,bool hanging)=>Math.Clamp(height,hanging?HangingMinHeight*bodyScale:.4,hanging?HangingMaxHeight*bodyScale:3);
    public static double FrameFootprint=>Math.Max(SupineFootprint,Math.Sqrt(FrameWidth*FrameWidth+FrameDepth*FrameDepth)*.5);
    public const float BoxWidth=1.8f,BoxDepth=1.72f,BoxRoofY=1.57f,BoxFloorY=-.43f;
    public const float BoxSideX=.85f,BoxWallThickness=.10f,BoxBackThickness=.08f;
    public const float BoxRoofThickness=.10f,BoxFloorThickness=.08f,BoxBarBottom=-.12f;
    public static float BoxBackZ=>-(BoxDepth*.5f-BoxBackThickness*.5f);
    public static float BoxFrontZ=>BoxDepth*.5f-.05f;
    public static float BoxWallBottom=>BoxFloorY+BoxFloorThickness*.5f;
    public static float BoxSidePanelDepth=>BoxDepth-BoxWallThickness;
    public static float BoxSidePanelZ=>-BoxWallThickness*.5f;
    public static float BoxBackWidth=>BoxWidth-2*BoxWallThickness;
    public static float BoxWallTop=>BoxRoofY-BoxRoofThickness*.5f;
    public static float BoxWallHeight=>BoxWallTop-BoxWallBottom;
    public static float BoxWallY=>(BoxWallTop+BoxWallBottom)*.5f;
    public static double BoxFootprint=>Math.Sqrt(BoxWidth*BoxWidth+BoxDepth*BoxDepth)*.5;
    public static double HangingFootprint=>Math.Sqrt(1.25*1.25+.9*.9);
}
