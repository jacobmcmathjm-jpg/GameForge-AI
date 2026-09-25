extends CharacterBody2D

var game: Node2D
var facing: float = 1.0
var coyote: float = 0.0
var jump_buffer: float = 0.0
var dash_left: float = 0.0
var dash_cooldown: float = 0.0
var invulnerable: float = 0.0
var trail: Array[Vector2] = []

func _ready() -> void:
	var shape := CollisionShape2D.new()
	var rectangle := RectangleShape2D.new()
	rectangle.size = Vector2(22, 36)
	shape.shape = rectangle
	add_child(shape)
	floor_snap_length = 8.0

func _physics_process(delta: float) -> void:
	if game == null or game.state != "playing":
		return
	coyote = 0.12 if is_on_floor() else maxf(0, coyote - delta)
	jump_buffer = maxf(0, jump_buffer - delta)
	dash_cooldown = maxf(0, dash_cooldown - delta)
	invulnerable = maxf(0, invulnerable - delta)
	if Input.is_action_just_pressed("jump"):
		jump_buffer = 0.14
	var direction := Input.get_axis("left", "right")
	if direction != 0:
		facing = direction
	if Input.is_action_just_pressed("dash") and dash_cooldown <= 0:
		dash_left = 0.18
		dash_cooldown = 0.85
		game.sound("dash")
	if dash_left > 0:
		dash_left -= delta
		velocity = Vector2(facing * 620, -15)
	else:
		velocity.x = move_toward(velocity.x, direction * 265, (2100 if direction != 0 else 1800) * delta)
		velocity.y = minf(velocity.y + 1250 * delta, 850)
		if jump_buffer > 0 and coyote > 0:
			velocity.y = -535
			jump_buffer = 0
			coyote = 0
			game.sound("jump")
		if Input.is_action_just_released("jump") and velocity.y < -200:
			velocity.y = -200
	move_and_slide()
	if position.y > 820:
		game.hurt(true)
	trail.push_front(position)
	if trail.size() > 7:
		trail.pop_back()
	queue_redraw()

func _draw() -> void:
	if invulnerable > 0 and int(invulnerable * 14) % 2 == 0:
		return
	var lean: float = clampf(velocity.x / 265, -1, 1)
	draw_circle(Vector2(0, -2), 35, Color(0.35, 0.9, 0.9, 0.045))
	if dash_left > 0:
		for i in range(1, trail.size()):
			draw_circle(trail[i] - position, 13 - i, Color(0.4, 1, 0.85, 0.18 - i * 0.02))
	# Scarf, boots, coat and a bright lantern give the small silhouette a clear identity.
	draw_colored_polygon(PackedVector2Array([Vector2(-facing * 7, -9), Vector2(-facing * (24 + absf(lean) * 8), -5), Vector2(-facing * 21, 2), Vector2(-facing * 6, -3)]), Color("ee9b63"))
	draw_rect(Rect2(-8, 9, 6, 10), Color("122331"))
	draw_rect(Rect2(3, 9, 6, 10), Color("122331"))
	draw_colored_polygon(PackedVector2Array([Vector2(-10, -6), Vector2(9, -6), Vector2(12, 11), Vector2(-12, 11)]), Color("7bd4ca"))
	draw_circle(Vector2(0, -12), 11, Color("172e40"))
	draw_arc(Vector2(0, -12), 10, PI, TAU, 16, Color("9cddd7"), 3)
	draw_line(Vector2(facing * 1, -12), Vector2(facing * 7, -12), Color("f9e9ba"), 3)
	draw_circle(Vector2(facing * 14, 1), 11, Color(1, 0.72, 0.3, 0.10))
	draw_rect(Rect2(facing * 14 - 3, -3, 6, 9), Color("ffdb89"))
