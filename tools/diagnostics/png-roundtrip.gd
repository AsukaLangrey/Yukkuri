extends SceneTree

func _initialize():
	var args = OS.get_cmdline_user_args()
	if args.size() != 2:
		quit(2)
		return
	var image = Image.load_from_file(args[0])
	if image == null or image.is_empty():
		quit(3)
		return
	DirAccess.make_dir_recursive_absolute(args[1])
	var expected = image.get_data()
	print("PNG_PROBE source_bytes=", expected.size(), " source_hash=", hash(expected))
	for i in range(64):
		var path = args[1].path_join("round-%03d.png" % i)
		var code = image.save_png(path)
		print("PNG_WRITE index=", i, " code=", code, " source_hash=", hash(image.get_data()))
		if code != OK:
			quit(4)
			return
	quit(0)
