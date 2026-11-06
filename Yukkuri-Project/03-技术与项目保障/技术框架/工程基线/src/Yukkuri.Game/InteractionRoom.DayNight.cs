using Godot;

public partial class InteractionRoom
{
    private Godot.Environment _courtyardEnvironment=null!;
    private DirectionalLight3D _courtyardSun=null!,_courtyardMoon=null!;
    private double _lastLightingSeconds=double.NaN;
    private void BuildCourtyardLighting()
    {
        _courtyardEnvironment=new Godot.Environment {
            BackgroundMode=Godot.Environment.BGMode.Color,
            AmbientLightSource=Godot.Environment.AmbientSource.Color,
            TonemapMode=Godot.Environment.ToneMapper.Linear };
        AddChild(new WorldEnvironment{Environment=_courtyardEnvironment});
        _courtyardSun=new DirectionalLight3D{ShadowEnabled=true};AddChild(_courtyardSun);
        _courtyardMoon=new DirectionalLight3D{RotationDegrees=new(-40,145,0),LightColor=new Color("a6bcff"),ShadowEnabled=false};
        AddChild(_courtyardMoon);
        UpdateCourtyardLighting();
    }
    private void UpdateCourtyardLighting()
    {
        double seconds=Sim.State.WorldSeconds;
        if(seconds==_lastLightingSeconds)return;
        _lastLightingSeconds=seconds;
        var light=CourtyardLightCycle.Sample(seconds);
        float day=(float)light.Daylight;
        // Original daytime energies retained. Night values and colors need visual review.
        _courtyardEnvironment.BackgroundColor=new Color("202c48").Lerp(new Color("c3cfba"),day);
        _courtyardEnvironment.AmbientLightColor=new Color("859dce").Lerp(new Color("f3ead8"),day);
        _courtyardEnvironment.AmbientLightEnergy=Mathf.Lerp(.20f,.30f,day);
        _courtyardSun.LightColor=new Color("fff4df").Lerp(new Color("ffd09a"),(float)light.Warmth);
        _courtyardSun.LightEnergy=.34f*day;
        _courtyardSun.RotationDegrees=new((float)light.SunPitch,(float)light.SunYaw,0);
        _courtyardMoon.LightEnergy=.16f*(1-day);
        // Only 3D lighting is touched. Separate CanvasLayer/sidebar colors stay intact.
    }
}