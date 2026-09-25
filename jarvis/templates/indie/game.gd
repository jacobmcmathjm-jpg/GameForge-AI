extends Node2D

const Player = preload("res://scripts/player.gd")
const Interface = preload("res://scripts/interface.gd")
var brief: Dictionary
var levels: Array
var state: String = "menu"
var player: CharacterBody2D
var camera: Camera2D
var world: Node2D
var ui: Node2D
var chapter: int = 0
var health: int = 3
var score: int = 0
var shards: int = 0
var deaths: int = 0
var elapsed: float = 0
var clock: float = 0
var checkpoint := Vector2(90, 510)
var checkpoint_index: int = -1
var platforms: Array = []
var pickups: Array = []
var enemies: Array = []
var checkpoints: Array = []
var particles: Array = []
var portal := Vector2.ZERO
var level_width: float = 3200
var shake: float = 0
var notice: String = ""
var notice_time: float = 0
var muted: bool = false
var sounds: Dictionary = {}
var music: AudioStreamPlayer
var capture_path: String = ""
var capture_mode: String = "menu"

func _ready() -> void:
	brief = JSON.parse_string(FileAccess.get_file_as_string("res://game_data.json"))
	levels = brief["levels"]
	RenderingServer.set_default_clear_color(Color("101d30"))
	setup_input()
	world = Node2D.new()
	add_child(world)
	camera = Camera2D.new()
	add_child(camera)
	camera.position = Vector2(640, 360)
	var canvas := CanvasLayer.new()
	add_child(canvas)
	ui = Interface.new()
	ui.game = self
	canvas.add_child(ui)
	for key in ["jump", "dash", "pickup", "hurt", "win", "checkpoint"]:
		var audio := AudioStreamPlayer.new()
		audio.stream = load("res://audio/" + key + ".wav")
		audio.volume_db = -11
		add_child(audio)
		sounds[key] = audio
	music = AudioStreamPlayer.new()
	var stream: AudioStreamWAV = load("res://audio/ambience.wav")
	stream.loop_mode = AudioStreamWAV.LOOP_FORWARD
	stream.loop_end = stream.data.size() / 2
	music.stream = stream
	music.volume_db = -20
	add_child(music)
	if DisplayServer.get_name() != "headless":
		music.play()
	load_level(0)
	state = "menu"
	var args := OS.get_cmdline_user_args()
	for i in range(args.size()):
		if args[i] == "--capture" and i + 1 < args.size():
			capture_path = args[i + 1]
		if args[i] == "--capture-mode" and i + 1 < args.size():
			capture_mode = args[i + 1]
	if not capture_path.is_empty():
		if capture_mode == "play":
			start_game()
			player.position = Vector2(platforms[1][0] + 130, platforms[1][1] - 30)
		await get_tree().create_timer(0.8).timeout
		await RenderingServer.frame_post_draw
		get_viewport().get_texture().get_image().save_png(capture_path)
		get_tree().quit()

func setup_input() -> void:
	var bindings := {"left": [KEY_A, KEY_LEFT], "right": [KEY_D, KEY_RIGHT], "jump": [KEY_SPACE, KEY_W, KEY_UP], "dash": [KEY_SHIFT, KEY_X], "pause_game": [KEY_ESCAPE], "restart": [KEY_R], "mute": [KEY_M], "confirm": [KEY_ENTER]}
	for action in bindings:
		if not InputMap.has_action(action):
			InputMap.add_action(action)
		for key in bindings[action]:
			var event := InputEventKey.new()
			event.physical_keycode = key
			InputMap.action_add_event(action, event)

func _exit_tree() -> void:
	if is_instance_valid(music):
		music.stop()
	for audio in sounds.values():
		if is_instance_valid(audio):
			audio.stop()

