using Godot;
using System;
using FirstPerson.StateMachines;

public partial class RevolverController : Node
{
    [Export] public StateMachine StateMachine { get; set; }
    
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

    public void GetInput(bool pressFire, bool aim)
    {
        if(aim && !aiming) StartAim();
        else if (!aim && aiming) EndAim();
        if (pressFire)
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
        GD.Print("PushHammerDown");
        pushHammerDownTrigger = true;
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
