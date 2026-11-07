extends Node3D
## Standalone art study. No WorldSimulation, save access, or main-scene changes.
## All screenshots are captured from the actual Godot viewport, not generated art.

var rng := RandomNumberGenerator.new()
var materials: Dictionary = {}
var meshes: Dictionary = {}
var batches: Dictionary = {}
var camera: Camera3D
var roof: Node3D
var output := ""
var capture_frames := 0
var item_count := 0
var render_groups := 0
var start_ticks := 0
var geometry_checks: Array[Dictionary]=[]
var views := [
	{"name":"01-garden", "focus":Vector3(-1,1.3,-1), "offset":Vector3(7,21,28), "size":25.0},
	{"name":"02-cottage", "focus":Vector3(-7,1.5,-4.7), "offset":Vector3(7,7,11), "size":12.5},
	{"name":"03-food-water", "focus":Vector3(-4,.25,1), "offset":Vector3(4,6,8), "size":7.5},
	{"name":"04-roof-open", "focus":Vector3(-7,.7,-5), "offset":Vector3(1,12,8), "size":9.8},
	{"name":"05-overhead", "focus":Vector3(-1,0,-1), "offset":Vector3(0,30,.1), "size":29.0}
]

func _ready() -> void:
	start_ticks=Time.get_ticks_msec()
	rng.seed=2026110304
	var args=OS.get_cmdline_user_args()
	var flag=args.find("--art-output")
	if flag>=0 and flag+1<args.size(): output=args[flag+1]
	get_window().title="悠悠桃源 · 初始庭院美术样板（独立预览）"
	get_window().content_scale_size=Vector2i(1920,1200)
	get_window().content_scale_mode=Window.CONTENT_SCALE_MODE_VIEWPORT
	get_window().content_scale_aspect=Window.CONTENT_SCALE_ASPECT_KEEP
	get_viewport().msaa_3d=Viewport.MSAA_4X
	_setup_light()
	_build_ground()
	_build_cottage()
	_build_tree(Vector3(4,0,-3))
	_build_bowls()
	_build_edges()
	_build_plants()
	_build_characters()
	_flush_batches()
	_setup_overlay()
	_set_view(0)
	print("STARTER_ART_READY pieces=",item_count," batches=",render_groups," seed=2026110304")
	if not output.is_empty(): _capture_all()

func _mat(hex: String, wood: bool=false) -> Material:
	var key=("wood:" if wood else "mat:")+hex
	if materials.has(key): return materials[key]
	var material: Material
	if wood:
		material=ShaderMaterial.new()
		material.shader=load("res://ArtPreview/GardenWood.gdshader")
		material.set_shader_parameter("tint",Color(hex))
	else:
		material=StandardMaterial3D.new()
		material.albedo_color=Color(hex)
		material.roughness=.87
		material.cull_mode=BaseMaterial3D.CULL_DISABLED
	materials[key]=material
	return material

func _foliage_material() -> Material:
	if materials.has("foliage_texture"):return materials["foliage_texture"]
	var img=Image.load_from_file("res://ArtPreview/Textures/foliage-cluster-v01.png")
	img.generate_mipmaps()
	var m=StandardMaterial3D.new();m.albedo_texture=ImageTexture.create_from_image(img)
	m.transparency=BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR;m.alpha_scissor_threshold=.42
	m.cull_mode=BaseMaterial3D.CULL_DISABLED;m.roughness=1
	m.texture_filter=BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	materials["foliage_texture"]=m
	return m

func _foliage(parent: Node3D, pos: Vector3, scale_factor: float) -> void:
	var key="foliage:"+str(parent.get_instance_id())
	if not batches.has(key):
		var quad=QuadMesh.new();quad.size=Vector2.ONE
		batches[key]={"parent":parent,"mesh":quad,"material":_foliage_material(),"transforms":[]}
	var rot=Vector3(-PI/2+rng.randf_range(-.5,.5),rng.randf()*TAU,rng.randf_range(-.3,.3))
	batches[key].transforms.append(Transform3D(Basis.from_euler(rot)*Basis.from_scale(Vector3.ONE*scale_factor),pos))
	item_count+=1

func _mesh(kind: String) -> Mesh:
	if meshes.has(kind): return meshes[kind]
	var m: Mesh
	match kind:
		"box":
			m=BoxMesh.new(); m.size=Vector3.ONE
		"ball":
			m=SphereMesh.new(); m.radius=.5; m.height=1; m.radial_segments=12; m.rings=6
		"smooth":
			m=SphereMesh.new(); m.radius=.5; m.height=1; m.radial_segments=32; m.rings=16
		"cylinder", "cone":
			m=CylinderMesh.new(); m.top_radius=1 if kind=="cylinder" else .18
			m.bottom_radius=1; m.height=1; m.radial_segments=16
		"leaf":
			m=_tri_mesh([Vector3(-.5,0,0),Vector3(0,.075,-.22),Vector3(.5,0,0),Vector3(0,0,.22)], [0,1,2,0,2,3])
		"blade":
			m=_tri_mesh([Vector3(-.045,0,0),Vector3(.045,0,0),Vector3(.025,.24,.025),Vector3(.06,.44,.07)], [0,1,2,0,2,3])
		"tile":
			var pts: Array[Vector3]=[]
			var indices: Array[int]=[]
			for row in range(2):
				for k in range(7):
					var a=PI*float(k)/6
					pts.append(Vector3(float(row),sin(a)*.065,cos(a)*.22))
			for k in range(6): indices.append_array([k,k+1,k+7,k+1,k+8,k+7])
			m=_tri_mesh(pts,indices)
	meshes[kind]=m
	return m

