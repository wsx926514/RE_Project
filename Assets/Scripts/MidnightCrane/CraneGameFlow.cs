using UnityEngine;

namespace MidnightCrane
{
    public enum CraneGameState
    {
        Ready,
        Running,
        Paused,
        Stopped,
        Completed,
        TimeUp
    }

    public enum CraneGameResult
    {
        DesiresCleared,
        TimeExpired
    }

    // ゲーム進行はここでまとめて見る。
    [DisallowMultipleComponent]
    public sealed class CraneGameFlow : MonoBehaviour
    {
        [Header("ゲーム設定")]
        [SerializeField] private CranePhysicsRig rig;
        [SerializeField] private CraneBellPendulum bell;
        [SerializeField, Min(1f)] private float roundSeconds = 30f;
        [SerializeField, Min(1)] private int startingDesires = 108;

        [Header("命中スコア")]
        [SerializeField, Min(0.01f)] private float impactSpeedForOneDesire = 1.35f;
        [SerializeField, Min(1f)] private float impactGrowthPower = 2f;
        [SerializeField, Min(0.01f)] private float referenceHammerMass = 4.06f;
        [SerializeField, Range(0.01f, 1f)] private float desireScoreMultiplier = 0.3f;
        [SerializeField, Min(1)] private int maximumDesiresPerHit = 18;

        [Header("UI")]
        [SerializeField] private CraneGameUI gameUI;

        [Header("命中演出")]
        [SerializeField] private CraneHitEffects hitEffects;

        public float RemainingTime { get; private set; }
        public int HitCount { get; private set; }
        public int RemainingDesires { get; private set; }
        public int LastClearedDesires { get; private set; }
        public float LastImpactSpeed { get; private set; }
        public CraneGameState State { get; private set; } = CraneGameState.Ready;

        private int remainingPhysicsSteps;

        private void Start()
        {
            ResetGame();
        }

        private void FixedUpdate()
        {
            if (State != CraneGameState.Running)
            {
                return;
            }

            // リプレイがずれないよう、物理フレームで数える。
            if (remainingPhysicsSteps <= 0)
            {
                RemainingTime = 0f;
                UpdateTimeUI();
                FinishRound(CraneGameResult.TimeExpired);
                return;
            }

            remainingPhysicsSteps--;
            RemainingTime = Mathf.Min(
                roundSeconds,
                remainingPhysicsSteps * Time.fixedDeltaTime);
            UpdateTimeUI();
        }

        // START。毎回ここで同じ初期状態に戻す。
        public void StartGame()
        {
            if (rig == null)
            {
                Debug.LogError("CranePhysicsRig を設定してください。", this);
                return;
            }

            rig.ResetSimulation();
            if (bell != null)
            {
                bell.ResetBell();
            }

            ResetCounters();
            rig.StartSimulation();
            if (bell != null)
            {
                bell.StartBell();
            }

            SetState(CraneGameState.Running);
        }

        // 一旦止める。
        public void PauseGame()
        {
            if (State != CraneGameState.Running)
            {
                return;
            }

            rig.PauseSimulation();
            if (bell != null)
            {
                bell.PauseBell();
            }

            SetState(CraneGameState.Paused);
        }

        // 止める前の勢いから再開する。
        public void ResumeGame()
        {
            if (State != CraneGameState.Paused)
            {
                return;
            }

            rig.ResumeSimulation();
            if (bell != null)
            {
                bell.ResumeBell();
            }

            SetState(CraneGameState.Running);
        }

        // その場で終了。
        public void StopGame()
        {
            if (State == CraneGameState.Running || State == CraneGameState.Paused)
            {
                rig.StopSimulation();
                if (bell != null)
                {
                    bell.StopBell();
                }

                SetState(CraneGameState.Stopped);
            }
        }

        // END 後はここへ戻す。
        public void ResetGame()
        {
            if (rig != null)
            {
                rig.ResetSimulation();
            }

            if (bell != null)
            {
                bell.ResetBell();
            }

            ResetCounters();
            SetState(CraneGameState.Ready);
        }

        // 鐘の当たりが来た時の処理。
        public void RegisterBellHit(float impactSpeed, Vector2 hitPoint)
        {
            if (State != CraneGameState.Running)
            {
                return;
            }

            float normalizedImpact = impactSpeed / impactSpeedForOneDesire;
            float hammerMass = rig != null
                ? rig.SecondLinkMass
                : referenceHammerMass;
            float hammerMassScale = hammerMass / referenceHammerMass;

            // 速度²に槌側の重さを掛ける。鐘の重さは使わない。
            float reducedScore = Mathf.Pow(normalizedImpact, impactGrowthPower) *
                hammerMassScale *
                desireScoreMultiplier;
            int clearedDesires = Mathf.Clamp(
                Mathf.RoundToInt(reducedScore),
                1,
                maximumDesiresPerHit);
            clearedDesires = Mathf.Min(clearedDesires, RemainingDesires);

            HitCount++;
            LastImpactSpeed = impactSpeed;
            LastClearedDesires = clearedDesires;
            RemainingDesires -= clearedDesires;
            UpdateCounterUI();

            if (hitEffects != null)
            {
                hitEffects.PlayHit(hitPoint, impactSpeed, clearedDesires);
            }

            if (RemainingDesires == 0)
            {
                FinishRound(CraneGameResult.DesiresCleared);
            }
        }

        private void FinishRound(CraneGameResult result)
        {
            rig.StopSimulation();
            if (bell != null)
            {
                bell.StopBell();
            }

            SetState(result == CraneGameResult.DesiresCleared
                ? CraneGameState.Completed
                : CraneGameState.TimeUp);

            if (gameUI != null)
            {
                gameUI.ShowResult(result);
            }
        }

        private void ResetCounters()
        {
            remainingPhysicsSteps = Mathf.CeilToInt(roundSeconds / Time.fixedDeltaTime);
            RemainingTime = roundSeconds;
            HitCount = 0;
            RemainingDesires = startingDesires;
            LastClearedDesires = 0;
            LastImpactSpeed = 0f;
            UpdateTimeUI();
            UpdateCounterUI();

            if (gameUI != null)
            {
                gameUI.HideResult();
            }
        }

        private void SetState(CraneGameState newState)
        {
            State = newState;
        }

        private void UpdateTimeUI()
        {
            if (gameUI != null)
            {
                gameUI.SetTime(RemainingTime);
            }
        }

        private void UpdateCounterUI()
        {
            if (gameUI != null)
            {
                gameUI.SetHitCount(HitCount);
                gameUI.SetDesires(RemainingDesires);
            }
        }

        private void OnValidate()
        {
            roundSeconds = Mathf.Max(1f, roundSeconds);
            startingDesires = Mathf.Max(1, startingDesires);
            impactSpeedForOneDesire = Mathf.Max(0.01f, impactSpeedForOneDesire);
            impactGrowthPower = Mathf.Max(1f, impactGrowthPower);
            referenceHammerMass = Mathf.Max(0.01f, referenceHammerMass);
            desireScoreMultiplier = Mathf.Clamp(desireScoreMultiplier, 0.01f, 1f);
            maximumDesiresPerHit = Mathf.Max(1, maximumDesiresPerHit);
        }
    }
}