func load_level(index: int) -> void:
	for child in world.get_children():
		world.remove_child(child)
		child.queue_free()
	chapter = index
	var level: Dictionary = levels[index]
	platforms = level["platforms"].duplicate(true)
	pickups = level["shards"].duplicate(true)
	enemies = level["enemies"].duplicate(true)
	checkpoints = level["checkpoints"].duplicate(true)
	level_width = float(level["width"])
	portal = Vector2(level["portal"][0], level["portal"][1])
	checkpoint = Vector2(90, float(platforms[0][1]) - 30)
	checkpoint_index = -1
	shards = 0
	particles.clear()
	for p in platforms:
		var body := StaticBody2D.new()
		body.position = Vector2(p[0] + p[2] / 2.0, p[1] + 150)
		var collision := CollisionShape2D.new()
		var shape := RectangleShape2D.new()
		shape.size = Vector2(p[2], 300)
		collision.shape = shape
		body.add_child(collision)
		world.add_child(body)
	player = Player.new()
	player.game = self
	player.position = checkpoint
	world.add_child(player)
	camera.position = Vector2(640, 360)
	health = 3
	notice = str(level["name"])
	notice_time = 3.0

func start_game() -> void:
	score = 0
	deaths = 0
	elapsed = 0
	load_level(0)
	state = "playing"

func toggle_pause() -> void:
	if state == "playing":
		state = "paused"
	elif state == "paused":
		state = "playing"

func toggle_mute() -> void:
	muted = not muted
	AudioServer.set_bus_mute(0, muted)

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("mute"):
		toggle_mute()
	if event.is_action_pressed("pause_game"):
		toggle_pause()
	if event.is_action_pressed("restart") and state in ["playing", "paused", "lost", "won"]:
		start_game()
	if event.is_action_pressed("confirm"):
		if state in ["menu", "lost", "won"]:
			start_game()
		elif state == "paused":
			state = "playing"
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		ui.click(event.position)

func _physics_process(delta: float) -> void:
	clock += delta
	if state == "playing":
		elapsed += delta
		notice_time = maxf(0, notice_time - delta)
		for pickup in pickups:
			if not pickup["taken"] and player.position.distance_to(Vector2(pickup["x"], pickup["y"])) < 33:
				collect(pickup)
		for i in range(checkpoints.size()):
			var point := Vector2(checkpoints[i][0], checkpoints[i][1])
			if i > checkpoint_index and absf(player.position.x - point.x) < 32 and absf(player.position.y - point.y) < 65:
				checkpoint_index = i
				checkpoint = point
				health = 3
				notice = "Light restored  /  checkpoint saved"
				notice_time = 2.5
				sound("checkpoint")
				burst(point, Color("77dacb"), 20)
		for enemy in enemies:
			if not enemy["alive"]:
				continue
			enemy["x"] = float(enemy["origin"]) + sin(clock * float(enemy["speed"])) * float(enemy["range"])
			var position_enemy := Vector2(enemy["x"], enemy["y"] + sin(clock * 3) * 4)
			if player.position.distance_to(position_enemy) < 30:
				if player.dash_left > 0 or (player.velocity.y > 40 and player.position.y < position_enemy.y - 12):
					enemy["alive"] = false
					player.velocity.y = -320
					score += 75
					burst(position_enemy, Color("e79490"), 20)
					sound("dash")
				else:
					hurt()
		if player.position.distance_to(portal) < 48:
			try_exit()
		var target := Vector2(clampf(player.position.x + 90, 640, level_width - 640), 360)
		camera.position = camera.position.lerp(target, minf(1, delta * 6))
	shake = maxf(0, shake - delta * 24)
	camera.offset = Vector2(sin(clock * 75), cos(clock * 90)) * shake
	for p in particles:
		p["life"] -= delta
		p["position"] += p["velocity"] * delta
		p["velocity"].y += delta * 80
	particles = particles.filter(func(p): return p["life"] > 0)
	queue_redraw()
	ui.queue_redraw()

