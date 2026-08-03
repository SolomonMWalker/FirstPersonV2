using System;
using System.Collections.Generic;
using System.Linq;
using FirstPerson.Scenes.Player;
using Godot;

namespace FirstPerson.Helpers;

public partial class ClamberController : Node3D
{
    [Export] public PlayerController PlayerController;
    [Export] public Timer PauseBetweenClamberAttemptsTimer { get; set; }
    [Export] public float RaycastLength { get; private set; } = 0.25f;
    [Export] public float ClamberMargin { get; private set; } = 0.26f;
    //[Export] public float MaxAngleInDeg { get; private set; } = 10f;
    [Export] public float WaitPerCallInSec { get; private set; } = 0.25f;

    private List<List<RayCast3D>> Raycasts { get; set; } = [];
    private Node3D RaycastsParent { get; set; }
    
    private float ClamberXzDistanceSquared { get; set; }
    private Vector3 ClamberDestination { get; set; }
    private Vector2 ClamberDestinationXz { get; set; }
    private Vector3 ClamberStartPoint { get; set; }
    private Vector2 ClamberStartPointXz { get; set; }
    private Vector2 ClamberXzDirection { get; set; }

    public override void _Ready()
    {
        base._Ready();
        RaycastsParent = GetNode<Node3D>("Raycasts");
        foreach (var child in RaycastsParent.GetChildren())
        {
            List<RayCast3D> rcList = [];
            rcList.AddRange(child.GetChildren().Cast<RayCast3D>());
            Raycasts.Add(rcList);
        }
    }

    private List<(Vector2 localSlice, Vector3 globalEndpoint, bool collided)> GetRaycastEndPoints(List<RayCast3D> raycasts)
    {
        List<(Vector2, Vector3, bool)> collisions = [];
        foreach (var rc in raycasts)
        {
            if (rc.IsColliding())
            {
                var globalEndpoint = rc.GetCollisionPoint();
                var endpoint = ToLocal(globalEndpoint);
                collisions.Add((new Vector2(endpoint.Z, endpoint.Y), globalEndpoint, true));
            }
            else
            {
                var globalEndpoint = rc.ToGlobal(rc.TargetPosition);
                var endpointLocalToThis = ToLocal(globalEndpoint);
                collisions.Add((new Vector2(endpointLocalToThis.Z, endpointLocalToThis.Y), globalEndpoint, false));
            }
        }
        return collisions;
    }
    
    private (bool success, RaycastCollisionResult result) AttemptClamberCheckRow(List<RayCast3D> raycasts)
    {
        var rawCollisions = GetRaycastEndPoints(raycasts);
        if (rawCollisions.All(c => !c.collided)) return (false, null);

        //If top raycast is colliding, we can't clamber
        var maxY = rawCollisions.Select(rc => rc.localSlice.Y).Max();
        var collidedCollisions = rawCollisions
            .Where(rc => rc.collided)
            .ToArray();
        if (collidedCollisions.Any(rc => Math.Abs(rc.localSlice.Y - maxY) < 0.0001f))
        {
            return (false, null);
        }

        var clamberPoint = collidedCollisions
            .OrderByDescending(c => c.localSlice.Y)
            .First();
        return (true, new RaycastCollisionResult
        {
            GlobalPositionToClamberTo = clamberPoint.globalEndpoint
        });
        //took out extra "check for angle" code for now
    }

    private (bool success, RaycastCollisionResult result) AttemptClamber()
    {
        if (!PauseBetweenClamberAttemptsTimer.IsStopped()) return (false, null);
        PauseBetweenClamberAttemptsTimer.Start();
        foreach (var rcList in Raycasts)
        {
            var clamberAttemptRow = AttemptClamberCheckRow(rcList);
            if (clamberAttemptRow.success) return clamberAttemptRow;
        }
        return (false, null);
    }
    
    public bool TryHandleClamber()
    {
        var clamberCheck = AttemptClamber();
        if (!clamberCheck.success) return false;
        ClamberDestination = clamberCheck.result.GlobalPositionToClamberTo ?? Vector3.Zero;
        ClamberDestinationXz = new Vector2(ClamberDestination.X, ClamberDestination.Z);
        ClamberStartPoint = GlobalPosition;
        ClamberStartPointXz = new Vector2(GlobalPosition.X, GlobalPosition.Z);
        ClamberXzDirection = ClamberStartPointXz.DirectionTo(ClamberDestinationXz);
        ClamberXzDistanceSquared = ClamberStartPointXz.DistanceSquaredTo(ClamberDestinationXz);
        return true;
    }
    
    public void Clamber()
    {
        if (PlayerController.BottomOfPlayer.GlobalPosition.Y < ClamberDestination.Y + ClamberMargin)
        { //move up to clamber Y
            PlayerController.Velocity = Vector3.Up * PlayerController.ClamberVelocity;
            PlayerController.MoveAndSlide();
            return;
        }
        if (ClamberXzDistanceSquared > ClamberStartPointXz.DistanceSquaredTo(new Vector2(GlobalPosition.X, GlobalPosition.Z)))
        { //move forward to clamber Z
            PlayerController.Velocity = new Vector3(ClamberXzDirection.X, 0, ClamberXzDirection.Y) * PlayerController.ClamberVelocity;
            PlayerController.MoveAndSlide();
            return;
        }
        PlayerController.Clambering = false;
    }
}

public class RaycastCollisionResult
{
    public Vector3? GlobalPositionToClamberTo { get; init; }
}