namespace FirstPerson;

// The physics collision layers, mirroring project.godot's [layer_names] section. Godot's inspector
// numbers layers from 1; the value stored in collision_layer/collision_mask is the bit, 2^(n-1).
// Both are given below because scene files hold the raw integer and there is no way around that --
// only C# can use these names, so a .tscn value and its meaning have to be checked by hand.
//
// The split that matters: a character's movement capsule lives on CharacterPhysics and nowhere
// else, while the volumes that register hits live on Player/Enemy. Grunt's capsule fully encloses
// its 19 per-bone hitboxes, so a damage ray that could see both would hit the capsule every time and
// no bone would ever register -- a failure invisible in the editor. Damage queries mask the hit
// layers and never CharacterPhysics; movement masks do the reverse.
public static class Layers
{
	public const uint StaticGeometry = 1 << 0;      // inspector layer 1, value 1
	public const uint DynamicGeometry = 1 << 1;     // inspector layer 2, value 2
	public const uint CharacterPhysics = 1 << 2;    // inspector layer 3, value 4
	public const uint Player = 1 << 3;              // inspector layer 4, value 8
	public const uint Enemy = 1 << 4;               // inspector layer 5, value 16

	// Anything solid enough to stop a shot or a step. DynamicGeometry has no members yet -- it is
	// here so the first crate that turns up is blocked and shootable without touching these masks.
	public const uint WorldSolid = StaticGeometry | DynamicGeometry;    // 3

	// What a character's move_and_slide is blocked by. move_and_slide reads only the *mover's* mask,
	// never the target's, so every character that should be stopped by something needs it here.
	public const uint Movement = WorldSolid | CharacterPhysics;         // 7

	// Player weapons hit the world and enemies; enemy weapons hit the world and the player. The
	// world half is not optional -- drop it and shots pass through walls instead of stopping.
	public const uint PlayerShot = WorldSolid | Enemy;                  // 19
	public const uint EnemyShot = WorldSolid | Player;                  // 11
}