func _tri_mesh(points: Array, indices: Array) -> ArrayMesh:
	var st=SurfaceTool.new(); st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for i in indices: st.add_vertex(points[i])
	st.generate_normals()
	return st.commit()

func _part(parent: Node3D, kind: String, pos: Vector3, size: Vector3, color: String, rotation: Vector3=Vector3.ZERO, wood: bool=false) -> void:
	var mesh=_mesh(kind); var material=_mat(color,wood)
	var key=str(parent.get_instance_id())+":"+kind+":"+str(material.get_instance_id())
	if not batches.has(key): batches[key]={"parent":parent,"mesh":mesh,"material":material,"transforms":[]}
	batches[key].transforms.append(Transform3D(Basis.from_euler(rotation)*Basis.from_scale(size),pos))
	item_count+=1

func _box(parent: Node3D,pos: Vector3,size: Vector3,color: String,wood: bool=false,rot: Vector3=Vector3.ZERO) -> void:
	_part(parent,"box",pos,size,color,rot,wood)

func _beam(parent: Node3D,a: Vector3,b: Vector3,radius: float,color: String,wood: bool=true,taper: bool=false) -> void:
	var delta=b-a
	var rot=Basis(Quaternion(Vector3.UP,delta.normalized())).get_euler()
	_part(parent,"cone" if taper else "cylinder",(a+b)*.5,Vector3(radius,delta.length(),radius),color,rot,wood)

func _root(pos: Vector3=Vector3.ZERO) -> Node3D:
	var n=Node3D.new(); add_child(n); n.position=pos; return n

func _flush_batches() -> void:
	for batch in batches.values():
		var mm=MultiMesh.new(); mm.transform_format=MultiMesh.TRANSFORM_3D
		mm.mesh=batch.mesh; mm.instance_count=batch.transforms.size()
		for i in range(mm.instance_count): mm.set_instance_transform(i,batch.transforms[i])
		var node=MultiMeshInstance3D.new(); node.multimesh=mm; node.material_override=batch.material
		batch.parent.add_child(node); render_groups+=1
	batches.clear()

func _setup_light() -> void:
	var world=WorldEnvironment.new(); var env=Environment.new()
	env.background_mode=Environment.BG_COLOR; env.background_color=Color("cbd5bd")
	env.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color=Color("d4e2ef"); env.ambient_light_energy=.50
	env.tonemap_mode=Environment.TONE_MAPPER_LINEAR
	world.environment=env; add_child(world)
	var sun=DirectionalLight3D.new(); sun.rotation_degrees=Vector3(-54,-35,0)
	sun.light_color=Color("fff5e4"); sun.light_energy=.85; sun.shadow_enabled=true
	sun.directional_shadow_max_distance=70; sun.shadow_bias=.025; sun.shadow_normal_bias=.65
	add_child(sun)
	RenderingServer.directional_shadow_atlas_set_size(4096,true)
	camera=Camera3D.new(); camera.projection=Camera3D.PROJECTION_ORTHOGONAL; camera.far=180
	add_child(camera); camera.current=true

func _build_ground() -> void:
	var p=MeshInstance3D.new(); var plane=PlaneMesh.new(); plane.size=Vector2(100,100)
	p.mesh=plane; var mat=ShaderMaterial.new(); mat.shader=load("res://ArtPreview/GardenGround.gdshader")
	p.material_override=mat; add_child(p)
	var ground=StaticBody3D.new(); var col=CollisionShape3D.new(); var shape=BoxShape3D.new()
	shape.size=Vector3(100,.2,100); col.shape=shape; col.position.y=-.1
	ground.add_child(col); add_child(ground)
	# Flat embedded stones are decorative, never vertical obstacles on the path.
	for k in range(10):
		_part(self,"ball",Vector3(1.5+sin(k*.7)*.2,.015,7.3+k*.55),Vector3(.62,.055,.4),["b9b09a","c9bfa6","a8a58e"][k%3])

func _solid_box(parent: Node3D,pos: Vector3,size: Vector3) -> void:
	var body=StaticBody3D.new(); var collision=CollisionShape3D.new(); var shape=BoxShape3D.new()
	shape.size=size; collision.shape=shape; body.position=pos; body.add_child(collision); parent.add_child(body)

