using System.Collections.Generic;
using UnityEngine;

namespace ZanderGame
{
    /// <summary>One area of the game (lobby, a puzzle shrine or the boss arena).</summary>
    public class Room
    {
        public int Index;
        public string Name;
        public string Subtitle;
        public string Objective;
        public string[] Hints = new string[0];
        public float ZMin, ZMax;
        public Vector3 Spawn;
        public float SpawnYaw;
        public Gate Exit;
        public bool Solved;
        public bool IsBoss;
        public bool IsLobby;
        public int Difficulty;   // 0 = lobby, 1..7 = training shrines (in order), 8 = boss
        public readonly List<BallDispenser> Dispensers = new List<BallDispenser>();
        public readonly List<Hoop> Hoops = new List<Hoop>();

        public bool Contains(Vector3 p) => p.z >= ZMin && p.z < ZMax;

        public void ResetBalls()
        {
            foreach (var d in Dispensers)
                if (d != null) d.ResetBalls();
            // free balls lying around in this room that aren't on a plate
            foreach (var b in Basketball.All.ToArray())
            {
                if (b == null || b.IsHeld || b.Source == null || !Contains(b.transform.position)) continue;
                bool onPlate = false;
                foreach (var p in PressurePlate.All)
                    if (p != null && (p.transform.position - b.transform.position).sqrMagnitude < 0.8f) onPlate = true;
                if (!onPlate) Object.Destroy(b.gameObject);
            }
        }
    }

    /// <summary>Everything the boss contest needs from the level.</summary>
    public class BossArena
    {
        public Room Room;
        public Hoop PlayerHoop;
        public Hoop BossHoop;
        public Vector3 BossSpawn;
        public Vector3 CourtCenter;
        public float StartLineZ;
        public GameObject Barrier;
        public readonly List<BallDispenser> Racks = new List<BallDispenser>();
        public TextMesh ScoreboardText;
        public Transform TrophySpot;
        public readonly List<Transform> Fans = new List<Transform>();
        public MovingHoopRig PlayerHoopRig;
    }

    /// <summary>Slides the player's hoop side to side in the final phase of the boss fight.</summary>
    public class MovingHoopRig : MonoBehaviour
    {
        public bool Moving;
        public float Amplitude = 4f;
        public float Speed = 0.55f;
        float _t;
        Vector3 _base;

        void Start() => _base = transform.localPosition;

        void Update()
        {
            if (Moving) _t += Time.deltaTime * Speed;
            else _t = Mathf.MoveTowards(_t, Mathf.Round(_t / Mathf.PI) * Mathf.PI, Time.deltaTime * Speed);
            transform.localPosition = _base + Vector3.right * Mathf.Sin(_t) * Amplitude;
        }
    }
}
