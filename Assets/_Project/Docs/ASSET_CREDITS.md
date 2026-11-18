# Assets reused for Level 1

No external art was downloaded for this implementation. These existing project assets are reused; their original package licenses remain applicable (not independently verified here).

| Use | Existing project source |
| --- | --- |
| Dropped heart | Assets/Jungle_UI/Sprites/Icons/heart_icon.png |
| Dropped/equipped sword | Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png — TX Village Props - Sword |
| Closed/open chest | Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Chest 01.prefab and PF Props - Chest 01 Open.prefab |
| Enemy variants | Existing Monster (1).prefab in the project; Level1 variants reuse its sprite/animation |
| Item/enemy shadows | Existing PlayerShadow sprite from the player prefab |

The player retains the original body animations. Final art polishing remains part of stage F.

## City expansion update
- DustGremlinWalk.png, MossBeetleWalk.png, ScrapGolemWalk.png: generated with the built-in imagegen tool for this project; each has 16 walk-cycle frames, consumed by Unity Animation Clips. CityCreatures.png is the generated design reference. Full prompts: CITY_CREATURE_PROMPTS.md.
- Added city scenery reuses Assets/_Project/Art/Sprites/ItemBackground/Item.png (existing project asset). Original source/license was not independently verified.
- Previous note about reusing the old enemy sprite is superseded: all 18 active level enemies now use the new creature sheets. Original inactive demo enemies remain available.
- Attack sprite sheets (*Attack.png): generated with built-in imagegen from the corresponding walk sheets; prompts recorded in CITY_CREATURE_PROMPTS.md. Consumed by four directional Attack clips per species, with Unity Animation Events.

## Hiding and traps (D+E)
- Hiding foliage: existing project Item.png, sprite Bush; reused without modifying the source sheet.
- Trap spikes: existing Cainos Pixel Art Platformer - Village Props, PF Village Props - Spike.prefab sprite; source package retained unmodified. Orange tint applied on new project-owned prefab.
- SharedGroundShadow: existing PlayerShadow.png, new reusable prefab with 35% alpha.
- No external downloads or new generated art for D+E. Final sprite-style polishing remains stage F.

## Lexi residents (G)
- Art/NPC/LexiResidents.png: generated using the built-in imagegen tool for six original NPC designs, with open-eye/blink frames. Prompts and generation notes: LEXI_NPC_ART.md.
- Dialogue marker: existing Fantasy Wooden GUI Exclamation_Yellow.png; shadows reuse PlayerShadow. Existing third-party source assets remain unchanged.

## F — reused art and grounding
No downloads or new bitmap art. Existing PlayerShadow.png reused by SharedGroundShadow; only transforms, alpha, renderer visibility and scene/prefab bindings changed. Creature import pivots updated through Sprite Editor metadata (96 frames); original images and sprite IDs retained. Houses/trees/benches keep original art. Heart and weapon remain icon sprites with added ground shadows, as permitted by asset rule 8(c).