func _build_cottage() -> void:
	var h=_root(Vector3(-7,0,-6)); h.name="StarterCottage"
	# Raised floor, entrance and ramp match V04: 6.4 x 5.6 D; opening 2.8 D.
	_box(h,Vector3(0,.10,0),Vector3(6.4,.2,5.6),"80603c",true)
	_solid_box(h,Vector3(0,.1,0),Vector3(6.4,.2,5.6))
	for i in range(23):
		_box(h,Vector3(-3.06+i*.274,.214,0),Vector3(.258,.035,5.48),["b28a56","bd9660","a97d49","c49a65"][i%4],true)
	_box(h,Vector3(0,1.44,-2.7),Vector3(6.3,2.45,.18),"c9b48b")
	for x in [-3.1,3.1]:
		_box(h,Vector3(x,1.44,0),Vector3(.16,2.45,5.5),"c9b48b")
		_solid_box(h,Vector3(x,1.44,0),Vector3(.2,2.45,5.5))
	_solid_box(h,Vector3(0,1.44,-2.7),Vector3(6.3,2.45,.2))
	for x in [-2.25,2.25]:
		_box(h,Vector3(x,1.44,2.7),Vector3(1.7,2.45,.18),"dfcfaa")
		_solid_box(h,Vector3(x,1.44,2.7),Vector3(1.7,2.45,.2))
	for x in [-3.15,-1.5,1.5,3.15]:
		_box(h,Vector3(x,1.52,2.81),Vector3(.18,2.65,.22),"775137",true)
		_box(h,Vector3(x,.3,2.82),Vector3(.28,.22,.28),"8a7862")
	for z in [-2.75,-1.0,1.0,2.75]:
		for x in [-3.2,3.2]: _box(h,Vector3(x,1.5,z),Vector3(.16,2.65,.18),"8e603a",true)
	for y in [.32,.9,2.48,2.72]:
		if y>2.4:
			_box(h,Vector3(0,y,2.83),Vector3(6.55,.12,.17),"825531",true)
		else:
			for side in [-1,1]: _box(h,Vector3(side*2.3,y,2.83),Vector3(1.85,.12,.17),"825531",true)
		for x in [-3.23,3.23]: _box(h,Vector3(x,y,0),Vector3(.13,.13,5.7),"8f633d",true)
	# Lower front beams stop at the opening; there is no bar across the doorway.
	for x in [-2.32,2.32]:
		for i in range(6): _box(h,Vector3(x+(i-2.5)*.24,.57,2.84),Vector3(.225,.49,.08),["a77946","b38650","956b40"][i%3],true)
	# Rice-paper lattice on the visible right side.
	for z in [-1.25,1.2]:
		_box(h,Vector3(3.23,1.7,z),Vector3(.06,1.1,1.6),"eee0b6")
		for k in range(6): _box(h,Vector3(3.28,1.7,z-.8+k*.32),Vector3(.07,1.2,.045),"936d44",true)
		for y in [1.13,1.5,1.88,2.27]: _box(h,Vector3(3.29,y,z),Vector3(.08,.045,1.7),"936d44",true)
	# Gables with warm planks and exposed triangular frame.
	for z in [-2.81,2.85]:
		for i in range(23):
			var x=-3.08+i*.28; var height=(1-abs(x)/3.5)*1.65
			_box(h,Vector3(x,2.77+height*.5,z),Vector3(.26,height,.12),["bb8e58","ae814c","c59b66"][i%3],true)
		_beam(h,Vector3(-3.55,2.7,z+.06),Vector3(0,4.48,z+.06),.10,"70472e")
		_beam(h,Vector3(0,4.48,z+.06),Vector3(3.55,2.7,z+.06),.10,"70472e")
		_box(h,Vector3(0,3.5,z+.08),Vector3(.15,1.58,.15),"865732",true)
	# True curved clay tiles, not a flat-colored roof prism.
	roof=Node3D.new(); roof.name="RoofToggle"; h.add_child(roof)
	for side in [-1,1]:
		for row in range(8):
			for col in range(16):
				var x=side*(.02+row*.48); var y=4.57-abs(x)*.47
				var tile_rotation=Basis(Vector3(side,-.47,0).normalized(),Vector3(side*.47,1,0).normalized(),Vector3(0,0,side)).get_euler()
				_part(roof,"tile",Vector3(x,y,-3.39+col*.45),Vector3(.56,1,1),["a4714f","ac7a54","b07e57","a77551","b38259"][rng.randi_range(0,4)],tile_rotation)
		# continuous wood fascia makes the silhouette read at game distance.
		_box(roof,Vector3(side*3.87,2.73,0),Vector3(.18,.20,7.25),"785134",true)
		for z in [-3.58,3.58]: _beam(roof,Vector3(0,4.6,z),Vector3(side*3.9,2.77,z),.105,"885b38")
	for col in range(18):
		_part(roof,"cylinder",Vector3(0,4.63,-3.5+col*.41),Vector3(.145,.42,.145),"b47b50",Vector3(PI/2,0,0))
	# Plank landing and continuous shallow ramp (no stairs).
	for i in range(16): _box(h,Vector3(-2.65+i*.35,.20,3.05),Vector3(.33,.10,.60),["bc925c","c49c65","ac814e"][i%3],true)
	var ramp_points=[Vector3(-1.5,.2,2.8),Vector3(1.5,.2,2.8),Vector3(-1.5,0,5.2),Vector3(1.5,0,5.2)]
	var ramp_mesh=_tri_mesh(ramp_points,[0,1,2,1,3,2])
	var ramp=MeshInstance3D.new(); ramp.mesh=ramp_mesh; ramp.material_override=_mat("be945e",true); h.add_child(ramp); ramp.create_trimesh_collision()
	for i in range(13):
		var z=2.82+i*.18; var y=.2*(5.2-z)/2.4+.012
		_box(h,Vector3(0,y,z),Vector3(3,.025,.014),"89623e",true)
	# Short two-panel noren sits above heads, with simple petal motifs.
	for side in [-1,1]:
		var cloth=MeshInstance3D.new(); var st=SurfaceTool.new(); st.begin(Mesh.PRIMITIVE_TRIANGLES)
		var pts: Array[Vector3]=[]
		for iy in range(5):
			for ix in range(9):
				var x=side*.71+(float(ix)/8-.5)*1.29; var y=2.54-float(iy)/4*.65
				pts.append(Vector3(x,y,2.97+sin(ix*.9)*.04+float(iy)*.012))
		for iy in range(4):
			for ix in range(8):
				var k=iy*9+ix
				for id in [k,k+9,k+1,k+1,k+9,k+10]: st.add_vertex(pts[id])
		st.generate_normals(); cloth.mesh=st.commit(); cloth.material_override=_mat("777a9b"); h.add_child(cloth)
		for p in range(5):
			var a=TAU*p/5
			_part(h,"smooth",Vector3(side*.71+cos(a)*.15,2.23+sin(a)*.12,3.055),Vector3(.13,.08,.018),"e5dfc0",Vector3(0,0,a))
	# Three woven sleeping pads inside.
	for x in [-1.8,0,1.8]: _basket_pad(h,Vector3(x,.25,-1.1),.66)
	_basket_pad(h,Vector3(-2.4,.29,3.05),.48)
	for x in [-2.75,2.75]:
		_part(h,"cone",Vector3(x,.42,3.18),Vector3(.20,.42,.20),"a87652")
		for n in range(7):
			var a=n*TAU/7
			_beam(h,Vector3(x,.6,3.18),Vector3(x+cos(a)*.21,.95+sin(n)*.1,3.18+sin(a)*.21),.016,"667936",false)
	_lantern(h,Vector3(2.7,2.1,3.24))

