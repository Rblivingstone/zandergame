using System.Collections;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>
    /// The final room: a race to 15 points against KING SWAT.
    /// Zander scores 2 points (3 from beyond the arc); King Swat scores 2 per make.
    /// </summary>
    public class ShootingContest : MonoBehaviour
    {
        public enum ContestState { Waiting, Intro, Countdown, Playing, Won, Lost }

        public const int TargetScore = 15;
        public BossArena Arena;
        public BossController Boss;
        public ContestState CurrentState { get; private set; } = ContestState.Waiting;
        public int PlayerScore { get; private set; }
        public int BossScore { get; private set; }
        public int Phase => PlayerScore >= 10 ? 3 : PlayerScore >= 5 ? 2 : 1;
        public string Banner { get; private set; }
        public bool Active => CurrentState != ContestState.Waiting;

        /// <summary>The one and only ball in the arena during the contest.</summary>
        public Basketball GameBall { get; private set; }
        /// <summary>True while the ball drops through the net and gets handed to the other player.</summary>
        public bool Inbounding { get; private set; }

        Vector3 CenterSpot => Arena.CourtCenter + Vector3.up * 1.5f;

        Basketball EnsureGameBall()
        {
            if (GameBall == null)
            {
                GameBall = Basketball.Create(CenterSpot);
                GameBall.Persistent = true;
                GameBall.name = "GameBall";
            }
            GameBall.ResetPoint = CenterSpot;
            GameBall.PlayerCanGrab = true;
            return GameBall;
        }

        /// <summary>Removes every ball in the arena except the game ball.</summary>
        void DespawnExtraBalls()
        {
            var player = PlayerController.Instance;
            foreach (var b in Basketball.All.ToArray())
            {
                if (b == null || b == GameBall || !Arena.Room.Contains(b.transform.position)) continue;
                if (player != null && player.Balls != null && player.Balls.Ball == b) player.Balls.ForgetBall();
                b.Persistent = false;
                b.Poof();
            }
        }

        /// <summary>After a basket the OTHER player appears by the hoop that was scored on, holding the ball.</summary>
        IEnumerator InboundAfterScore(Hoop scoredOn, bool toBoss)
        {
            Inbounding = true;
            var ball = EnsureGameBall();
            ball.PlayerCanGrab = false;
            yield return new WaitForSeconds(0.9f);      // let it drop through the net
            if (CurrentState != ContestState.Playing) { Inbounding = false; yield break; }
            var rim = scoredOn.RimCenter;
            var front = scoredOn.transform.forward;
            front.y = 0;
            front.Normalize();
            var spot = new Vector3(rim.x, 0.05f, rim.z) + front * 1.8f;
            ball.PlayerCanGrab = true;
            ball.Body.SetVelocity(Vector3.zero);
            if (toBoss)
            {
                Boss.InboundAt(spot, front, ball);
                Hud.Toast("King Swat's ball!", new Color(0.8f, 0.6f, 1f), 1.0f);
            }
            else
            {
                var p = PlayerController.Instance;
                p.Teleport(spot, Quaternion.LookRotation(front).eulerAngles.y);
                p.Balls.GiveBall(ball);
                Boss.BackOff(2.0f);
                Hud.Toast("Your ball - go!", new Color(0.4f, 0.8f, 1f), 1.0f);
            }
            GameAudio.Play("whistle", 0.6f);
            Inbounding = false;
        }


        float _cheer;
        int _lastPhase = 1;
        float[] _fanBaseY;

        public void Setup(BossArena arena)
        {
            Arena = arena;
            Boss = BossController.Spawn(arena, this);
            arena.PlayerHoop.Scored += OnPlayerHoop;
            arena.BossHoop.Scored += OnBossHoop;
            _fanBaseY = new float[arena.Fans.Count];
            for (int i = 0; i < _fanBaseY.Length; i++) _fanBaseY[i] = arena.Fans[i].localPosition.y;
            UpdateBoard();
        }

        void Update()
        {
            var p = PlayerController.Instance;
            if (CurrentState == ContestState.Waiting && p != null && Arena.Room.Contains(p.transform.position) &&
                p.transform.position.z > Arena.StartLineZ)
                StartCoroutine(IntroRoutine());

            if (Active && CurrentState != ContestState.Won) DespawnExtraBalls();

            // crowd
            _cheer = Mathf.MoveTowards(_cheer, 0, Time.deltaTime * 0.5f);
            for (int i = 0; i < Arena.Fans.Count; i++)
            {
                var f = Arena.Fans[i];
                if (f == null) continue;
                float hop = Mathf.Abs(Mathf.Sin(Time.time * (6f + i % 5) + i)) * (0.05f + 0.45f * _cheer);
                var lp = f.localPosition;
                lp.y = _fanBaseY[i] + hop;
                f.localPosition = lp;
            }

            if (CurrentState == ContestState.Playing && Phase != _lastPhase)
            {
                _lastPhase = Phase;
                if (Phase == 2) Hud.Banner("ROUND 2: WATCH OUT FOR STOMPS!", 2.5f);
                if (Phase == 3)
                {
                    Hud.Banner("FINAL ROUND: THE HOOP IS MOVING!", 2.5f);
                    Arena.PlayerHoopRig.Moving = true;
                }
                Boss.PlayTaunt();
            }
        }

        IEnumerator IntroRoutine()
        {
            CurrentState = ContestState.Intro;
            Arena.Barrier.SetActive(true);
            DespawnExtraBalls();
            EnsureGameBall().transform.position = CenterSpot;
            PlayerScore = BossScore = 0;
            _lastPhase = 1;
            Arena.PlayerHoopRig.Moving = false;
            UpdateBoard();
            Boss.ResetToSpawn();
            Boss.PlayTaunt();
            GameAudio.Play("cheer");
            _cheer = 1f;
            Hud.Banner("KING SWAT: \"Nobody beats the King on HIS court!\"", 3f);
            yield return new WaitForSeconds(3.2f);
            Hud.Banner("First to " + TargetScore + " points wins! 3-pointers count!", 2.5f);
            yield return new WaitForSeconds(2.6f);
            CurrentState = ContestState.Countdown;
            for (int i = 3; i > 0; i--)
            {
                Hud.Banner(i.ToString(), 0.9f);
                GameAudio.Play("tick", 1f, 0.8f);
                yield return new WaitForSeconds(1f);
            }
            Hud.Banner("GO!", 1f);
            GameAudio.Play("whistle");
            CurrentState = ContestState.Playing;
            DespawnExtraBalls();
            PlayerController.Instance.Balls.GiveBall(EnsureGameBall());   // Zander gets the tip-off
            Boss.StartBrain();
        }

        void OnPlayerHoop(Hoop h, Basketball b)
        {
            if (CurrentState != ContestState.Playing || b.LastThrower != BallOwner.Player) return;
            var origin = b.ShotOrigin;
            var rim = h.RimCenter;
            float dist = new Vector2(origin.x - rim.x, origin.z - rim.z).magnitude;
            int pts = !b.IsDunk && dist >= 6.5f ? 3 : 2;
            PlayerScore += pts;
            _cheer = 1f;
            GameAudio.Play("cheer", 0.8f);
            Hud.Toast(pts == 3 ? "THREE POINTER! +3" : b.IsDunk ? "DUNK! +2" : "+2", new Color(0.4f, 0.8f, 1f), 1.2f);
            Boss.PlayHit();
            UpdateBoard();
            if (PlayerScore >= TargetScore) StartCoroutine(WinRoutine());
            else StartCoroutine(InboundAfterScore(h, true));
        }

        void OnBossHoop(Hoop h, Basketball b)
        {
            if (CurrentState != ContestState.Playing || b.LastThrower != BallOwner.Boss) return;
            BossScore += 2;
            Hud.Toast("King Swat scores! +2", new Color(0.8f, 0.6f, 1f), 1.0f);
            UpdateBoard();
            if (BossScore >= TargetScore) StartCoroutine(LoseRoutine());
            else StartCoroutine(InboundAfterScore(h, false));
        }

        IEnumerator WinRoutine()
        {
            CurrentState = ContestState.Won;
            Boss.PlayDefeat();
            Arena.PlayerHoopRig.Moving = false;
            GameAudio.Play("whistle");
            GameAudio.Play("cheer");
            _cheer = 3f;
            Hud.Banner("YOU BEAT KING SWAT!", 3f);
            Rumble.Pulse(0.7f, 0.7f, 0.9f);
            PlayerController.Instance?.Anim?.Trigger("Celebrate");
            for (int i = 0; i < 4; i++)
            {
                Effects.Confetti(Arena.PlayerHoop.RimCenter + Vector3.up * 2f);
                Effects.Confetti(Arena.CourtCenter + new Vector3(Random.Range(-6f, 6f), 6f, Random.Range(-6f, 6f)));
                yield return new WaitForSeconds(0.5f);
            }
            // trophy!
            var trophy = ModelLibrary.Spawn("Trophy", Arena.TrophySpot, Vector3.up * 1.2f, 0f, 1.4f);
            if (trophy == null)
            {
                trophy = Build.Group("Trophy", Arena.TrophySpot, Vector3.up * 1.2f);
                Build.Prim(PrimitiveType.Cylinder, "Cup", trophy.transform, new Vector3(0, 0.5f, 0), new Vector3(0.5f, 0.4f, 0.5f),
                    Mat.Color("#FFC21A", 0.9f, 0.9f), false);
                Build.Prim(PrimitiveType.Sphere, "Ball", trophy.transform, new Vector3(0, 1.1f, 0), Vector3.one * 0.3f, Mat.Color("#E8772E"), false);
            }
            trophy.AddComponent<Spinner>();
            Build.PointLight(Arena.TrophySpot, new Vector3(0, 3, 0), new Color(1f, 0.85f, 0.4f), 8f, 3f);
            GameAudio.Play("solve");
            yield return new WaitForSeconds(2.5f);
            Arena.Barrier.SetActive(false);
            GameManager.Instance.ShowVictory();
        }

        IEnumerator LoseRoutine()
        {
            CurrentState = ContestState.Lost;
            Boss.StopBrain();
            Boss.PlayTaunt();
            Arena.PlayerHoopRig.Moving = false;
            GameAudio.Play("buzz");
            Hud.Banner("King Swat wins this time... TRY AGAIN!", 3.5f);
            yield return new WaitForSeconds(4f);
            RestartContest();
        }

        public void RestartContest()
        {
            StopAllCoroutines();
            Boss.StopBrain();
            Boss.ResetToSpawn();
            var gm = GameManager.Instance;
            gm.Player.Balls.DropBall(false);
            Inbounding = false;
            if (GameBall != null)
            {
                gm.Player.Balls.ForgetBall();
                Destroy(GameBall.gameObject);
                GameBall = null;
            }
            PlayerScore = BossScore = 0;
            Arena.PlayerHoopRig.Moving = false;
            UpdateBoard();
            CurrentState = ContestState.Waiting;
            Arena.Barrier.SetActive(false);
            gm.Player.Teleport(Arena.Room.Spawn, 0);
            Hud.Toast("Walk onto the court to start the rematch!", Color.white, 2.5f);
        }

        void UpdateBoard()
        {
            if (Arena.ScoreboardText != null)
                Arena.ScoreboardText.text = "ZANDER " + PlayerScore + "  -  " + BossScore + " KING";
        }
    }
}