func collect(pickup: Dictionary) -> void:
	if pickup["taken"]:
		return
	pickup["taken"] = true
	shards += 1
	score += 100
	sound("pickup")
	burst(Vector2(pickup["x"], pickup["y"]), Color("f8d38c"), 14)
	if shards == pickups.size():
		notice = "The beacon is awake. Reach the arch."
		notice_time = 4

func hurt(falling: bool = false) -> void:
	if state != "playing" or (player.invulnerable > 0 and not falling):
		return
	health -= 1
	deaths += 1
	shake = 7
	sound("hurt")
	if health <= 0:
		state = "lost"
		return
	player.position = checkpoint
	player.velocity = Vector2.ZERO
	player.dash_left = 0
	player.invulnerable = 1.5
	player.trail.clear()

func try_exit() -> void:
	if state != "playing" or shards < pickups.size():
		return
	score += 250
	sound("win")
	if chapter + 1 < levels.size():
		load_level(chapter + 1)
	else:
		state = "won"
		burst(portal, Color("a9ead9"), 40)

func sound(key: String) -> void:
	if sounds.has(key) and not muted and DisplayServer.get_name() != "headless":
		sounds[key].play()

func burst(point: Vector2, color: Color, count: int) -> void:
	for i in range(count):
		var angle: float = TAU * float(i) / count
		particles.append({"position": point, "velocity": Vector2(cos(angle), sin(angle)) * (45 + i % 5 * 25), "life": 0.8, "color": color})

