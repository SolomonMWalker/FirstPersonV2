using Godot;
using System;
using System.Linq;

public partial class HitboxComponent : Node
{
    [Export] public Node HitboxParent { get; set; }
    private Godot.Collections.Array<PhysicalBone3DHitbox> Hitboxes { get; set; } = [];

    public override void _Ready()
    {
        base._Ready();
        Hitboxes.AddRange(HitboxParent.GetChildren().OfType<PhysicalBone3DHitbox>());
        foreach (var hitbox in Hitboxes)
        {
            hitbox.Hit += OnHit;
        }
    }
    
    private void OnHit(PhysicalBone3DHitbox source, int damageAmount, Vector3 damageSourceGlobalPosition)
    {
        
    }
}
