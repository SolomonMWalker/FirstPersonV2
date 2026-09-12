@tool
extends EditorScenePostImport

# Adds hand-authored tracks to the imported Arms.glb animations.
#
# The .glb is the base class; this file is the subclass. The importer regenerates the bone
# tracks from Blender every time, then this runs and re-applies everything below, so edits
# in Blender and edits here never fight. Nothing is hand-edited in the editor, so nothing
# is lost on reimport.
#
# Requires "Save to File" to stay OFF on every animation. With it on, the importer writes
# the animations before this script runs and the added tracks silently vanish at runtime
# (godotengine/godot#85738).

# Node paths are relative to the AnimationPlayer's root, which is the scene root ("Arms").
# Meshes live inside the .glb, so those paths resolve here at import time. The controller
# and the skeleton modifier are added by player_rig_glb.tscn, so their paths only resolve
# at runtime -- that is expected, and why MISSING NODE below is a note rather than an error.
const CYLINDER := "StateMachine/WeaponControllers/RevolverController/RevolverCylinderController"
const SPINNER := "RigArmature/Skeleton3D/RevolverCylinderRotationModifier"

# ---------------------------------------------------------------------------------------
# The spec. Add entries here; times are in seconds from the start of the clip.
#
#   {"type": "method",  "node": <path>, "at": <t>, "call": <name>, "args": [...]}
#   {"type": "value",   "prop": "<path>:<Property>", "keys": [[t, value], ...]}
#   {"type": "visible", "node": <path>, "keys": [[t, true/false], ...]}
#   {"type": "audio",   "node": <path>, "at": <t>, "stream": "res://...wav"}
# ---------------------------------------------------------------------------------------
const TRACKS := {
	# VERIFIED WORKING, but disabled: FireBullet() indexes Bullets[ammoInCylinder - 1], and
	# Bullets / BulletShells are still empty arrays on RevolverCylinderController in
	# player_rig_glb.tscn. Fill those in (Bullets = bulletWithCasing1..6,
	# BulletShells = bulletCasing1..6) and uncomment.
	#
	# "RevolverRigHipFire": [
	#	{"type": "method", "node": CYLINDER, "at": 0.0, "call": "FireBullet"},
	# ],
	# "RevolverRigAimFire": [
	#	{"type": "method", "node": CYLINDER, "at": 0.0, "call": "FireBullet"},
	# ],

	# --- patterns for the other track types; tune the times, then uncomment ---
	#
	# "RevolverRigReloadTurnCylinder": [
	#	{"type": "value", "prop": SPINNER + ":SpinDegrees", "keys": [[0.0, 0.0], [0.083, 60.0]]},
	# ],
	#
	# "RevolverRigReloadStartOpenCylinderHip": [
	#	{"type": "visible", "node": "bulletWithCasing1", "keys": [[0.0, true], [0.12, false]]},
	# ],
	#
	# Audio needs an AudioStreamPlayer(3D) node in player_rig_glb.tscn and a sound file;
	# neither exists yet.
	# "RevolverRigHipFire": [
	#	{"type": "audio", "node": "GunAudio", "at": 0.0, "stream": "res://Assets/Audio/shot.wav"},
	# ],
}


func _post_import(scene: Node) -> Node:
	var player := scene.find_child("AnimationPlayer", true, false) as AnimationPlayer
	if player == null:
		push_error("[arms_post_import] no AnimationPlayer in the imported scene")
		return scene

	var added := 0
	var clips: PackedStringArray = player.get_animation_list()
	for clip_name in TRACKS:
		if not player.has_animation(clip_name):
			push_warning("[arms_post_import] no such clip: %s" % clip_name)
			continue
		var anim := player.get_animation(clip_name)
		for spec in TRACKS[clip_name]:
			if _add_track(scene, anim, clip_name, spec):
				added += 1

	print("[arms_post_import] %d track(s) added across %d clip(s)" % [added, TRACKS.size()])
	return scene


func _add_track(scene: Node, anim: Animation, clip_name: String, spec: Dictionary) -> bool:
	match spec.get("type", ""):
		"method":
			_note_if_missing(scene, spec["node"], clip_name)
			var t := _fresh_track(anim, Animation.TYPE_METHOD, NodePath(spec["node"]))
			anim.track_insert_key(t, spec["at"], {
				"method": StringName(spec["call"]),
				"args": spec.get("args", []),
			})
			return true

		"value":
			var path: String = spec["prop"]
			_note_if_missing(scene, path.get_slice(":", 0), clip_name)
			var tv := _fresh_track(anim, Animation.TYPE_VALUE, NodePath(path))
			for k in spec["keys"]:
				anim.track_insert_key(tv, k[0], k[1])
			return true

		"visible":
			_note_if_missing(scene, spec["node"], clip_name)
			var tb := _fresh_track(anim, Animation.TYPE_VALUE, NodePath(spec["node"] + ":visible"))
			# booleans must not be interpolated
			anim.value_track_set_update_mode(tb, Animation.UPDATE_DISCRETE)
			for k in spec["keys"]:
				anim.track_insert_key(tb, k[0], k[1])
			return true

		"audio":
			_note_if_missing(scene, spec["node"], clip_name)
			var stream := load(spec["stream"])
			if stream == null:
				push_warning("[arms_post_import] missing stream %s" % spec["stream"])
				return false
			var ta := _fresh_track(anim, Animation.TYPE_AUDIO, NodePath(spec["node"]))
			anim.audio_track_insert_key(ta, spec["at"], stream)
			return true

		_:
			push_warning("[arms_post_import] unknown track type in %s: %s" % [clip_name, spec])
			return false


# Replaces any existing track with the same type+path, so re-running never stacks duplicates.
func _fresh_track(anim: Animation, type: int, path: NodePath) -> int:
	var existing := anim.find_track(path, type)
	if existing != -1:
		anim.remove_track(existing)
	var idx := anim.add_track(type)
	anim.track_set_path(idx, path)
	return idx


func _note_if_missing(scene: Node, path: String, clip_name: String) -> void:
	if scene.get_node_or_null(NodePath(path)) == null:
		print("[arms_post_import]   note: '%s' is not in the .glb (resolves at runtime) - %s"
			% [path, clip_name])