func _basket_pad(parent: Node3D, pos: Vector3, radius: float) -> void:
	_part(parent,"smooth",pos,Vector3(radius*2,.11,radius*1.65),"c3a477")
	for ring in range(4):
		for k in range(40):
			var a=k*TAU/40; var r=radius*(.76+ring*.06)
			_part(parent,"ball",pos+Vector3(cos(a)*r,.035,sin(a)*r*.82),Vector3(.10,.055,.075),["a37b49","d1b383"][k%2])
	_part(parent,"smooth",pos+Vector3(0,.07,0),Vector3(radius*1.4,.06,radius*1.15),"dcc9a0")

func _lantern(parent: Node3D,pos: Vector3) -> void:
	_beam(parent,pos+Vector3(0,.55,0),pos+Vector3(0,.25,0),.018,"63553e")
	_part(parent,"smooth",pos,Vector3(.34,.52,.34),"e9bd70")
	for y in [-.23,.23]: _part(parent,"cylinder",pos+Vector3(0,y,0),Vector3(.13,.045,.13),"775539")
	for n in range(8):
		var a=n*TAU/8
		_beam(parent,pos+Vector3(cos(a)*.16,-.19,sin(a)*.16),pos+Vector3(cos(a)*.16,.19,sin(a)*.16),.007,"bc884e",false)

