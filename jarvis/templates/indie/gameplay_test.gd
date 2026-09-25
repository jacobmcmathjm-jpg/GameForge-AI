extends SceneTree

var checks: Array = []
var game: Node2D

func _initialize() -> void:
	call_deferred("run")

func verify(label: String, passed: bool) -> void:
	checks.append({"name": label, "passed": passed})
	print(("PASS " if passed else "FAIL ") + label)

func frames(count: int) -> void:
	for i in range(count):
		await physics_frame

func run() -> void:
	game = load("res://Main.tscn").instantiate()
	root.add_child(game)
	await frames(3)
	verify("title menu loads", game.state == "menu")
	game.start_game()
	await frames(45)
	verify("player lands on solid ground", game.player.is_on_floor())
	var start_x: float = game.player.position.x
	Input.action_press("right")
	await frames(20)
	Input.action_release("right")
	verify("movement responds to input", game.player.position.x > start_x + 35)
	Input.action_press("jump")
	await frames(3)
	verify("jump leaves the ground", game.player.velocity.y < -300 and not game.player.is_on_floor())
	Input.action_release("jump")
	Input.action_press("dash")
	await frames(2)
	verify("dash accelerates with cooldown", absf(game.player.velocity.x) > 500 and game.player.dash_cooldown > 0)
	Input.action_release("dash")
	game.toggle_pause()
	var paused_position: Vector2 = game.player.position
	await frames(15)
	verify("pause freezes player", game.player.position == paused_position and game.state == "paused")
	game.toggle_pause()
	game.toggle_mute()
	verify("mute silences master bus", AudioServer.is_bus_mute(0))
	game.toggle_mute()
	game.start_game()
	await frames(3)
	game.player.position = game.portal
	game.try_exit()
	verify("beacon requires all shards", game.chapter == 0 and game.state == "playing")
	game.player.position = Vector2(game.pickups[0]["x"], game.pickups[0]["y"])
	await frames(2)
	verify("pickup collision adds score exactly once", game.shards == 1 and game.score == 100)
	game.collect(game.pickups[0])
	verify("collected shard cannot score twice", game.score == 100)
	game.player.position = Vector2(game.checkpoints[0][0], game.checkpoints[0][1])
	await frames(2)
	verify("checkpoint activates on contact", game.checkpoint_index == 0)
	game.hurt(true)
	verify("fall respawns at checkpoint", game.health == 2 and game.player.position == game.checkpoint)
	game.hurt()
	verify("respawn has damage protection", game.health == 2)
	game.player.invulnerable = 0
	var enemy: Dictionary = game.enemies[0]
	game.player.position = Vector2(enemy["x"], enemy["y"])
	game.player.dash_left = 0.18
	await frames(2)
	verify("dash defeats enemy", not enemy["alive"])
	game.start_game()
	await frames(2)
	for i in range(3):
		game.hurt(true)
	verify("three hits produce game over", game.state == "lost")
	game.start_game()
	await frames(2)
	verify("restart resets run", game.state == "playing" and game.score == 0 and game.health == 3 and game.chapter == 0)
	for level_index in range(3):
		for pickup in game.pickups:
			game.collect(pickup)
		game.try_exit()
		await frames(2)
	verify("all chapters lead to victory", game.state == "won" and game.chapter == 2 and game.score == 2550)
	var failures := checks.filter(func(c): return not c["passed"])
	var report := {"passed": failures.is_empty(), "checks": checks}
	var file := FileAccess.open("res://gameplay_test_results.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "  "))
	file.close()
	quit(0 if failures.is_empty() else 1)
