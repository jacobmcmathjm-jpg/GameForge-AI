extends Node2D

var game: Node2D
var font: Font = ThemeDB.fallback_font
const LIGHT = Color("e8efde")
const MUTED = Color("a2b5b8")
const GOLD = Color("f3cb87")
const TEAL = Color("8ed8c6")

func text_line(value: String, pos: Vector2, size: int, color: Color = LIGHT) -> void:
	draw_string(font, pos, value, HORIZONTAL_ALIGNMENT_LEFT, -1, size, color)

func centered(value: String, y: float, size: int, color: Color = LIGHT) -> void:
	var width: float = font.get_string_size(value, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x
	text_line(value, Vector2((1280 - width) / 2, y), size, color)

func fitted(value: String, pos: Vector2, size: int, max_width: float, color: Color = LIGHT) -> void:
	while size > 12 and font.get_string_size(value, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x > max_width:
		size -= 1
	text_line(value, pos, size, color)

func box(rect: Rect2, color: Color, radius: int = 8, border: Color = Color.TRANSPARENT) -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(radius)
	style.border_color = border
	style.set_border_width_all(1)
	draw_style_box(style, rect)

func button(label: String, rect: Rect2, primary: bool = false) -> void:
	var hovered: bool = rect.has_point(get_global_mouse_position())
	var color := Color("a8e4cd") if primary else Color("233b49")
	if hovered:
		color = color.lightened(0.12)
	box(rect, color, 7, Color("6b9e9b") if not primary else Color.TRANSPARENT)
	var size := 18
	var width: float = font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x
	text_line(label, Vector2(rect.get_center().x - width / 2, rect.position.y + 31), size, Color("102c34") if primary else LIGHT)

func _draw() -> void:
	if game == null or game.brief == null:
		return
	if game.state == "menu":
		draw_rect(Rect2(0, 0, 1280, 720), Color(0.035, 0.07, 0.13, 0.65))
		text_line("GAMEFORGE  /  ORIGINAL INDIE SERIES", Vector2(76, 68), 15, TEAL)
		text_line("01  /  THE LANTERN KEEPER", Vector2(76, 180), 17, GOLD)
		fitted(str(game.brief["title"]).to_upper(), Vector2(72, 277), 76, 770)
		fitted(str(game.brief["tagline"]), Vector2(78, 323), 26, 770, TEAL)
		text_line("Three forgotten valleys. Eighteen scattered lights.", Vector2(78, 380), 20, MUTED)
		text_line("Find the shards, outrun the shadows, wake the beacons.", Vector2(78, 411), 20, MUTED)
		button("Begin journey   /   Enter", Rect2(78, 465, 286, 54), true)
		button("Sound: " + ("off" if game.muted else "on"), Rect2(380, 465, 150, 54))
		text_line("MOVE  A / D       JUMP  SPACE       DASH  SHIFT", Vector2(78, 574), 15, MUTED)
		text_line("Dash through moths. Light posts restore health and save your place.", Vector2(78, 603), 15, MUTED)
		# Large lantern emblem balances the title without covering the landscape.
		var p := Vector2(985, 369)
		for radius in [145, 110, 80]:
			draw_circle(p, radius, Color(0.95, 0.72, 0.35, 0.025))
		draw_arc(p + Vector2(0, -60), 28, PI, TAU, 24, GOLD, 4)
		draw_colored_polygon(PackedVector2Array([p + Vector2(-40, -62), p + Vector2(40, -62), p + Vector2(30, 64), p + Vector2(-30, 64)]), Color("233b46"))
		draw_polyline(PackedVector2Array([p + Vector2(-40, -62), p + Vector2(40, -62), p + Vector2(30, 64), p + Vector2(-30, 64), p + Vector2(-40, -62)]), GOLD, 3, true)
		draw_colored_polygon(PackedVector2Array([p + Vector2(0, -35), p + Vector2(14, 2), p + Vector2(0, 37), p + Vector2(-14, 2)]), Color("ffdf9c"))
		text_line("A small adventure, made by GameForge", Vector2(78, 678), 13, Color("70888f"))
		return
	# Compact gameplay HUD.
	box(Rect2(24, 22, 308, 76), Color(0.055, 0.11, 0.16, 0.94), 10, Color("35545c"))
	fitted(str(game.brief["title"]).to_upper(), Vector2(42, 49), 14, 270, TEAL)
	for i in range(3):
		var x: float = 49 + i * 28
		draw_circle(Vector2(x - 3, 73), 6, Color("f0a08d") if i < game.health else Color("344d58"))
		draw_circle(Vector2(x + 3, 73), 6, Color("f0a08d") if i < game.health else Color("344d58"))
		draw_colored_polygon(PackedVector2Array([Vector2(x - 9, 73), Vector2(x + 9, 73), Vector2(x, 84)]), Color("f0a08d") if i < game.health else Color("344d58"))
	text_line("LIGHT  %d / %d" % [game.shards, game.pickups.size()], Vector2(153, 79), 19, GOLD)
	box(Rect2(1060, 22, 196, 76), Color(0.055, 0.11, 0.16, 0.94), 10, Color("35545c"))
	text_line("CHAPTER  %02d / 03" % (game.chapter + 1), Vector2(1078, 49), 14, MUTED)
	text_line("%05d" % game.score, Vector2(1078, 81), 25)
	box(Rect2(24, 654, 340, 42), Color(0.055, 0.11, 0.16, 0.9), 7)
	text_line("SHIFT  DASH", Vector2(41, 681), 14, TEAL)
	var ready: float = 1.0 - clampf(game.player.dash_cooldown / 0.85, 0, 1)
	draw_rect(Rect2(160, 671, 95, 6), Color("36545b"))
	draw_rect(Rect2(160, 671, 95 * ready, 6), TEAL)
	text_line("ESC  Pause", Vector2(1100, 680), 14, MUTED)
	if game.notice_time > 0:
		box(Rect2(360, 28, 560, 46), Color(0.055, 0.11, 0.16, 0.88), 8)
		centered(game.notice, 58, 18, GOLD)
	if game.state in ["paused", "lost", "won"]:
		draw_rect(Rect2(0, 0, 1280, 720), Color(0.035, 0.07, 0.13, 0.84))
		var heading: String = {"paused": "A MOMENT OF QUIET", "lost": "THE LIGHT FADES", "won": "THE VALLEYS SHINE AGAIN"}[game.state]
		centered(heading, 256, 42)
		var subtitle: String = {"paused": "Your journey will be here when you return.", "lost": "Every keeper begins again. The path is yours to learn.", "won": "Eighteen lights carried home. A little darkness lifted."}[game.state]
		centered(subtitle, 305, 20, MUTED)
		centered("SCORE  %05d    /    TIME  %02d:%02d" % [game.score, int(game.elapsed) / 60, int(game.elapsed) % 60], 354, 19, GOLD)
		button("Continue" if game.state == "paused" else "Begin again", Rect2(490, 405, 300, 52), true)
		button("Return to title", Rect2(490, 473, 300, 50))
		centered("M  Sound " + ("off" if game.muted else "on") + "     /     R  Restart", 581, 15, MUTED)

func click(point: Vector2) -> void:
	if game.state == "menu":
		if Rect2(78, 465, 286, 54).has_point(point):
			game.start_game()
		elif Rect2(380, 465, 150, 54).has_point(point):
			game.toggle_mute()
	elif game.state in ["paused", "lost", "won"]:
		if Rect2(490, 405, 300, 52).has_point(point):
			if game.state == "paused":
				game.state = "playing"
			else:
				game.start_game()
		elif Rect2(490, 473, 300, 50).has_point(point):
			game.state = "menu"