func _build_tree(pos: Vector3) -> void:
	var t=_root(pos); t.name="ShadeTree"
	_beam(t,Vector3(0,0,0),Vector3(.24,3.6,-.1),.56,"806045",true,true)
	for k in range(7):
		var a=k*TAU/7+.4
		_beam(t,Vector3(0,.28,0),Vector3(cos(a)*1.15,.04,sin(a)*1.15),.15,"806045",true,true)
	for branch in range(12):
		var a=branch*TAU/12+.2; var reach=rng.randf_range(2.4,3.8)
		var end=Vector3(cos(a)*reach,4.5+rng.randf_range(-.5,.6),sin(a)*reach)
		var elbow=Vector3(cos(a)*1.45,2.8+rng.randf_range(-.2,.8),sin(a)*1.45)
		_beam(t,Vector3(.2,1.8,-.1),elbow,.19,"806045",true,true)
		_beam(t,elbow,end,.12,"89694a",true,true)
		for fork in range(2):
			var crown=end+Vector3(cos(a+fork-.5)*.7,.65,sin(a+fork-.5)*.7)
			_beam(t,end,crown,.055,"95754d",true,true)
			for i in range(8):
				var p=crown+Vector3(rng.randf_range(-.8,.8),rng.randf_range(-.3,.5),rng.randf_range(-.8,.8))
				_foliage(t,p,rng.randf_range(1.8,2.5))
	# Central crown fills the branch junction; upper and lower clusters overlap in 3D.
	for i in range(55):
		var a=rng.randf()*TAU;var r=sqrt(rng.randf())*2.3
		_foliage(t,Vector3(cos(a)*r,5.7+rng.randf_range(-.3,.8),sin(a)*r),rng.randf_range(2,2.8))
	_solid_box(t,Vector3(0,1.6,0),Vector3(1.05,3.2,1.05))
	# Low segmented bench sits behind the trunk, never spans the principal path.
	for k in range(8):
		var a=PI+k*PI/7
		_box(t,Vector3(cos(a)*1.25,.40,sin(a)*1.25),Vector3(.64,.09,.42),"b08b55",true,Vector3(0,-a+PI/2,0))
		_box(t,Vector3(cos(a)*1.25,.20,sin(a)*1.25),Vector3(.09,.4,.11),"8e6e45",true)
	_basket_pad(t,Vector3(-1.4,.08,1.35),.55)
	for p in [Vector3(-1.2,3.1,1.2),Vector3(1.65,3.55,.2)]: _lantern(t,p)

func _build_bowls() -> void:
	for index in range(2):
		var pos=Vector3(-5.5 if index==0 else -3,0,.6)
		var bowl=_root(pos); bowl.name="FoodBowl" if index==0 else "WaterBowl"
		var verts: Array[Vector3]=[]; var inds: Array[int]=[]
		# A hollow low-lipped lathed bowl, visibly distinct from a solid cylinder.
		var profile=[Vector2(.30,.02),Vector2(.42,.13),Vector2(.43,.19),Vector2(.37,.19),Vector2(.31,.07)]
		for band in profile:
			for i in range(48):
				var a=i*TAU/48; verts.append(Vector3(cos(a)*band.x,band.y,sin(a)*band.x))
		for band in range(profile.size()-1):
			for i in range(48):
				var a=band*48+i; var b=band*48+(i+1)%48
				inds.append_array([a,b,a+48,b,b+48,a+48])
		var body=MeshInstance3D.new(); body.mesh=_tri_mesh(verts,inds)
		body.material_override=_mat("b28a51" if index==0 else "d9c9a7"); bowl.add_child(body)
		if index==0:
			_part(bowl,"cylinder",Vector3(0,.07,0),Vector3(.31,.02,.31),"786332")
			for k in range(30):
				var a=rng.randf()*TAU; var r=sqrt(rng.randf())*.28
				_part(bowl,"smooth",Vector3(cos(a)*r,.11+rng.randf()*.04,sin(a)*r),Vector3(.075,.06,.075),["d4a15d","c49350","e7bb78"][k%3])
		else:
			var water=StandardMaterial3D.new(); water.albedo_color=Color("71aeb3"); water.roughness=.2; water.metallic=.12
			var surface=MeshInstance3D.new(); var circle=CylinderMesh.new(); circle.top_radius=.34;circle.bottom_radius=.34;circle.height=.012;circle.radial_segments=48
			surface.mesh=circle;surface.material_override=water;surface.position.y=.125;bowl.add_child(surface)
			for k in range(18):
				var a=k*TAU/18
				_part(bowl,"ball",Vector3(cos(a)*.418,.13,sin(a)*.418),Vector3(.07,.045,.018),"5d929c",Vector3(0,-a+PI/2,0))
		_solid_box(bowl,Vector3(0,.10,0),Vector3(.86,.2,.86))

