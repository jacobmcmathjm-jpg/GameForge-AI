# ForgeCore indie visual direction

User decision, 16 November 2026: replace the N64-inspired target with indie-style graphics. The original retro profile and its old deliveries remain available; this is a new direction, not retro-art acceptance being revoked or historical evidence being rewritten.

First benchmark: a cohesive grounded industrial FPS. Aim for readable silhouettes, convincing material scale, restrained weathering, a coherent exterior/practical-light balance and richer architectural definition. This is not a photorealistic/AA quality claim. The existing technical mannequin/weapon still need dedicated source-art work.

Work order:

1. Reusable environment/look-development candidate through the existing app tools, preserving working gameplay/audio. Compare actual rendered output, not a concept image.
2. Improve the visible weapon/hands and guard art against that environment. Preserve evaluated sights/grips/muzzle, motion and accepted audio; exact source/target reviews remain required for library placement.
3. Expand into connected playable areas after route/collision/encounter tests. Decorative backdrop buildings do not count as accessible rooms or larger levels.
4. Carry the shared asset/audio/material/export/testing infrastructure into a second genre, then implement and test its own gameplay adapter. Existing primitive platformer/RPG/racing scaffolds are not finished indie games or proof of all-genre generation.

Each stage must retain measured resource limits, the provisional maximum-frame gate, old failures and human acceptance boundaries. The outstanding intermittent freeze and 84–86 ms events remain tracked separately; a graphics pass must not hide them by extending warmup or replacing maximum timing with p95.

First reusable increment: kit 0.16.96's `grounded_indie_environment_v1` on the non-retro detailed industrial route. Ordinary prompt: `Create an indie industrial courtyard named indie-yard.` Existing detailed/default requests use the same route; explicit retro requests keep the retro path. This is a bounded industrial-FPS recipe, not unrestricted natural-language game generation.
