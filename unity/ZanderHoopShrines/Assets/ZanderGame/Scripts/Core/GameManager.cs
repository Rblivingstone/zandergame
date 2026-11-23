using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// Builds the world, spawns Zander, tracks which room he is in, checkpoints,
    /// shrine completion, pausing, hints and the victory screen.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum GameState { Title, Playing, Paused, Victory }

        public static GameManager Instance { get; private set; }
        public GameState State { get; private set; } = GameState.Title;
        /// <summary>False on the frame the game (re)starts, so the button that closed a menu doesn't also jump/shoot.</summary>
        public bool IsPlaying => State == GameState.Playing && Time.frameCount != _unpausedFrame;
        int _unpausedFrame = -1;

        public List<Room> Rooms { get; private set; }
        public Room CurrentRoom { get; private set; }
        public BossArena Arena { get; private set; }
        public ShootingContest Contest { get; private set; }
        public PlayerController Player { get; private set; }
        public ThirdPersonCamera Cam { get; private set; }

        public int ShrinesCleared { get; private set; }
        public int ShrineCount => 3;
        public float PlayTime { get; private set; }
        public int HintIndex { get; private set; } = -1;
        public bool Won { get; private set; }

        public Vector3 CheckpointPosition => CurrentRoom != null ? CurrentRoom.Spawn : Vector3.up;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 60;
            Physics.gravity = new Vector3(0, -9.81f, 0);
            GameAudio.Init(gameObject);
            SetupLighting();

            var world = new GameObject("World");
            var builder = new LevelBuilder(world.transform);
            builder.BuildAll();
            Rooms = builder.Rooms;
            Arena = builder.Arena;

            Contest = gameObject.AddComponent<ShootingContest>();
            Contest.Setup(Arena);

            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            var cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = 60f;
            Cam = camGo.GetComponent<ThirdPersonCamera>();
            if (Cam == null) Cam = camGo.AddComponent<ThirdPersonCamera>();

            CurrentRoom = Rooms[0];
            Player = PlayerController.Spawn(CurrentRoom.Spawn, 0);
            Player.Cam = Cam;
            Cam.Target = Player.transform;
            Cam.SnapBehind(0);

            gameObject.AddComponent<ControlsDriver>();
            gameObject.AddComponent<Hud>();
        }

        void SetupLighting()
        {
            var sunGo = GameObject.Find("Directional Light");
            if (sunGo == null) sunGo = new GameObject("Sun", typeof(Light));
            var sun = sunGo.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.42f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 220f;
            RenderSettings.fogColor = new Color(0.6f, 0.72f, 0.88f);
        }

        void Update()
        {
            switch (State)
            {
                case GameState.Title:
                    SetCursor(false);
                    if (Controls.AnyStart) StartGame();
                    return;
                case GameState.Paused:
                    SetCursor(false);
                    if (Controls.PauseDown || Controls.MenuBack) Resume();
                    return;
                case GameState.Victory:
                    SetCursor(false);
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                        Input.GetKeyDown(KeyCode.JoystickButton0)) State = GameState.Playing;
                    return;
            }

            // ---- playing ----
            SetCursor(true);
            if (Input.GetMouseButtonDown(0)) SetCursor(true);
            PlayTime += Time.deltaTime;
            if (Controls.PauseDown)
            {
                Pause();
                return;
            }
            if (Controls.HintDown && CurrentRoom != null && CurrentRoom.Hints.Length > 0)
            {
                HintIndex = (HintIndex + 1) % CurrentRoom.Hints.Length;
                GameAudio.Play("tick");
            }
            if (Controls.EasyToggleDown)
            {
                PlayerBallHandler.SuperShots = !PlayerBallHandler.SuperShots;
                Hud.Toast(PlayerBallHandler.SuperShots ? "SUPER SHOTS ON - every shot is perfect!" : "Super Shots off", Color.white);
            }
            if (Controls.RestartDown) RestartRoom();

            // which room is Zander in?
            var pos = Player.transform.position;
            foreach (var r in Rooms)
                if (r.Contains(pos) && r != CurrentRoom)
                {
                    EnterRoom(r);
                    break;
                }

            // shrine completion
            foreach (var r in Rooms)
            {
                if (r.Solved || r.Exit == null || !r.Exit.IsOpen) continue;
                r.Solved = true;
                ShrinesCleared++;
                GameAudio.Play("solve");
                Hud.Banner("SHRINE CLEAR!  (" + ShrinesCleared + "/" + ShrineCount + ")", 3f);
                Effects.Confetti(r.Exit.transform.position + Vector3.up * 4.5f, 80);
                Rumble.Pulse(0.5f, 0.5f, 0.6f);
                Player.Anim?.Trigger("Celebrate");
            }
        }

        void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void StartGame()
        {
            State = GameState.Playing;
            _unpausedFrame = Time.frameCount;
            Hud.Banner("ZANDER'S HOOP SHRINES", 2.5f);
            GameAudio.Play("whistle");
        }

        public void Pause()
        {
            State = GameState.Paused;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            State = GameState.Playing;
            _unpausedFrame = Time.frameCount;
            Time.timeScale = 1f;
        }

        void EnterRoom(Room r)
        {
            CurrentRoom = r;
            HintIndex = -1;
            if (r.IsLobby) return;
            string diff = r.Difficulty == 1 ? "EASY" : r.Difficulty == 2 ? "MEDIUM" : r.Difficulty == 3 ? "HARD" : "BOSS";
            Hud.Banner(r.Name.ToUpperInvariant() + "\n" + diff + " - " + r.Subtitle, 3f);
            GameAudio.Play("chime", 0.6f);
        }

        public void RespawnPlayer(bool fell)
        {
            Player.Balls?.DropBall(false);
            var r = CurrentRoom ?? Rooms[0];
            Player.Teleport(r.Spawn, r.SpawnYaw);
            if (fell)
            {
                GameAudio.Play("splash");
                Hud.Toast("Whoops! Back to the start of the room.", Color.white, 1.8f);
            }
        }

        public void RestartRoom()
        {
            if (CurrentRoom != null && CurrentRoom.IsBoss && Contest.Active)
            {
                Contest.RestartContest();
                return;
            }
            Player.Balls?.DropBall(false);
            CurrentRoom?.ResetBalls();
            RespawnPlayer(false);
            Hud.Toast("Room restarted - balls are back on their racks.", Color.white, 1.8f);
        }

        public void ShowVictory()
        {
            Won = true;
            State = GameState.Victory;
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }
    }

    /// <summary>
    /// Starts the game automatically in ANY scene when you press Play - no scene
    /// setup needed. Add an object named "NoZanderBoot" to a scene to disable this.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameManager.Instance != null) return;
            if (GameObject.Find("NoZanderBoot") != null) return;
            new GameObject("ZanderGame").AddComponent<GameManager>();
        }
    }
}
