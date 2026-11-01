extends SceneTree
func _initialize():
	var args = OS.get_cmdline_user_args()
	var out = args[2]
	DirAccess.make_dir_recursive_absolute(out)
	var names = [".godot/exported/133200997/export-bcb0d2eb5949c52b6a65bfe9de3e985b-Main.scn", ".godot/imported/pipeline-probe.glb-92ab040049fa3010a444cda7c29661a0.scn"]
	for i in range(2):
		if not ProjectSettings.load_resource_pack(args[i], true):
			quit(2)
			return
		for j in range(names.size()):
			var bytes = FileAccess.get_file_as_bytes("res://" + names[j])
			var stem = out.path_join("pack%d-scene%d" % [i, j])
			var header = bytes.slice(0,4).get_string_from_ascii()
			if header == "RSCC":
				# Resource compression uses the same FileAccessCompressed format,
				# but with RSCC rather than the public API's GCPF magic.
				bytes[0]=71; bytes[1]=67; bytes[2]=80; bytes[3]=70
				var copy = FileAccess.open(stem + ".gcmp", FileAccess.WRITE)
				copy.store_buffer(bytes); copy.close()
				var decoded = FileAccess.open_compressed(stem + ".gcmp", FileAccess.READ)
				if decoded == null:
					quit(3)
					return
				bytes = decoded.get_buffer(decoded.get_length())
				decoded.close()
			var target = FileAccess.open(stem + ".raw", FileAccess.WRITE)
			target.store_buffer(bytes); target.close()
			print("SCENE_DECODE pack=",i," scene=",j," original_header=",header," bytes=",bytes.size())
	quit(0)
