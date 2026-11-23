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

> The game reads the controller through Unity's classic Input Manager (the axes
> are in `ProjectSettings/InputManager.asset`). If *Active Input Handling* is set
> to the new Input System only, the setup step switches it to **Both**. Restart Unity when it asks.

## Controls

Made for an **Xbox-layout controller on Windows**, such as the GameSir Kaleid
wired controller: plug it in and play. Keyboard and mouse work too. On-screen
prompts switch automatically to whichever one you used last, and the
controller rumbles on dunks, stomps and shrine clears.

| Action | Controller | Keyboard / mouse |
|---|---|---|
| Move | Left stick (or D-pad) | WASD / arrows |
| Look | Right stick | Mouse |
| Camera behind Zander | RB (or R3) | – |
| Jump | A | Space |
| Sprint | Hold LT (or L3) | Left Shift |
| **Shoot** | Hold **RT** (or X), release in the **green** zone | Hold left mouse (or F) |
| **Dunk** | RT in mid-air next to a hoop | Same as Shoot |
| Drop / place ball on a plate | B | Q / right mouse |
| Use (turn boards, take a ball off a plate) | Y | E |
| Hint | LB | H |
| Super Shots (every shot is perfect) | View (two squares) | Tab |
| Pause menu (restart room, quit) | Menu (three lines) | Esc |
| Restart room | Pause menu | R |

While Zander runs, the camera slowly swings in behind him unless you're using the right stick.

## One-click Windows build

**From the Unity editor:** menu **Zander → Build for Windows (one click)**.
**Without opening Unity:** close the project in the Unity editor, then double-click **`build_windows.bat`** in the repo root.
The script finds Unity through Unity Hub.

Either way you get `unity/ZanderHoopShrines/Builds/Windows/ZanderHoopShrines.exe`
plus a zip of the whole folder (`Builds/ZanderHoopShrines-Windows.zip`). Copy the
folder or the zip to any Windows 10/11 PC, plug in the controller and
double-click the `.exe`. **Zander → Build and Run on this PC** builds the game and launches it straight away.

> The Windows build needs **Windows Build Support** for your Unity version.
> On a Windows PC it comes with the editor; on a Mac, add it in Unity Hub → Installs → Add modules.

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
