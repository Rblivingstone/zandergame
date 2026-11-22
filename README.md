# Zander's Hoop Shrines

A short 3D basketball puzzle adventure starring **Zander**, a chibi-anime
version of a certain 8-year-old (spiky blonde faux hawk, big blue eyes).
Clear three puzzle shrines, each harder than the last, using basketball
skills. Then beat **KING SWAT**, a robot basketball king, in a shooting
contest on the Championship Court.

## Quick start

1. Install **Unity 2022.3 LTS** or **Unity 6** (Built-in Render Pipeline; no extra packages needed).
2. In Unity Hub choose **Add → Add project from disk** and pick `unity/ZanderHoopShrines`.
3. Open the project. A setup step runs automatically. Then open
   `Assets/Scenes/Main.unity` (or use the menu **Zander → Open Main Scene**) and press **Play**.

The whole game (courts, puzzles, characters, boss, UI and sounds) builds itself
at runtime, so **any scene works**: just press Play.

> If Unity asks about the *Input System*, choose the **old Input Manager**
> (or **Both**): *Edit → Project Settings → Player → Active Input Handling*.

## Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD / arrows | Left stick |
| Look | Mouse | |
| Jump | Space | A |
| Sprint | Left Shift | L3 |
| **Shoot** | Hold left mouse (or F), release in the **green** zone | X |
| **Dunk** | Press Shoot in mid-air next to a hoop | X |
| Drop / place ball on a plate | Q / right mouse | B |
| Use (turn boards, take a ball off a plate) | E | Y |
| Hint | H | Back |
| Super Shots (every shot is perfect) | Tab | |
| Restart room | R | |
| Pause | Esc | Start |

## The game

| Area | Difficulty | Puzzle |
|---|---|---|
| Lobby | – | Practice hoop and controls |
| **Shrine 1 – Warm-Up** | Easy | Stand on the blue plate to free the ball, light the hoop, then rest a ball on the orange plate. |
| **Shrine 2 – Bounce & Dunk** | Medium | Ride the bounce pad and **dunk** the high "dunk only" hoop to power a lift. Then light **both** timed hoops within 12 seconds. |
| **Shrine 3 – Chain Reaction** | Hard | Hold a ball on a plate to raise a bridge over a pit. Then score GREEN → RED → BLUE in order, using rotating **Rebound Boards** for bank shots. |
| **Championship Court** | Boss | First to 15 vs **KING SWAT**. 3-pointers count. Jump his stomp shockwaves, wait out his swats, and hit a moving hoop in the final round. |

Basketball mechanics: auto pick-up, dribbling, a timing shot meter with
kid-friendly aim assist, free throws, dunks, placing balls on plates,
bounce pads, and bank shots off rotating Rebound Boards.

## Repository layout

```
blender/                      Python scripts that generate every 3D model
  zg_common.py                shared helpers (materials, primitives, rigging, FBX export)
  zg_anime.py                 smooth anime modelling helpers (skin bodies, heads, hair, decals, weights)
  zander_character.py         Zander, rigged, with 9 animations
  boss_character.py           King Swat, rigged, with 8 animations
  props.py                    ball, hoop, plates, gate, dispenser, bounce pad, rebound board, bleachers...
  render_preview.py           renders preview PNGs of any generated .blend
unity/ZanderHoopShrines/      the Unity project
  Assets/ZanderGame/Scripts   game code (Core, Player, Gameplay, Level, Boss, UI)
  Assets/ZanderGame/Editor    import settings and one-click project setup
  Assets/ZanderGame/Resources/Models   generated FBX models (committed)
  Assets/ZanderGame/Resources/Shaders  anime toon + outline shader
```

## Regenerating the models

You need Blender 3.6 or newer, or the `bpy` Python module (`pip install bpy`, Python 3.11 or 3.13).

```bash
blender -b -P blender/zander_character.py
blender -b -P blender/boss_character.py
blender -b -P blender/props.py
```

or, with the pip module:

```bash
python blender/zander_character.py
```

The FBX files are written straight into `unity/.../Resources/Models`. Unity
re-imports them automatically. Every model also has a primitive fallback in
code, so the game still runs without them.
