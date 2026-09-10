using Godot;
using System;
using System.Collections.Generic;

public partial class RevolverCylinderController : Node
{
    private Dictionary<int, int> _bulletsLeftInCylinderToShootPosition = new Dictionary<int, int>()
    {
        { 6, 300 },
        { 5, 0 },
        { 4, 60 },
        { 3, 120 },
        { 2, 180 },
        { 1, 240 },
        { 0, 300 }
    };

    private int _reloadSlotOffsetInDegrees = 60;
    private int _hammerDownTurnAmountInDegrees = 60;
}
