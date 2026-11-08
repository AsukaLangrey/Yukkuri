extends SceneTree
func _initialize():
	var args = OS.get_cmdline_user_args()
	var base = args[0]
	var packer = PCKPacker.new()
	if packer.pck_start(base.path_join("ClangProbe.pck")) != OK:
		quit(2)
		return
	for name in ["project.godot", "probe.tscn", "probe-node.gd"]:
		if packer.add_file("res://" + name, base.path_join("packed/" + name)) != OK:
			quit(3)
			return
	quit(0 if packer.flush() == OK else 4)
