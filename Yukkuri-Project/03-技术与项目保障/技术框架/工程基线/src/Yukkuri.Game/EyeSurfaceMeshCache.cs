using Godot;
using System;
using System.Collections.Generic;

// Room-owned geometry only. Materials and animation state remain actor-owned.
// 96 pairs is a conservative memory budget inherited from the old per-actor cache,
// not a gameplay limit. Actors retain their original local 96-pair working sets;
// this second-level cache adds at most 96 pairs per room. FIFO eviction drops
// references, never disposes live meshes.
internal sealed class EyeSurfaceMeshCache
{
    internal const int Capacity=96;
    private readonly Dictionary<(float open,float arc,bool hat),(ArrayMesh left,ArrayMesh right)> _pairs=new();
    private readonly Queue<(float open,float arc,bool hat)> _order=new();
    internal int Count=>_pairs.Count;
    internal (ArrayMesh left,ArrayMesh right) Get(float open,float arc,bool hat,Func<(ArrayMesh left,ArrayMesh right)> build)
    {
        var key=(open,arc,hat);
        if(_pairs.TryGetValue(key,out var pair))return pair;
        pair=build();
        if(_pairs.Count>=Capacity)_pairs.Remove(_order.Dequeue());
        _pairs.Add(key,pair);_order.Enqueue(key);
        return pair;
    }
    internal void Clear(){_pairs.Clear();_order.Clear();}
}