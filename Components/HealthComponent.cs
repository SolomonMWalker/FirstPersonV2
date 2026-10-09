using System;
using Godot;

namespace FirstPersonV3.Components;

public partial class HealthComponent: Node
{
    [Export] public int InitialHealthAmount {get; set;} = 200;

    private int _currentAmount;

    [Signal] public delegate void HealthAtZeroEventHandler();

    public override void _Ready()
    {
        _currentAmount = InitialHealthAmount;
    }

    public void ReduceHealth(int amount)
    {
        _currentAmount -= amount;
        if(_currentAmount <= 0)
        {
            EmitSignalHealthAtZero();
            _currentAmount = 0;
        }
    }
}
