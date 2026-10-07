using Godot;

namespace FirstPerson;

public static class ExtensionMethods
{
    // Physics never rotates a CharacterBody3D, so code that sets its rotation must check AxisLockAngularY itself.
    public static void FreezeYRotation(this CharacterBody3D body) => body.AxisLockAngularY = true;

    public static void UnfreezeYRotation(this CharacterBody3D body) => body.AxisLockAngularY = false;
}
