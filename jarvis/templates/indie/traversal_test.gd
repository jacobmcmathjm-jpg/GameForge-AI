extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var game: Node2D = load("res://Main.tscn").instantiate()
	root.add_child(game)
	await physics_frame
	game.start_game()
	var visited: Dictionary = {}
	var ticks := 0
	var jump_hold := 0
	var trace: Array = []
	while game.state == "playing" and ticks < 15000:
		ticks += 1
		visited[game.chapter] = true
		if jump_hold > 0:
			jump_hold -= 1
		elif Input.is_action_pressed("jump"):
			Input.action_release("jump")
		var target: Vector2 = game.portal
		for shard in game.pickups:
			if not shard["taken"]:
				target = Vector2(shard["x"], shard["y"])
				break
		var dx: float = target.x - game.player.position.x
		Input.action_release("left")
		Input.action_release("right")
		if absf(dx) > 5:
			Input.action_press("right" if dx > 0 else "left")
		if game.player.is_on_floor():
			for platform in game.platforms:
				if game.player.position.x >= platform[0] - 2 and game.player.position.x <= platform[0] + platform[2] + 2:
					var edge: float = platform[0] + platform[2] if dx > 0 else platform[0]
					if absf(edge - game.player.position.x) < 55 and absf(dx) > 70:
						Input.action_press("jump")
						jump_hold = 23
		var wants_dash := false
		for enemy in game.enemies:
			if enemy["alive"] and absf(enemy["x"] - game.player.position.x) < 95 and absf(enemy["y"] - game.player.position.y) < 50 and game.player.dash_cooldown <= 0:
				wants_dash = true
		if wants_dash and not Input.is_action_pressed("dash"):
			Input.action_press("dash")
		elif not wants_dash and Input.is_action_pressed("dash"):
			Input.action_release("dash")
		if ticks % 4 == 0:
			trace.append({"tick":ticks,"x":game.player.position.x,"y":game.player.position.y,"dash":game.player.dash_left,"cooldown":game.player.dash_cooldown,"dash_key":Input.is_action_pressed("dash"),"enemy_x":game.enemies[0]["x"],"floor":game.player.is_on_floor()})
			if trace.size() > 25:
				trace.pop_front()
		await physics_frame
	for action in ["left", "right", "jump", "dash"]:
		Input.action_release(action)
	var result := {"passed": game.state == "won", "state": game.state, "chapter": game.chapter,
		"chapters_visited": visited.size(), "physics_frames": ticks, "damage_events": game.deaths,
		"score": game.score, "shards": game.shards, "position": str(game.player.position),
		"method": "simulated keyboard input; no teleports or direct objective completion"}
	if not result["passed"]:
		result["trace"] = trace
	var file := FileAccess.open("res://traversal_test_results.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(result, "  "))
	file.close()
	print("TRAVERSAL=" + JSON.stringify(result))
	quit(0 if result["passed"] else 1)
