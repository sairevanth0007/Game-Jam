using System;
using System.Collections;
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
        LevelComplete,
        /// <summary>The last level is done.</summary>
        Won,
    }

    /// <summary>
    /// Game flow: Title → Playing ⇄ Paused → LevelComplete → next level … → Won.
    /// Owns level loading (via LevelLoader), echo counting, death/respawn and the run statistics.
    /// Confirm (or Jump) continues from the title and the level-complete screen, Pause toggles pause,
    /// Respawn sends the player to the last checkpoint. Everything is also announced through GameEvents.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Input (drag from LumenControls > Game; empty = use LumenInput's actions)")]
        [SerializeField] InputActionReference pauseAction;
        [SerializeField] InputActionReference respawnAction;
        [SerializeField] InputActionReference confirmAction;

        [Header("References (empty = set by GameBootstrap / found in scene)")]
        [SerializeField] PlayerMovement player;
        [SerializeField] CheckpointManager checkpoints;
        [SerializeField] LevelLoader levels;
        [SerializeField] CameraController cameraController;

        [Header("Flow")]
        [Tooltip("Off = skip the title and start playing immediately (handy while testing).")]
        [SerializeField] bool startOnTitle;
        [Tooltip("Level index to start on (0 = first file).")]
        [SerializeField] int startLevel;
        [Tooltip("Seconds between dissolving and reforming at the checkpoint.")]
        [SerializeField] float respawnDelay = 0.75f;
        [Tooltip("Falling this far below the bottom of the level counts as lost in the void.")]
        [SerializeField] float fallDeathDepth = 3f;
        [Tooltip("The level-complete screen ignores Continue for this long, so a held jump doesn't skip it.")]
        [SerializeField] float continueLockTime = 0.6f;

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
        /// <summary>Time of the whole run (all levels).</summary>
        public float RunTime { get; private set; }
        /// <summary>Burns over the whole run.</summary>
        public int BurnCount { get; private set; }
        public float LevelTime { get; private set; }
        public int LevelBurns { get; private set; }
        public int EchoesCollected { get; private set; }
        public int EchoTotal { get; private set; }
        public int LevelIndex { get; private set; }
        public LevelData CurrentLevel { get; private set; }
        public bool IsDying { get; private set; }
        public PlayerMovement Player => player;

        InputAction pause, respawn, confirm, jump;
        bool continueRequested;
        float stateTime;
        Coroutine respawnRoutine;

        /// <summary>Time as m:ss.ff, e.g. "2:05.37".</summary>
        public static string FormatTime(float time)
        {
            int minutes = (int)(time / 60f);
            float seconds = time - minutes * 60f;
            return $"{minutes}:{seconds:00.00}";
        }

        public string RunTimeText => FormatTime(RunTime);

        void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[GameManager] More than one in the scene ({Instance.name}, {name}). Using the newest.", this);
            Instance = this;
        }

        public void Configure(PlayerMovement player, CheckpointManager checkpoints, LevelLoader levels, CameraController cameraController, bool startOnTitle, int startLevel)
        {
            this.startLevel = startLevel;
            this.player = player;
            this.checkpoints = checkpoints;
            this.levels = levels;
            this.cameraController = cameraController;
            this.startOnTitle = startOnTitle;
        }

        void OnEnable()
        {
            pause = LumenInput.Resolve(pauseAction, "Game/Pause");
            respawn = LumenInput.Resolve(respawnAction, "Game/Respawn");
            confirm = LumenInput.Resolve(confirmAction, "Game/Confirm");
            jump = LumenInput.FindAction("Player/Jump");
            Subscribe(pause, OnPause);
            Subscribe(respawn, OnRespawn);
            Subscribe(confirm, OnConfirm);
            Subscribe(jump, OnConfirm);
        }

        void OnDisable()
        {
            Unsubscribe(pause, OnPause);
            Unsubscribe(respawn, OnRespawn);
            Unsubscribe(confirm, OnConfirm);
            Unsubscribe(jump, OnConfirm);
        }

        void Start()
        {
            if (player == null) player = FindFirstObjectByType<PlayerMovement>();
            if (checkpoints == null) checkpoints = CheckpointManager.Instance;
            if (levels == null) levels = FindFirstObjectByType<LevelLoader>();
            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();

            if (player == null) Debug.LogWarning("[GameManager] No PlayerMovement in the scene.", this);
            if (checkpoints == null) Debug.LogWarning("[GameManager] No CheckpointManager in the scene.", this);

            if (startOnTitle)
            {
                if (player != null) player.SetFrozen(true); // nothing to stand on until a level loads
                SetState(GameState.Title);
            }
            else
            {
                LoadLevel(startLevel);
            }
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            stateTime += Time.unscaledDeltaTime;

            if (State == GameState.Playing)
            {
                RunTime += Time.deltaTime;
                LevelTime += Time.deltaTime;

                if (!IsDying && player != null && levels != null && player.transform.position.y < levels.Bounds.yMin - fallDeathDepth)
                    KillPlayer(DeathCause.Fell);
            }

            // Handled here rather than inside the input callback, so the press that continues
            // can't also be read as a jump by the freshly re-enabled player.
            if (continueRequested)
            {
                continueRequested = false;
                Continue();
            }
        }

        // ---------- Public API (also usable from UI buttons) ----------

        public void StartGame()
        {
            if (State == GameState.Title)
            {
                RunTime = 0f;
                BurnCount = 0;
                LoadLevel(startLevel);
            }
        }

        /// <summary>Builds level [index] and puts the player at its start.</summary>
        public void LoadLevel(int index)
        {
            if (levels == null)
            {
                Debug.LogError("[GameManager] No LevelLoader.", this);
                return;
            }

            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = null;
            IsDying = false;

            LevelData level = levels.Load(index);
            if (level == null) return;

            CurrentLevel = level;
            LevelIndex = levels.CurrentIndex;
            LevelTime = 0f;
            LevelBurns = 0;
            EchoesCollected = 0;
            EchoTotal = levels.EchoTotal;

            Vector2 spawn = levels.SpawnPosition;
            if (checkpoints != null) checkpoints.SetInitialSpawn(spawn);
            if (player != null)
            {
                player.SetFrozen(false);
                player.Teleport(spawn);
            }
            if (cameraController != null)
            {
                cameraController.SetBounds(levels.Bounds);
                cameraController.Snap();
            }

            SetState(GameState.Playing);
            GameEvents.RaiseLevelStarted(level, LevelIndex);
            GameEvents.RaisePlayerRespawned(spawn);
        }

        public void RestartLevel() => LoadLevel(LevelIndex);

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

        public void CollectEcho(Vector2 position)
        {
            EchoesCollected++;
            GameEvents.RaiseEchoCollected(position, EchoesCollected, EchoTotal);
        }

        /// <summary>Dissolve, count it, and reform at the last checkpoint after a short delay.</summary>
        public void KillPlayer(DeathCause cause)
        {
            if (State != GameState.Playing || IsDying || player == null) return;

            IsDying = true;
            BurnCount++;
            LevelBurns++;
            onBurnCountChanged.Invoke(BurnCount);

            Vector2 position = player.transform.position;
            player.SetControlEnabled(false);
            player.SetFrozen(true);
            GameEvents.RaisePlayerDied(position, cause);
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        /// <summary>Called when light burns the player out (LightDamage).</summary>
        public void PlayerBurned() => KillPlayer(DeathCause.Burned);

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            respawnRoutine = null;
            RespawnPlayer();
        }

        /// <summary>Send the player back to the current respawn point (no burn counted).</summary>
        public void RespawnPlayer()
        {
            if (player == null) return;

            Vector2 position = checkpoints != null ? checkpoints.RespawnPosition : (levels != null ? levels.SpawnPosition : Vector2.zero);
            player.SetFrozen(false);
            player.Teleport(position);
            IsDying = false;
            player.SetControlEnabled(State == GameState.Playing);
            onRespawn.Invoke();
            GameEvents.RaisePlayerRespawned(position);
        }

        /// <summary>Called by the level door (WinZone).</summary>
        public void CompleteLevel()
        {
            if (State != GameState.Playing || IsDying) return;

            SetState(GameState.LevelComplete);
            GameEvents.RaiseLevelCompleted(new LevelResult
            {
                LevelIndex = LevelIndex,
                LevelName = CurrentLevel != null ? CurrentLevel.Name : "",
                Time = LevelTime,
                Echoes = EchoesCollected,
                EchoTotal = EchoTotal,
                Burns = LevelBurns,
                IsLastLevel = levels == null || LevelIndex >= levels.LevelCount - 1,
            });
        }

        /// <summary>Kept for older callers: same as CompleteLevel.</summary>
        public void Win() => CompleteLevel();

        /// <summary>From the level-complete screen: next level, or the end screen after the last one.</summary>
        public void Continue()
        {
            if (stateTime < continueLockTime) return;

            if (State == GameState.Title)
            {
                StartGame();
            }
            else if (State == GameState.LevelComplete)
            {
                if (levels != null && LevelIndex < levels.LevelCount - 1) LoadLevel(LevelIndex + 1);
                else SetState(GameState.Won);
            }
            else if (State == GameState.Won)
            {
                RestartRun();
            }
        }

        /// <summary>New run from the first level: timer and burns reset.</summary>
        public void RestartRun()
        {
            RunTime = 0f;
            BurnCount = 0;
            onBurnCountChanged.Invoke(BurnCount);
            onRestart.Invoke();
            LoadLevel(startLevel);
        }

        // ---------- State ----------

        void SetState(GameState next)
        {
            State = next;
            stateTime = 0f;

            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            if (player != null)
                player.SetControlEnabled(next == GameState.Playing && !IsDying);

            switch (next)
            {
                case GameState.Title: onTitle.Invoke(); break;
                case GameState.Playing: onPlaying.Invoke(); break;
                case GameState.Paused: onPaused.Invoke(); break;
                case GameState.Won: onWon.Invoke(); break;
            }
            StateChanged?.Invoke(next);
            GameEvents.RaiseGameStateChanged(next);
        }

        // ---------- Input ----------

        void OnPause(InputAction.CallbackContext _) => TogglePause();

        void OnRespawn(InputAction.CallbackContext _)
        {
            if (IsDying) return;
            if (State == GameState.Paused) SetState(GameState.Playing);
            if (State == GameState.Playing) RespawnPlayer();
        }

        void OnConfirm(InputAction.CallbackContext _)
        {
            if (State == GameState.Title || State == GameState.LevelComplete || State == GameState.Won)
                continueRequested = true;
        }

        static void Subscribe(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action != null)
                action.performed += callback;
        }

        static void Unsubscribe(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action != null)
                action.performed -= callback;
        }
    }
}
