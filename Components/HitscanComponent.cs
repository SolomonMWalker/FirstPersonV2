using Godot;
using System;

public partial class HitscanComponent : Node
{
    [Export(PropertyHint.None, "suffix:m")] public float Range { get; set; } = 100f;
    [Export(PropertyHint.Layers3DPhysics)] public uint CollisionMask { get; set; } = 1;
    // Pow exponent on the pellet's distance from center: 0.5 = even spread across the cone; 1 = clustered near center.
    [Export] public float CenterBias { get; set; } = 0.5f;
    // The shooter's own body, so pellets don't hit it. Optional.
    [Export] public CollisionObject3D Shooter { get; set; }

    // With this many pellets or more, the first flies dead center so long-range shots always land something.
    private const int AccuratePelletThreshold = 3;

    private readonly PhysicsRayQueryParameters3D _query = new();

    // Touches the physics space, so only call this during the physics step (_PhysicsProcess or code it calls).
    public void FireHitscan(Transform3D sourceGlobalTransform,
        float spreadDegrees,
        int numberOfPellets,
        int damagePerPellet)
    {
        var space = GetViewport().FindWorld3D().DirectSpaceState;
        var basis = sourceGlobalTransform.Basis.Orthonormalized();

        _query.From = sourceGlobalTransform.Origin;
        _query.CollisionMask = CollisionMask;
        _query.Exclude = Shooter is null ? [] : [Shooter.GetRid()];

        for (int i = 0; i < numberOfPellets; i++)
        {
            var pelletSpread = i == 0 && numberOfPellets >= AccuratePelletThreshold ? 0f : spreadDegrees;
            _query.To = sourceGlobalTransform.Origin + RandomDirectionInCone(basis, pelletSpread, CenterBias) * Range;

            var hit = space.IntersectRay(_query);
            if (hit.Count == 0) continue;

            if (hit["collider"].As<GodotObject>() is PhysicalBone3DHitbox hitbox)
                hitbox.HittableHit(damagePerPellet, sourceGlobalTransform.Origin);
        }
    }

    private Vector3 RandomDirectionInCone(Basis aim, float spreadDegrees, float centerBias = 0.5f)
    {
        var forward = -aim.Z;
        if (spreadDegrees <= 0f) return forward;

        // centerBias 0.5 = even spread across the circle; 1 = clustered near center; <0.5 = pushed to the edge
        var theta = Mathf.DegToRad(spreadDegrees) * Mathf.Pow(GD.Randf(), centerBias);
        var phi = GD.Randf() * Mathf.Tau;

        var offset = aim.X * Mathf.Cos(phi) + aim.Y * Mathf.Sin(phi);
        return (forward + offset * Mathf.Tan(theta)).Normalized();
    }
}
