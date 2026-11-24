using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// All on-screen UI, drawn with IMGUI so the project needs no UI packages:
    /// title screen, room info, shot meter, aim marker, prompts, hints, boss
    /// scoreboard, pause and victory screens.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        static Hud _inst;
        string _toast;
        Color _toastColor;
        float _toastUntil;
        string _banner;
        float _bannerUntil;
        float _bannerStart;

        GUIStyle _big, _title, _med, _small, _center, _shadow;
        Texture2D _white;
        float _scale = 1f;
        float W => Screen.width / _scale;
        float H => Screen.height / _scale;

        static readonly Color Panel = new Color(0.05f, 0.07f, 0.12f, 0.72f);
        static readonly Color GoldC = new Color(1f, 0.8f, 0.2f);

        void Awake() => _inst = this;

        public static void Toast(string text, Color color, float seconds = 1.8f)
        {
            if (_inst == null) return;
            _inst._toast = text;
            _inst._toastColor = color;
            _inst._toastUntil = Time.unscaledTime + seconds;
        }

        public static void Banner(string text, float seconds = 2.5f)
        {
            if (_inst == null) return;
            _inst._banner = text;
            _inst._bannerStart = Time.unscaledTime;
            _inst._bannerUntil = Time.unscaledTime + seconds;
        }

        void InitStyles()
        {
            if (_white != null) return;
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            var font = Compat.DefaultFont();
            _small = new GUIStyle { font = font, fontSize = 22, wordWrap = true, richText = true };
            _small.normal.textColor = Color.white;
            _med = new GUIStyle(_small) { fontSize = 30, fontStyle = FontStyle.Bold };
            _big = new GUIStyle(_med) { fontSize = 56, alignment = TextAnchor.MiddleCenter };
            _title = new GUIStyle(_big) { fontSize = 92 };
            _center = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _shadow = new GUIStyle(_big);
            _shadow.normal.textColor = new Color(0, 0, 0, 0.6f);
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        void Text(Rect r, string s, GUIStyle st, Color c, bool shadow = false)
        {
            s = Controls.Expand(s);
            if (shadow)
            {
                var sh = new GUIStyle(st);
                sh.normal.textColor = new Color(0, 0, 0, 0.7f);
                GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), s, sh);
            }
            var st2 = new GUIStyle(st);
            st2.normal.textColor = c;
            GUI.Label(r, s, st2);
        }

        void OnGUI()
        {
            InitStyles();
            _scale = Mathf.Max(0.5f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_scale, _scale, 1));
            var gm = GameManager.Instance;
            if (gm == null) return;

            switch (gm.State)
            {
                case GameManager.GameState.Title:
                    DrawTitle();
                    return;
                case GameManager.GameState.Victory:
                    DrawVictory(gm);
                    return;
            }

            DrawStamina(gm);
            DrawRoomPanel(gm);
            DrawTopRight(gm);
            DrawAim(gm);
            DrawMeter(gm);
            DrawPrompt(gm);
            DrawHint(gm);
            DrawContest(gm);
            DrawToastAndBanner();
            if (gm.State == GameManager.GameState.Paused) DrawPause(gm);
        }

        // ---------------------------------------------------------------- title --
        void DrawTitle()
        {
            Fill(new Rect(0, 0, W, H), new Color(0.03f, 0.05f, 0.12f, 0.82f));
            float bob = Mathf.Sin(Time.unscaledTime * 2f) * 6f;
            Text(new Rect(0, H * 0.14f + bob, W, 120), "ZANDER'S", _title, GoldC, true);
            Text(new Rect(0, H * 0.14f + 110 + bob, W, 120), "HOOP SHRINES", _title, new Color(1f, 0.55f, 0.15f), true);
            Text(new Rect(0, H * 0.14f + 230, W, 50), "A basketball puzzle adventure", _center, Color.white);

            float bw = 900, bx = (W - bw) / 2, by = H * 0.5f;
            Fill(new Rect(bx, by, bw, 300), Panel);
            string controls = Controls.UsingGamepad
                ? "<b>MOVE</b>  Left stick        <b>LOOK</b>  Right stick        <b>JUMP</b>  A        <b>DASH</b>  hold LT (stamina)\n" +
                  "<b>SHOOT</b>  hold RT - let go in the <color=#6CFF8A>GREEN</color> zone!\n" +
                  "<b>DUNK</b>  X near a hoop (hold X to hang on the rim)        <b>CAMERA BEHIND</b>  RB\n" +
                  "<b>DROP / PLACE BALL</b>  B        <b>USE / TURN BOARD</b>  Y\n" +
                  "<b>HINT</b>  LB        <b>SUPER SHOTS</b>  View        <b>PAUSE</b>  Menu"
                : "<b>MOVE</b>  WASD / arrows        <b>JUMP</b>  Space        <b>DASH</b>  hold Shift (stamina)\n" +
                  "<b>SHOOT</b>  hold Left Mouse (or F) - let go in the <color=#6CFF8A>GREEN</color> zone!\n" +
                  "<b>DUNK</b>  C near a hoop (hold C to hang on the rim)\n" +
                  "<b>DROP / PLACE BALL</b>  Q        <b>USE / TURN BOARD</b>  E\n" +
                  "<b>HINT</b>  H        <b>SUPER SHOTS</b>  Tab        <b>RESTART ROOM</b>  R        <b>PAUSE</b>  Esc";
            Text(new Rect(bx + 30, by + 20, bw - 60, 270), controls, _small, Color.white);
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
            Text(new Rect(0, by + 330, W, 60), Controls.UsingGamepad ? "Press A to start!" : "Click or press SPACE to start!", _med, new Color(1, 1, 1, pulse));
            var st = new GUIStyle(_center);
            st.alignment = TextAnchor.MiddleCenter;
            Text(new Rect(0, H - 60, W, 40), "Beat 3 shrines, then win the shooting contest against KING SWAT!", st, GoldC);
        }

        // ------------------------------------------------------- stamina wheel --
        // The wheel is drawn from pre-made ring images (one per fill step), so it is a
        // single picture on screen - no rotated pieces.
        const int RingSteps = 64;
        const int RingTexSize = 160;
        readonly Texture2D[] _ringTex = new Texture2D[RingSteps + 1];

        Texture2D RingTexture(float fraction)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(fraction) * RingSteps), 0, RingSteps);
            if (_ringTex[step] != null) return _ringTex[step];
            int n = RingTexSize;
            float outer = n * 0.5f - 2f, inner = outer * 0.68f;
            float fill = step / (float)RingSteps;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;   // y up (row 0 = bottom)
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float radial = Mathf.Clamp01(Mathf.Min(r - inner, outer - r) + 0.5f);
                    // clockwise from 12 o'clock
                    float ang = Mathf.Atan2(dx, dy) / (2f * Mathf.PI);
                    if (ang < 0) ang += 1f;
                    float angular = fill >= 1f ? 1f : Mathf.Clamp01((fill - ang) * 2f * Mathf.PI * r + 0.5f);
                    byte a = (byte)(255f * radial * angular);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "StaminaRing" + step, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply();
            _ringTex[step] = t;
            return t;
        }

        void DrawStamina(GameManager gm)
        {
            var p = gm.Player;
            var cam = Camera.main;
            if (p == null || cam == null || Time.time > p.StaminaShownUntil) return;
            // centred above Zander's head (hair top is ~1.6 m)
            var sp = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 2.25f);
            if (sp.z < 0) return;
            var c = new Vector2(sp.x / _scale, (Screen.height - sp.y) / _scale);
            float alpha = Mathf.Clamp01((p.StaminaShownUntil - Time.time) * 3f);
            Color fillC;
            if (p.Exhausted)
                fillC = Color.Lerp(new Color(1f, 0.3f, 0.25f), new Color(1f, 0.6f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f));
            else if (p.Stamina < 0.3f) fillC = new Color(1f, 0.85f, 0.25f);
            else fillC = new Color(0.42f, 1f, 0.55f);
            fillC.a = alpha;
            const float size = 96f;
            var rect = new Rect(c.x - size / 2, c.y - size / 2, size, size);
            var old = GUI.color;
            GUI.color = new Color(0.05f, 0.07f, 0.12f, 0.6f * alpha);
            GUI.DrawTexture(rect, RingTexture(1f));                       // dark track
            GUI.color = fillC;
            GUI.DrawTexture(rect, RingTexture(p.Stamina));                // stamina
            GUI.color = old;
        }

        // ---------------------------------------------------------- room panel --
        void DrawRoomPanel(GameManager gm)
        {
            var r = gm.CurrentRoom;
            if (r == null) return;
            float w = 560;
            Fill(new Rect(20, 20, w, 150), Panel);
            Text(new Rect(36, 28, w - 30, 40), r.Name, _med, GoldC);
            Text(new Rect(36, 70, w - 30, 100), r.Objective, _small, Color.white);
            string prog = "Shrines cleared: " + gm.ShrinesCleared + " / " + gm.ShrineCount;
            Text(new Rect(20, 176, w, 30), prog, _small, new Color(0.8f, 0.9f, 1f));
        }

        void DrawTopRight(GameManager gm)
        {
            string s = "{HINT}: Hint    {PAUSE}: Menu    {EASY}: Super Shots " + (PlayerBallHandler.SuperShots ? "<color=#6CFF8A>ON</color>" : "OFF");
            var st = new GUIStyle(_small) { alignment = TextAnchor.UpperRight };
            Text(new Rect(W - 720, 24, 700, 30), s, st, new Color(1, 1, 1, 0.85f));
            int mins = (int)(gm.PlayTime / 60), secs = (int)(gm.PlayTime % 60);
            Text(new Rect(W - 720, 56, 700, 30), string.Format("Time {0}:{1:00}", mins, secs), st, new Color(1, 1, 1, 0.6f));
        }

        // --------------------------------------------------------------- aiming --
        void DrawAim(GameManager gm)
        {
            var p = gm.Player;
            if (p == null || p.Balls == null) return;
            var cam = Camera.main;
            // centre dot
            Fill(new Rect(W / 2 - 3, H / 2 - 3, 6, 6), new Color(1, 1, 1, 0.7f));
            if (!p.Balls.HasBall || !p.Balls.AimPoint.HasValue || cam == null) return;
            var sp = cam.WorldToScreenPoint(p.Balls.AimPoint.Value);
            if (sp.z < 0) return;
            float x = sp.x / _scale, y = (Screen.height - sp.y) / _scale;
            float s = 46 + Mathf.Sin(Time.unscaledTime * 6) * 4;
            var c = new Color(0.4f, 1f, 0.5f, 0.95f);
            float t = 4, l = 16;
            // corner brackets
            Fill(new Rect(x - s, y - s, l, t), c); Fill(new Rect(x - s, y - s, t, l), c);
            Fill(new Rect(x + s - l, y - s, l, t), c); Fill(new Rect(x + s - t, y - s, t, l), c);
            Fill(new Rect(x - s, y + s - t, l, t), c); Fill(new Rect(x - s, y + s - l, t, l), c);
            Fill(new Rect(x + s - l, y + s - t, l, t), c); Fill(new Rect(x + s - t, y + s - l, t, l), c);
            var name = p.Balls.AimName;
            if (name != null) Text(new Rect(x - 200, y + s + 6, 400, 30), name, _center, c, true);
        }

        void DrawMeter(GameManager gm)
        {
            var b = gm.Player?.Balls;
            if (b == null || !b.Charging) return;
            float w = 520, h = 34, x = (W - w) / 2, y = H - 170;
            Fill(new Rect(x - 6, y - 6, w + 12, h + 12), Panel);
            void Zone(float a, float z, Color c) => Fill(new Rect(x + a * w, y, (z - a) * w, h), c);
            Zone(0, PlayerBallHandler.OkMin, new Color(0.9f, 0.3f, 0.25f));
            Zone(PlayerBallHandler.OkMin, PlayerBallHandler.SweetMin, new Color(1f, 0.8f, 0.25f));
            Zone(PlayerBallHandler.SweetMin, PlayerBallHandler.SweetMax, new Color(0.3f, 0.95f, 0.45f));
            Zone(PlayerBallHandler.SweetMax, PlayerBallHandler.OkMax, new Color(1f, 0.8f, 0.25f));
            Zone(PlayerBallHandler.OkMax, 1, new Color(0.9f, 0.3f, 0.25f));
            if (PlayerBallHandler.SuperShots) Zone(0, 1, new Color(0.3f, 0.95f, 0.45f));
            float nx = x + b.Power * w;
            Fill(new Rect(nx - 4, y - 12, 8, h + 24), Color.white);
            string label = b.AimTarget != null ? "Let go of {SHOOT} in the GREEN!" : "No target - free throw!";
            Text(new Rect(x, y + h + 10, w, 30), label, _center, Color.white, true);
        }

        void DrawPrompt(GameManager gm)
        {
            var p = gm.Player;
            if (p == null) return;
            string s = null;
            if (p.Balls != null && p.Balls.RimHanging) s = "Hold {DUNK} to hang on the rim!";
            else if (p.Balls != null && p.Balls.DunkHoop != null) s = "Press {DUNK} to DUNK!";
            else if (!string.IsNullOrEmpty(p.InteractPrompt)) s = p.InteractPrompt;
            if (s == null) return;
            float w = 980, x = (W - w) / 2, y = H - 110;
            Fill(new Rect(x, y, w, 50), Panel);
            Text(new Rect(x, y + 8, w, 40), s, _center, GoldC);
        }

        void DrawHint(GameManager gm)
        {
            var r = gm.CurrentRoom;
            if (r == null || gm.HintIndex < 0 || gm.HintIndex >= r.Hints.Length) return;
            float w = 560, x = 20, y = 215;
            Fill(new Rect(x, y, w, 130), new Color(0.15f, 0.1f, 0.02f, 0.8f));
            Text(new Rect(x + 16, y + 8, w - 30, 30), "HINT " + (gm.HintIndex + 1) + "/" + r.Hints.Length + "  (press {HINT} for more)",
                _small, GoldC);
            Text(new Rect(x + 16, y + 40, w - 30, 90), r.Hints[gm.HintIndex], _small, Color.white);
        }

        void DrawContest(GameManager gm)
        {
            var c = gm.Contest;
            if (c == null || !c.Active) return;
            float w = 700, x = (W - w) / 2, y = 20;
            Fill(new Rect(x, y, w, 110), Panel);
            Text(new Rect(x, y + 6, w, 40), "FIRST TO " + ShootingContest.TargetScore, _center, GoldC);
            var left = new GUIStyle(_big) { alignment = TextAnchor.MiddleLeft, fontSize = 40 };
            var right = new GUIStyle(_big) { alignment = TextAnchor.MiddleRight, fontSize = 40 };
            Text(new Rect(x + 20, y + 40, w / 2, 50), "ZANDER " + c.PlayerScore, left, new Color(0.5f, 0.8f, 1f), true);
            Text(new Rect(x + w / 2 - 20, y + 40, w / 2, 50), c.BossScore + " KING SWAT", right, new Color(0.85f, 0.6f, 1f), true);
            // bars
            float bw = w / 2 - 30;
            Fill(new Rect(x + 20, y + 92, bw, 10), new Color(1, 1, 1, 0.15f));
            Fill(new Rect(x + 20, y + 92, bw * Mathf.Clamp01(c.PlayerScore / (float)ShootingContest.TargetScore), 10), new Color(0.4f, 0.75f, 1f));
            Fill(new Rect(x + w - 20 - bw, y + 92, bw, 10), new Color(1, 1, 1, 0.15f));
            float bf = bw * Mathf.Clamp01(c.BossScore / (float)ShootingContest.TargetScore);
            Fill(new Rect(x + w - 20 - bf, y + 92, bf, 10), new Color(0.8f, 0.5f, 1f));
            if (c.CurrentState == ShootingContest.ContestState.Playing)
                Text(new Rect(x, y + 112, w, 30), "Round " + c.Phase + " / 3", _center, new Color(1, 1, 1, 0.7f));
        }

        void DrawToastAndBanner()
        {
            float now = Time.unscaledTime;
            if (_toast != null && now < _toastUntil)
            {
                float a = Mathf.Clamp01((_toastUntil - now) * 3f);
                var c = _toastColor;
                c.a = a;
                var st = new GUIStyle(_med) { alignment = TextAnchor.MiddleCenter };
                Text(new Rect(0, H * 0.3f, W, 50), _toast, st, c, true);
            }
            if (_banner != null && now < _bannerUntil)
            {
                float t = now - _bannerStart;
                float a = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((_bannerUntil - now) * 2f);
                float sc = 1f + Mathf.Max(0, 0.25f - t) * 2f;
                Fill(new Rect(0, H * 0.38f, W, 170), new Color(0, 0, 0, 0.45f * a));
                var st = new GUIStyle(_big) { fontSize = (int)(56 * sc) };
                Text(new Rect(0, H * 0.38f, W, 170), _banner, st, new Color(1f, 0.85f, 0.3f, a), true);
            }
        }

        int _pauseIndex;
        static readonly string[] PauseItems = { "Resume", "Super Shots", "Restart Room", "Quit" };

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameManager.GameState.Paused)
            {
                _pauseIndex = 0;
                return;
            }
            int nav = Controls.MenuNav;
            if (nav != 0)
            {
                _pauseIndex = (_pauseIndex + nav + PauseItems.Length) % PauseItems.Length;
                GameAudio.Play("tick");
            }
            if (Controls.MenuConfirm) ActivatePause(gm, _pauseIndex);
        }

        void ActivatePause(GameManager gm, int i)
        {
            GameAudio.Play("click");
            switch (i)
            {
                case 0: gm.Resume(); break;
                case 1: PlayerBallHandler.SuperShots = !PlayerBallHandler.SuperShots; break;
                case 2:
                    gm.Resume();
                    gm.RestartRoom();
                    break;
                case 3: gm.QuitGame(); break;
            }
        }

        void DrawPause(GameManager gm)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
            Text(new Rect(0, H * 0.2f, W, 100), "PAUSED", _big, Color.white, true);
            float bw = 460, bx = (W - bw) / 2, by = H * 0.38f;
            var bs = new GUIStyle(GUI.skin.button) { fontSize = 30, font = _small.font };
            for (int i = 0; i < PauseItems.Length; i++)
            {
                var r = new Rect(bx, by + i * 90, bw, 70);
                if (i == _pauseIndex) Fill(new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16), GoldC);
                string label = i == 1 ? "Super Shots: " + (PlayerBallHandler.SuperShots ? "ON" : "OFF") : PauseItems[i];
                if (GUI.Button(r, label, bs))
                {
                    _pauseIndex = i;
                    ActivatePause(gm, i);
                }
            }
            Text(new Rect(0, by + 380, W, 40),
                Controls.UsingGamepad ? "D-pad: choose    A: select    B: back" : "Click a button, or press Esc to resume", _center,
                new Color(1, 1, 1, 0.8f));
        }

        void DrawVictory(GameManager gm)
        {
            Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.03f, 0.12f, 0.7f));
            float bob = Mathf.Sin(Time.unscaledTime * 3f) * 8f;
            Text(new Rect(0, H * 0.16f + bob, W, 130), "CHAMPION!", _title, GoldC, true);
            Text(new Rect(0, H * 0.32f, W, 60), "Zander cleared all 3 Hoop Shrines and beat KING SWAT!", _med, Color.white, true);
            int mins = (int)(gm.PlayTime / 60), secs = (int)(gm.PlayTime % 60);
            Text(new Rect(0, H * 0.42f, W, 50), string.Format("Time: {0}:{1:00}      Final score: {2} - {3}", mins, secs,
                gm.Contest.PlayerScore, gm.Contest.BossScore), _med, new Color(0.7f, 0.9f, 1f));
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
            Text(new Rect(0, H * 0.6f, W, 60), "Press {START} to keep shooting hoops!", _med, new Color(1, 1, 1, pulse));
        }
    }
}