func _build_edges() -> void:
	# Interrupted fence and planting edges imply a living garden, not a square arena.
	for edge in [0,1]:
		for k in range(11):
			var a=(.08+float(k)/10*.60)*PI+(PI if edge==1 else 0)
			var p=Vector3(cos(a)*11-.5,0,sin(a)*8+.2)
			var q=Vector3(cos(a+.06*PI)*11-.5,0,sin(a+.06*PI)*8+.2)
			if (p.z>6 and p.x>-.8 and p.x<4) or (p.x<-8 and p.z>3):continue
			if _near_house(p) or _near_house(q) or _near_house((p+q)*.5):continue
			_beam(self,p,p+Vector3(0,.86,0),.075,"9c8357")
			for y in [.34,.66]: _beam(self,p+Vector3(0,y,0),q+Vector3(0,y,0),.042,"b29b6b")
	# Expansion portal on the southwest spur. This is a visible proposal, not unlock logic.
	var gate=_root(Vector3(-9,0,5.5)); gate.rotation.y=-.65; gate.name="FutureExpansionGate"
	for x in [-1.5,1.5]:
		_box(gate,Vector3(x,.65,0),Vector3(.18,1.3,.18),"a27f4d",true)
		_part(gate,"ball",Vector3(x,1.31,0),Vector3(.22,.08,.22),"c1a572")
	for y in [.40,.85]: _box(gate,Vector3(0,y,0),Vector3(2.85,.1,.09),"baa174",true)
	var sign=Label3D.new(); sign.text="拓地"; sign.font=load("res://Assets/Fonts/NotoSansSC.ttf")
	sign.font_size=48;sign.pixel_size=.006;sign.position=Vector3(0,.85,.075);sign.modulate=Color("4c5435");sign.outline_size=0;gate.add_child(sign)
	_box(gate,Vector3(0,.85,.03),Vector3(1.05,.46,.06),"d3ba87",true)
	for k in range(35):
		var a=rng.randf()*TAU;var r=rng.randf_range(11.7,14)
		var p=Vector3(cos(a)*r-.5,.05,sin(a)*r*.72)
		if _near_house(p) or _path_distance(Vector2(p.x,p.z))<1.3:continue
		_part(self,"ball",p,Vector3(rng.randf_range(.4,1),rng.randf_range(.22,.65),rng.randf_range(.4,.8)),["a7aa92","b8b69f","949c88"][k%3])

func _near_house(p: Vector3) -> bool:
	return abs(p.x+7)<3.8 and abs(p.z+6)<3.4

func _path_distance(p: Vector2) -> float:
	var ring=abs(((p-Vector2(-.5,.2))/Vector2(9,6.6)).length()-1)*6.6
	for segment in [[Vector2(-7,-3.2),Vector2(-7,1)],[Vector2(-8,3.9),Vector2(-13,8.6)],[Vector2(1,6.6),Vector2(2,13)]]:
		var a: Vector2=segment[0]; var b: Vector2=segment[1]
		var ab=b-a; ring=min(ring,p.distance_to(a+ab*clamp((p-a).dot(ab)/ab.length_squared(),0,1)))
	return ring

func _clear_ground(p: Vector2) -> bool:
	if abs(p.x+7)<3.6 and p.y>-9.2 and p.y<-.6:return false
	if p.distance_to(Vector2(4,-3))<1.65:return false
	for bowl in [Vector2(-5.5,.6),Vector2(-3,.6)]:
		if p.distance_to(bowl)<1.1:return false
	return _path_distance(p)>.95

func _flower(pos: Vector3,color: String,scale_factor: float=1) -> void:
	var height=rng.randf_range(.18,.42)*scale_factor
	_beam(self,pos,pos+Vector3(.03,height,0),.009,"6b8246",false)
	for leaf_side in [-1,1]:
		_part(self,"leaf",pos+Vector3(leaf_side*.07,height*.45,0),Vector3(.21,.6,.55)*scale_factor,"779347",Vector3(0,leaf_side*.3,.6))
	for petal in range(5):
		var a=petal*TAU/5; var center=pos+Vector3(cos(a)*.065,height,sin(a)*.065)*Vector3(scale_factor,1,scale_factor)
		_part(self,"ball",center,Vector3(.11,.026,.065)*scale_factor,color,Vector3(0,-a,0))
	_part(self,"ball",pos+Vector3(0,height+.017,0),Vector3(.055,.035,.055)*scale_factor,"ddbd61")