func _draw() -> void:
	if camera == null:
		return
	var left: float = camera.position.x - 640
	# An original layered dusk landscape, drawn from geometry; no downloaded assets.
	draw_rect(Rect2(left - 20, -20, 1320, 780), Color("101d30"))
	for i in range(85):
		var sx: float = fposmod(i * 179.3 - left * 0.07, 1350) + left - 30
		var sy: float = fposmod(i * 77.1, 390)
		draw_circle(Vector2(sx, sy), 0.8 + i % 2 * 0.4, Color(0.64, 0.82, 0.83, 0.25 + sin(clock + i) * 0.16))
	var moon := Vector2(left + 975 - left * 0.025, 137)
	for radius in [105, 76, 52]:
		draw_circle(moon, radius, Color(0.72, 0.83, 0.7, 0.025))
	draw_circle(moon, 32, Color("c4d9c0"))
	draw_circle(moon + Vector2(12, -8), 29, Color("162737"))
	for layer in range(3):
		var mountain := PackedVector2Array([Vector2(left - 80, 780)])
		for i in range(17):
			var px: float = left - 100 + i * 105
			var py: float = 305 + layer * 92 - sin((px - left * (0.85 - layer * 0.1)) * 0.006 + layer * 2) * (90 - layer * 15)
			mountain.append(Vector2(px, py))
		mountain.append(Vector2(left + 1560, 780))
		draw_colored_polygon(mountain, [Color("193446"), Color("214452"), Color("28535d")][layer])
	for i in range(14):
		var tx: float = left + fposmod(i * 233.0 - left * 0.32, 1600) - 130
		var ty: float = 550 + sin(i * 9) * 20
		draw_line(Vector2(tx, ty), Vector2(tx + 8, ty - 135 - i % 3 * 24), Color("22434c"), 9)
		draw_line(Vector2(tx + 6, ty - 80), Vector2(tx - 30, ty - 110), Color("22434c"), 5)
	for p in platforms:
		var rect := Rect2(p[0], p[1], p[2], 300)
		draw_rect(rect, Color("152f3b"))
		draw_rect(Rect2(p[0], p[1] + 8, p[2], 35), Color("2f5357"))
		draw_rect(Rect2(p[0], p[1], p[2], 7), Color("86b9a0"))
		draw_line(Vector2(p[0], p[1] + 2), Vector2(p[0] + p[2], p[1] + 2), Color("b6cfaa"), 2)
		for i in range(int(p[2] / 34)):
			var gx: float = p[0] + 10 + i * 34
			draw_line(Vector2(gx, p[1]), Vector2(gx - 3, p[1] - 7 - i % 3 * 2), Color("7da98b"), 2)
			if i % 3 == 0:
				draw_rect(Rect2(gx, p[1] + 55 + i % 4 * 18, 18, 5), Color("284650"))
	for i in range(checkpoints.size()):
		var point := Vector2(checkpoints[i][0], checkpoints[i][1])
		var lit: bool = i <= checkpoint_index
		draw_line(point + Vector2(0, 16), point + Vector2(0, -40), Color("8b9c91"), 5)
		draw_rect(Rect2(point + Vector2(-9, -54), Vector2(18, 21)), Color("203641"))
		draw_circle(point + Vector2(0, -44), 5, Color("f8d38c") if lit else Color("677e79"))
		if lit:
			draw_circle(point + Vector2(0, -44), 30, Color(1, 0.8, 0.45, 0.07))
	for pickup in pickups:
		if pickup["taken"]:
			continue
		var point := Vector2(pickup["x"], pickup["y"] + sin(clock * 2.5 + pickup["x"]) * 5)
		draw_circle(point, 26, Color(1, 0.75, 0.35, 0.06))
		draw_circle(point, 15, Color(1, 0.8, 0.4, 0.10))
		draw_colored_polygon(PackedVector2Array([point + Vector2(0, -11), point + Vector2(7, 0), point + Vector2(0, 11), point + Vector2(-7, 0)]), Color("ffda8c"))
		draw_line(point + Vector2(0, -7), point + Vector2(0, 5), Color("fff4d6"), 2)
	for enemy in enemies:
		if not enemy["alive"]:
			continue
		var point := Vector2(enemy["x"], enemy["y"] + sin(clock * 3) * 4)
		var wing: float = sin(clock * 11) * 6
		draw_circle(point, 24, Color(0.9, 0.3, 0.4, 0.08))
		draw_colored_polygon(PackedVector2Array([point + Vector2(-7, 0), point + Vector2(-29, -15 + wing), point + Vector2(-22, 9), point + Vector2(0, 8)]), Color("b87986"))
		draw_colored_polygon(PackedVector2Array([point + Vector2(7, 0), point + Vector2(29, -15 + wing), point + Vector2(22, 9), point + Vector2(0, 8)]), Color("b87986"))
		draw_circle(point, 12, Color("493c54"))
		draw_circle(point + Vector2(-4, -2), 2, Color("ffd0b4"))
		draw_circle(point + Vector2(4, -2), 2, Color("ffd0b4"))
	var ready: bool = shards == pickups.size()
	var gate_color := Color("9de2c9") if ready else Color("6c8790")
	draw_rect(Rect2(portal + Vector2(-32, -50), Vector2(10, 72)), Color("526f76"))
	draw_rect(Rect2(portal + Vector2(23, -50), Vector2(10, 72)), Color("526f76"))
	draw_arc(portal + Vector2(0, -48), 28, PI, TAU, 24, gate_color, 10)
	if ready:
		for i in range(5):
			draw_circle(portal + Vector2(0, -15), 25 + i * 7, Color(0.5, 1, 0.8, 0.035))
		draw_line(portal + Vector2(0, -44), portal + Vector2(0, 15), Color("a4f2d8"), 3)
	for p in particles:
		var color: Color = p["color"]
		color.a = minf(1, p["life"] * 2)
		draw_circle(p["position"], 3, color)
	for i in range(28):
		var firefly := Vector2(left + fposmod(i * 149.7 + clock * (3 + i % 3) - left * 0.6, 1300), 380 + sin(i * 2 + clock * 0.4) * 130)
		draw_circle(firefly, 1.6, Color(0.8, 0.95, 0.7, 0.22 + sin(clock * 2 + i) * 0.18))
