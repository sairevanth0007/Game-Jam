using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace LumenWave
{
    public enum GameState
    {
        Title,
        Playing,
        Paused,
        Won,
    }

    /// <summary>
    /// Game flow: Title → Playing ⇄ Paused → Won. Confirm starts from Title (and restarts after winning),
    /// Pause toggles pause (Time.timeScale = 0), Respawn sends the player to the last checkpoint.
    /// Tracks run time and burn count. Hook UI up to the UnityEvents.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Input (drag from LumenControls > Game)")]
        [SerializeField] InputActionReference pauseAction;
        [SerializeField] InputActionReference respawnAction;
        [SerializeField] InputActionReference confirmAction;

        [Header("References (empty = find in scene)")]
        [SerializeField] PlayerMovement player;
        [SerializeField] CheckpointManager checkpoints;

        [Header("Flow")]
        [Tooltip("Off = skip the title and start playing immediately (handy while testing).")]
        [SerializeField] bool startOnTitle = true;

        [Header("State events (for UI)")]
        public UnityEvent onTitle = new UnityEvent();
        public UnityEvent onPlaying = new UnityEvent();
        public UnityEvent onPaused = new UnityEvent();
        public UnityEvent onWon = new UnityEvent();

        [Header("Other events")]
        public UnityEvent onRespawn = new UnityEvent();
        public UnityEvent onRestart = new UnityEvent();
        public UnityEvent<int> onBurnCountChanged = new UnityEvent<int>();

        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Title;
        public float RunTime { get; private set; }
        public int BurnCount { get; private set; }

        Vector2 spawnFallback;

        /// <summary>Run time as m:ss.ff, e.g. "2:05.37".</summary>
        public string RunTimeText
        {
            get
            {
                int minutes = (int)(RunTime / 60f);
                float seconds = RunTime - minutes * 60f;
                return $"{minutes}:{seconds:00.00}";
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[GameManager] More than one in the scene ({Instance.name}, {name}). Using the newest.", this);
            Instance = this;
        }

        void OnEnable()
        {
            Subscribe(pauseAction, OnPause);
            Subscribe(respawnAction, OnRespawn);
            Subscribe(confirmAction, OnConfirm);
        }

        void OnDisable()
        {
            Unsubscribe(pauseAction, OnPause);
            Unsubscribe(respawnAction, OnRespawn);
            Unsubscribe(confirmAction, OnConfirm);
        }

        void Start()
        {
            if (player == null) player = FindFirstObjectByType<PlayerMovement>();
            if (checkpoints == null) checkpoints = CheckpointManager.Instance;

            if (player == null) Debug.LogWarning("[GameManager] No PlayerMovement in the scene.", this);
            if (checkpoints == null) Debug.LogWarning("[GameManager] No CheckpointManager in the scene; respawn will use the player's start position.", this);

            spawnFallback = player != null ? (Vector2)player.transform.position : Vector2.zero;
            SetState(startOnTitle ? GameState.Title : GameState.Playing);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (State == GameState.Playing)
                RunTime += Time.deltaTime;
        }

        // ---------- Public API (also usable from UI buttons) ----------

        public void StartGame()
        {
            if (State == GameState.Title)
                SetState(GameState.Playing);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void Resume()
        {
            if (State == GameState.Paused)
                SetState(GameState.Playing);
        }

        /// <summary>Send the player back to the current respawn point.</summary>
        public void RespawnPlayer()
        {
            if (player == null) return;

            Vector2 position = checkpoints != null ? checkpoints.RespawnPosition : spawnFallback;
            player.Teleport(position);
            onRespawn.Invoke();
        }

        /// <summary>Call when light burns the player out (e.g. from LightDamage). Counts the burn and respawns.</summary>
        public void PlayerBurned()
        {
            if (State != GameState.Playing) return;

            BurnCount++;
            onBurnCountChanged.Invoke(BurnCount);
            RespawnPlayer();
        }

        /// <summary>Called by WinZone.</summary>
        public void Win()
        {
            if (State == GameState.Playing)
                SetState(GameState.Won);
        }

        /// <summary>New run in the same scene: initial spawn, checkpoints re-armed, timer and burns reset.</summary>
        public void RestartRun()
        {
            RunTime = 0f;
            BurnCount = 0;
            onBurnCountChanged.Invoke(BurnCount);

            if (checkpoints != null) checkpoints.ResetAll();
            RespawnPlayer();
            onRestart.Invoke();
            SetState(GameState.Playing);
        }

        // ---------- State ----------

        void SetState(GameState next)
        {
            State = next;

            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            if (player != null)
                player.SetControlEnabled(next == GameState.Playing);

            switch (next)
            {
                case GameState.Title: onTitle.Invoke(); break;
                case GameState.Playing: onPlaying.Invoke(); break;
                case GameState.Paused: onPaused.Invoke(); break;
                case GameState.Won: onWon.Invoke(); break;
            }
            StateChanged?.Invoke(next);
        }

        // ---------- Input ----------

        void OnPause(InputAction.CallbackContext _) => TogglePause();

        void OnRespawn(InputAction.CallbackContext _)
        {
            if (State == GameState.Playing)
                RespawnPlayer();
        }

        void OnConfirm(InputAction.CallbackContext _)
        {
            if (State == GameState.Title) StartGame();
            else if (State == GameState.Won) RestartRun();
        }

        static void Subscribe(InputActionReference reference, Action<InputAction.CallbackContext> callback)
        {
            if (reference != null && reference.action != null)
                reference.action.performed += callback;
        }

        static void Unsubscribe(InputActionReference reference, Action<InputAction.CallbackContext> callback)
        {
            if (reference != null && reference.action != null)
                reference.action.performed -= callback;
        }
    }
}
