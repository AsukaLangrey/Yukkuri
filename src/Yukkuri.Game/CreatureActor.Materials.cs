using Godot;

public partial class CreatureActor
{
    private static void ApplyDoughMaterial(MeshInstance3D skin)
    {
        // Keep the exact SphereMesh used by contact and floor-support calculations.
        // Vertical UV color gives the lower dough a little warmth without adding geometry.
        var gradient=new Gradient
        {
            Offsets=new[]{0f,.36f,.70f,1f},
            Colors=new[]{new Color("fff4df"),new Color("fff0d9"),new Color("f5dfbb"),new Color("e9cda7")}
        };
        var texture=new GradientTexture2D
        {
            Gradient=gradient,Width=16,Height=128,FillFrom=new(.5f,0),FillTo=new(.5f,1)
        };
        var material=(StandardMaterial3D)SceneArt.Mat("ffeaca").Duplicate();
        material.AlbedoColor=Colors.White;material.AlbedoTexture=texture;
        material.Roughness=.94f;material.MetallicSpecular=.18f;
        skin.MaterialOverride=material;
    }
}