func _build_plants() -> void:
	# Grass is instanced geometry; main traffic remains visibly short and clear.
	for k in range(14000):
		var p=Vector2(rng.randf_range(-17,17),rng.randf_range(-13,14))
		var edge=(p/Vector2(17,13)).length()
		if rng.randf()>clamp((1.15-edge)*4,0,1):continue
		if not _clear_ground(p):continue
		var inner=(p/Vector2(10,8)).length()<.8
		if inner and rng.randf()<.83:continue
		var s=rng.randf_range(.25,.5) if inner else rng.randf_range(.35,.75)
		_part(self,"blade",Vector3(p.x,.005,p.y),Vector3(s,s,s),["819655","8c9e60","9aab6d","758c4b"][k%4],Vector3(0,rng.randf()*TAU,0))
	# Curated clumps along boundaries; avoid random flowers filling the play space.
	var centers=[Vector2(-11,-6),Vector2(-10,-1),Vector2(-9.5,3),Vector2(-7.5,6),Vector2(7,-7),Vector2(10,-1),Vector2(9,4),Vector2(5,7.5),Vector2(-3,-9)]
	for j in range(centers.size()):
		for k in range(65):
			var p=centers[j]+Vector2(rng.randfn(0,.7),rng.randfn(0,.55))
			if not _clear_ground(p):continue
			_flower(Vector3(p.x,.015,p.y),["d7c4df","dfafbc","ede7c8","b1bbd5"][j%4],rng.randf_range(.8,1.5))
	# Hydrangeas around the cottage: leafy base and many small blooms.
	for center in [Vector3(-11.4,0,-3.7),Vector3(-2.7,0,-5.7),Vector3(-11,0,1),Vector3(9,0,3.2)]:
		for k in range(70):
			var a=rng.randf()*TAU;var r=sqrt(rng.randf())*.75
			_part(self,"leaf",center+Vector3(cos(a)*r,rng.randf_range(.12,.65),sin(a)*r),Vector3.ONE*.45,"63834b",Vector3(0,a,.3))
		for k in range(10):
			var a=k*TAU/10; var head=center+Vector3(cos(a)*.5,.65+sin(k)*.16,sin(a)*.5)
			for n in range(12):
				var direction=Vector3(rng.randf_range(-1,1),rng.randf_range(0,1),rng.randf_range(-1,1)).normalized()
				_part(self,"ball",head+direction*.15,Vector3(.12,.07,.12),["a3a9c8","b9bdd8","8f9cbf","d1c2db"][n%4])
	# Lower shrubs build a soft silhouette behind the house and flower borders.
	for center in [Vector3(-12,.65,-6.6),Vector3(-9,.65,-10.5),Vector3(-4.1,.65,-10.5),Vector3(8.8,.4,-5.8)]:
		for k in range(9):
			_foliage(self,center+Vector3(rng.randf_range(-.65,.65),rng.randf_range(0,.6),rng.randf_range(-.5,.5)),rng.randf_range(1.1,1.7))
	# Sparse low clover and daisies keep the open play lawn alive without obstacles.
	for k in range(280):
		var p=Vector2(rng.randf_range(-8,8),rng.randf_range(-3,6.5))
		if not _clear_ground(p):continue
		for j in range(3):
			var a=j*TAU/3
			_part(self,"ball",Vector3(p.x+cos(a)*.055,.027,p.y+sin(a)*.055),Vector3(.11,.016,.08),["8b9e62","839958","a0b074"][j],Vector3(0,-a,0))
		if k%8==0:_flower(Vector3(p.x,0,p.y),"e7dfc6",.55)

func _build_characters() -> void:
	# Scale stand-ins only; existing production CreatureActor is not duplicated or changed.
	for i in range(3):
		var positions=[Vector3(-5.4,.46,2),Vector3(-4,.46,-1.8),Vector3(1,.46,2.8)]
		var c=_root(positions[i]);c.name="ScaleStandIn_"+str(i)
		var blonde=i==1;var hair="c7a35c" if blonde else "4b3831"
		_part(c,"smooth",Vector3(0,.05,-.07),Vector3(1.06,.94,.94),hair)
		_part(c,"smooth",Vector3(0,-.07,.14),Vector3(.98,.78,.88),"f6dfb8")
		for k in range(7):
			var x=(k-3)*.13
			_part(c,"smooth",Vector3(x,.26-abs(x)*.15,.35),Vector3(.22,.31,.19),hair,Vector3(0,0,x*.4))
		for side in [-1,1]:
			_part(c,"smooth",Vector3(side*.46,-.09,.14),Vector3(.21,.54,.30),hair)
			_part(c,"smooth",Vector3(side*.21,-.08,.54),Vector3(.10,.14,.025),"49392e")
			_part(c,"smooth",Vector3(side*.20,-.045,.555),Vector3(.025,.035,.012),"fff6da")
			_part(c,"smooth",Vector3(side*.32,-.19,.51),Vector3(.14,.045,.014),"d89b87")
		_part(c,"smooth",Vector3(0,-.23,.568),Vector3(.10,.045,.018),"a4614c")
		if blonde:
			_part(c,"cylinder",Vector3(0,.48,-.02),Vector3(.77,.045,.66),"393735",Vector3(.04,0,-.06))
			_part(c,"cone",Vector3(-.04,.79,-.08),Vector3(.44,.65,.4),"3d3a38",Vector3(0,0,.12))
			for side in [-1,1]: _part(c,"smooth",Vector3(side*.21,.65,.39),Vector3(.45,.24,.06),"e2d7bc",Vector3(0,0,side*.3))
		else:
			for side in [-1,1]:
				_part(c,"smooth",Vector3(side*.37,.45,-.05),Vector3(.65,.36,.13),"a94f49",Vector3(0,0,side*.25))
				_part(c,"smooth",Vector3(side*.43,.47,.02),Vector3(.48,.24,.018),"d3a995",Vector3(0,0,side*.25))
			_part(c,"smooth",Vector3(0,.42,.02),Vector3(.21,.21,.17),"b1534b")

