namespace Yukkuri.Simulation;

public static class InteractionSurfaceObservation
{
    private static readonly (BodyPart part,SurfaceSide side,string name)[] Areas=
    [
        (BodyPart.Head,SurfaceSide.Whole,"头发"),
        (BodyPart.Cheek,SurfaceSide.Left,"左脸"),(BodyPart.Cheek,SurfaceSide.Right,"右脸"),
        (BodyPart.Side,SurfaceSide.Left,"身体左侧"),(BodyPart.Side,SurfaceSide.Right,"身体右侧"),
        (BodyPart.Mouth,SurfaceSide.Whole,"嘴边"),(BodyPart.Bottom,SurfaceSide.Whole,"底部")
    ];
    public static IEnumerable<string> Describe(InteractionLife life)
    {
        var dirty=Areas.Where(a=>life.DirtAt(a.part,a.side)>1).Select(a=>a.name).ToArray();
        var wet=Areas.Where(a=>life.WetnessAt(a.part,a.side)>1).Select(a=>a.name).ToArray();
        if(dirty.Length>0)yield return dirty.Length>3?"身上有多处污渍":string.Join("、",dirty)+"还有污渍";
        if(wet.Length>0)yield return wet.Length>3?"身上多处还湿着":string.Join("、",wet)+"还湿着";
        if(life.HeadSensitivity>15)yield return life.HeadSensitivity>45?"额头还敏感，碰近时会缩开":"额头还有些不舒服，接触时比较拘谨";
        double left=life.CheekSensitivityAt(SurfaceSide.Left),right=life.CheekSensitivityAt(SurfaceSide.Right);
        if(Math.Max(left,right)>15)
        {
            string side=left>15&&right>15?"两边脸侧":left>15?"左脸侧":"右脸侧";
            yield return side+(Math.Max(left,right)>45?"还敏感，会躲开揉蹭":"还有些不舒服，暂时不宜反复揉蹭");
        }
    }
}
