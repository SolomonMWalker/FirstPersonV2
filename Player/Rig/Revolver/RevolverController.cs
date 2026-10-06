using Godot;
using System;
using FirstPerson.StateMachines;

public partial class RevolverController : Node
{
    [Export] public StateMachine StateMachine { get; set; }
    [Export] public RevolverCylinderController RevolverCylinderController { get; set; }
    [Export] public State Idle { get; set; }

    public const int CylinderCapacity = 6;

    public bool CanAct => Idle is { Enabled: true };

    public bool CanReload => ammoInCylinder < CylinderCapacity && reserveAmmo > 0;

    public override void _Ready()
    {
        base._Ready();
        if (Idle is null) GD.PushError($"{Name}: Idle is not assigned; no input will be accepted.");
    }

    public int reserveAmmo = 100;
    public int ammoInCylinder = CylinderCapacity;

    // One-shot triggers, consumed by rig states.
    public bool fireTrigger;
    public bool pushHammerDownTrigger;
    public bool reloadTrigger;

    public bool aiming;
    public bool isHammerDown;
    public bool reloadInterrupted;

    public int reloadRemaining;

    public void BeginReload() => reloadRemaining = Mathf.Min(CylinderCapacity - ammoInCylinder, reserveAmmo);

    public void GetInput(bool pressFire, bool aim, bool pressReload)
    {
        // Fire is the only input accepted outside Idle: it interrupts a reload.
        if (pressFire && !CanAct) reloadInterrupted = true;
        if (!CanAct) return;
        if(aim && !aiming) StartAim();
        else if (!aim && aiming) EndAim();
        if (pressReload)
        {
            if (CanReload) reloadTrigger = true;
        }
        else if (pressFire)
        {
            if(isHammerDown) Fire();
            else PushHammerDown();
        }
    }

    public void StartAim()
    {
        aiming = true;
    }

    public void EndAim()
    {
        aiming = false;
    }

    public void Fire()
    {
        GD.Print("Fire");
        isHammerDown = false;
        fireTrigger = true;
    }

    public void PushHammerDown()
    {
        if (ammoInCylinder <= 0)
        {
            GD.Print("No ammo in cylinder!");
            if (CanReload) reloadTrigger = true;
        }
        else
        {
            GD.Print("PushHammerDown");
            pushHammerDownTrigger = true;
        }
    }

    // Mesh updates index off ammoInCylinder, so they run before the count changes.
    public void BulletFired()
    {
        RevolverCylinderController.FireBullet();
        SubtractBulletFromCylinder();
    }

    // Animation method key; runs deferred, so the reload loop counts with reloadRemaining instead.
    public void ReloadAddBullet()
    {
        if (ammoInCylinder >= CylinderCapacity || reserveAmmo <= 0) return;
        RevolverCylinderController.ReloadBullet();
        AddBulletToCylinder();
        SubtractBulletFromTotalAmmo();
    }

    public void AddBulletToCylinder()
    {
        ammoInCylinder += 1;
    }

    public void SubtractBulletFromCylinder()
    {
        ammoInCylinder -= 1;
    }

    public void SubtractBulletFromTotalAmmo()
    {
        reserveAmmo -= 1;
    }
}
