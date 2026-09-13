using Godot;
using System;

public partial class PhysicalBone3DHitbox : PhysicalBone3D
{
    public enum HitboxType
    {
        Normal, Weakspot
    }
    
    [Export] public HitboxType Type { get; set; }
    
    [Signal] public delegate void HitEventHandler(PhysicalBone3DHitbox source, int damageAmount, Vector3 damageSourceGlobalPosition);
    
    public void HittableHit(int damageAmount, Vector3 damageSourceGlobalPosition)
    {
        EmitSignalHit(this, damageAmount, damageSourceGlobalPosition);
    }
}
