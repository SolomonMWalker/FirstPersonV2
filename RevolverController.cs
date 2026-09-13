using Godot;
using System;
using FirstPerson.StateMachines;

public partial class RevolverController : Node
{
    [Export] public StateMachine StateMachine { get; set; }
    [Export] public RevolverCylinderController RevolverCylinderController { get; set; }
    [Export] public State Idle { get; set; }

    public bool CanAct => Idle is { Enabled: true };

    // A reload press is dropped rather than latched when it would gain nothing: a full cylinder,
    // or an empty pouch. Latching it would fire a reload later, at a moment the player never asked
    // for one.
    public bool CanReload => ammoInCylinder < 6 && reserveAmmo > 0;

    public override void _Ready()
    {
        base._Ready();
        if (Idle is null) GD.PushError($"{Name}: Idle is not assigned; no input will be accepted.");
    }
    
    public int reserveAmmo = 100;
    public int ammoInCylinder = 6;

    //Fired action bools
    public bool fireTrigger;
    public bool pushHammerDownTrigger;
    public bool reloadTrigger;

    //Consistent "state" bools
    public bool aiming;
    public bool isHammerDown;
    public bool reloadInterrupted;

    // Rounds still to seat in the reload in progress. Fixed by BeginReload() when the reload
    // starts, then counted down by each insert state as it commits to a round.
    public int reloadRemaining;

    public void BeginReload() => reloadRemaining = Mathf.Min(6 - ammoInCylinder, reserveAmmo);

    public void GetInput(bool pressFire, bool aim, bool pressReload)
    {
        // The one input accepted outside Idle: fire aborts a reload in progress. Only the Reload
        // states read this, and IntroInsertBullet clears it on entry, so a press latched during
        // Fire or PushHammerDown cannot leak into the next reload.
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

    // Both of these index the chamber meshes off ammoInCylinder, so the mesh call has to run
    // before the count moves: FireBullet reads ammoInCylinder - 1, ReloadBullet reads
    // ammoInCylinder.
    public void BulletFired()
    {
        RevolverCylinderController.FireBullet();
        SubtractBulletFromCylinder();
    }

    // Called from a method key at the end of each insert clip. Nothing may branch on the count it
    // writes in the same frame -- AnimationMixer dispatches method keys deferred, so this lands
    // after every _PhysicsProcess. The reload loop counts with reloadRemaining for that reason.
    //
    // Guarded because the caller is animation content: a stray or duplicated key would otherwise
    // push ammoInCylinder past 6, burn a reserve round, and crash the chamber-angle lookup, which
    // only has entries for 0..6.
    public void ReloadAddBullet()
    {
        if (ammoInCylinder >= 6 || reserveAmmo <= 0) return;
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
