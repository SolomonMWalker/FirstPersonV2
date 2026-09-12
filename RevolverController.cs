using Godot;
using System;
using FirstPerson.StateMachines;

public partial class RevolverController : Node
{
    [Export] public StateMachine StateMachine { get; set; }
    [Export] public RevolverCylinderController RevolverCylinderController { get; set; }
    [Export] public State Idle { get; set; }

    public bool CanAct => Idle is { Enabled: true };

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

    public void GetInput(bool pressFire, bool aim, bool pressReload)
    {
        if (!CanAct) return;
        if(aim && !aiming) StartAim();
        else if (!aim && aiming) EndAim();
        if (pressReload)
        {
            reloadTrigger = true;
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
            reloadTrigger = true;
        }
        else
        {
            GD.Print("PushHammerDown");
            pushHammerDownTrigger = true;
        }
    }

    public void BulletFired()
    {
        SubtractBulletFromCylinder();
        RevolverCylinderController.FireBullet();
    }

    public void ReloadAddBullet()
    {
        AddBulletToCylinder();
        RevolverCylinderController.ReloadBullet();
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