func _setup_overlay() -> void:
	var layer=CanvasLayer.new();add_child(layer)
	var title=Label.new(); title.position=Vector2(32,25)
	title.text="悠悠桃源  /  初始庭院 · 实机美术样板"
	title.add_theme_font_override("font",load("res://Assets/Fonts/NotoSansSC.ttf"))
	title.add_theme_font_size_override("font_size",23);title.add_theme_color_override("font_color",Color("fff3d8"))
	title.add_theme_color_override("font_shadow_color",Color("425143"));title.add_theme_constant_override("shadow_offset_x",1);title.add_theme_constant_override("shadow_offset_y",2)
	layer.add_child(title)
	if output.is_empty():
		var hint=Label.new();hint.position=Vector2(32,62);hint.text="F1 全景   F2 小屋   F3 食水角   F4 屋内   F5 俯视   R 屋顶   Esc 退出"
		hint.add_theme_font_override("font",load("res://Assets/Fonts/NotoSansSC.ttf"));hint.add_theme_font_size_override("font_size",16);layer.add_child(hint)

func _set_view(index: int) -> void:
	var view=views[index]; camera.size=view.size; camera.position=view.focus+view.offset
	camera.look_at(view.focus,Vector3.UP); roof.visible=index!=3

func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:return
	if event.keycode>=KEY_F1 and event.keycode<=KEY_F5: _set_view(event.keycode-KEY_F1)
	if event.keycode==KEY_R:roof.visible=not roof.visible
	if event.keycode==KEY_ESCAPE:get_tree().quit()

func _capture_all() -> void:
	DirAccess.make_dir_recursive_absolute(output)
	await get_tree().physics_frame
	await get_tree().physics_frame
	if not _verify_geometry():
		var failed=FileAccess.open(output.path_join("geometry-failure.json"),FileAccess.WRITE)
		failed.store_string(JSON.stringify(geometry_checks,"\t"));failed.close()
		push_error("Starter art geometry check failed");get_tree().quit(2);return
	var files: Array[String]=[]
	for i in range(views.size()):
		_set_view(i)
		for frame in range(35): await get_tree().process_frame;capture_frames+=1
		await RenderingServer.frame_post_draw
		var picture=get_viewport().get_texture().get_image()
		var path=output.path_join(views[i].name+".png")
		var error=picture.save_png(path)
		if error!=OK:push_error("Screenshot failed: "+path);get_tree().quit(2);return
		files.append(path)
		print("STARTER_ART_CAPTURE ",path," ",picture.get_size())
	var render_size=get_viewport().get_texture().get_size()
	var report={"status":"rendered", "engine":Engine.get_version_info().string,"renderer":RenderingServer.get_current_rendering_method(),"gpu":RenderingServer.get_video_adapter_name(),"seed":2026110304,"viewport":[int(render_size.x),int(render_size.y)],"window":[get_window().size.x,get_window().size.y],"pieces":item_count,"batches":render_groups,"capture_frames":capture_frames,"elapsed_ms":Time.get_ticks_msec()-start_ticks,"files":files,"geometry_checks":geometry_checks,"scope":"Independent real 3D art sample. Procedural geometry/materials plus imagegen foliage texture. Scale stand-ins, no production character substitution, simulation, saves, economic unlocks or navigation acceptance. Short capture only, not a performance or gameplay test."}
	var report_file=FileAccess.open(output.path_join("render-report.json"),FileAccess.WRITE)
	report_file.store_string(JSON.stringify(report,"\t"));report_file.close()
	print("STARTER_ART_CAPTURE_COMPLETE")
	get_tree().quit(0)

func _verify_geometry() -> bool:
	var space=get_world_3d().direct_space_state
	for k in range(5):
		var z=-3.1+float(k)*.55
		var hit=space.intersect_ray(PhysicsRayQueryParameters3D.create(Vector3(-7,1,z),Vector3(-7,-1,z)))
		var expected=.2*(-.8-z)/2.4
		var ok=not hit.is_empty() and abs(hit.position.y-expected)<.025
		geometry_checks.append({"name":"ramp_height_"+str(k),"passed":ok,"expected_y":expected,"actual_y":hit.position.y if not hit.is_empty() else null})
	var doorway=space.intersect_ray(PhysicsRayQueryParameters3D.create(Vector3(-7,.8,-1),Vector3(-7,.8,-4)))
	geometry_checks.append({"name":"doorway_center_clear","passed":doorway.is_empty()})
	var wall=space.intersect_ray(PhysicsRayQueryParameters3D.create(Vector3(-4.5,.8,-4),Vector3(-3.5,.8,-4)))
	geometry_checks.append({"name":"side_wall_solid","passed":not wall.is_empty()})
	var tree=space.intersect_ray(PhysicsRayQueryParameters3D.create(Vector3(4,1,-5),Vector3(4,1,-1)))
	geometry_checks.append({"name":"tree_trunk_solid","passed":not tree.is_empty()})
	for check in geometry_checks:
		if not check.passed:return false
	return true
