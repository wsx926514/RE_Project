using UnityEngine;

namespace MidnightCrane
{
    // 吊り腕と二重振り子の本体。
    [DisallowMultipleComponent]
    public sealed class CranePhysicsRig : MonoBehaviour
    {
        private const float MinLength = 0.01f;

        private enum RigMode
        {
            Setup,
            Running,
            Paused,
            Stopped
        }

        [Header("物理物体")]
        [SerializeField] private Rigidbody2D rootAnchor;
        [SerializeField] private Rigidbody2D firstLink;
        [SerializeField] private Rigidbody2D secondLink;
        [SerializeField] private HingeJoint2D firstJoint;
        [SerializeField] private HingeJoint2D secondJoint;
        [SerializeField] private BoxCollider2D firstLinkCollider;
        [SerializeField] private BoxCollider2D secondLinkCollider;

        [Header("表示")]
        [SerializeField] private SpriteRenderer boomRenderer;
        [SerializeField] private SpriteRenderer firstLinkRenderer;
        [SerializeField] private SpriteRenderer secondLinkRenderer;
        [SerializeField, Min(MinLength)] private float boomWidth = 0.62f;

        [Header("任意の物体")]
        [SerializeField] private Transform boomPivot;
        [SerializeField] private Collider2D firstJointMassCollider;
        [SerializeField] private Collider2D endMassCollider;

        [Header("初期位置と長さ")]
        [SerializeField, Min(MinLength)] private float boomLength = 4.15f;
        [SerializeField] private float minimumBoomAngle = 20f;
        [SerializeField] private float maximumBoomAngle = 80f;
        [SerializeField] private float boomAngle = 57f;
        [SerializeField, Min(0.1f)] private float boomAngleStep = 5f;
        [SerializeField, Min(MinLength)] private float firstLinkLength = 2.75f;
        [SerializeField, Min(MinLength)] private float secondLinkLength = 2.9f;
        [SerializeField] private float firstLinkWorldAngle = -13f;
        [SerializeField] private float secondLinkWorldAngle = 47f;
        [SerializeField, Min(MinLength)] private float firstLinkWidth = 0.25f;
        [SerializeField, Min(MinLength)] private float secondLinkWidth = 0.22f;

        [Header("質量")]
        [SerializeField, Min(0f)] private float firstLinkDensity = 0.22f;
        [SerializeField, Min(0f)] private float secondLinkDensity = 0.16f;
        [SerializeField, Min(0f)] private float firstJointMassDensity = 8.5f;
        [SerializeField, Min(0f)] private float endMassDensity = 14f;

        [Header("物理設定")]
        [SerializeField] private bool startSimulatingOnAwake;
        [SerializeField] private float gravityScale = 1.65f;
        [SerializeField, Min(0f)] private float linearDamping = 0.005f;
        [SerializeField, Min(0f)] private float angularDamping = 0.012f;
        [SerializeField] private CollisionDetectionMode2D collisionDetection =
            CollisionDetectionMode2D.Continuous;
        [SerializeField] private RigidbodyInterpolation2D interpolation =
            RigidbodyInterpolation2D.None;
        [SerializeField] private PhysicsMaterial2D linkMaterial;
        [SerializeField] private PhysicsMaterial2D massMaterial;

        private RigMode mode = RigMode.Setup;
        private Vector2 rootPoint;
        private Vector2 firstJointPoint;
        private Vector2 endPoint;
        private Vector2 pausedFirstVelocity;
        private Vector2 pausedSecondVelocity;
        private float pausedFirstAngularVelocity;
        private float pausedSecondAngularVelocity;

        public float BoomAngle => boomAngle;
        public float FirstLinkLength => firstLinkLength;
        public float SecondLinkLength => secondLinkLength;
        public float MinimumBoomAngle => minimumBoomAngle;
        public float MaximumBoomAngle => maximumBoomAngle;
        public float FirstLinkMass => firstLink != null ? firstLink.mass : 0f;
        public float SecondLinkMass => secondLink != null ? secondLink.mass : 0f;
        public Rigidbody2D EndMassBody => secondLink;
        public Collider2D EndMassCollider => endMassCollider;
        public bool CanEditSetup => mode == RigMode.Setup;
        public bool IsSimulating => mode == RigMode.Running;
        public bool IsPaused => mode == RigMode.Paused;

        // 槌の先端速度。回転ぶんもここに入る。
        public Vector2 GetEndMassVelocityAt(Vector2 worldPoint)
        {
            return secondLink != null
                ? secondLink.GetPointVelocity(worldPoint)
                : Vector2.zero;
        }

        private void Awake()
        {
            if (!ValidateRequiredReferences(true))
            {
                enabled = false;
                return;
            }

            ApplyConfiguration();
            if (startSimulatingOnAwake)
            {
                StartSimulation();
            }
        }

        // Inspector の値から待機姿勢を作り直す。
        [ContextMenu("初期姿勢を適用")]
        public void ApplyConfiguration()
        {
            if (!ValidateRequiredReferences(Application.isPlaying))
            {
                return;
            }

            ClampSettings();
            mode = RigMode.Setup;
            ConfigureRootPoint();
            UpdatePointsFromSettings();
            ConfigurePhysicsParts();
            PlaceBodies();
        }

        // 待機を外して物理に渡す。
        [ContextMenu("物理を開始")]
        public void StartSimulation()
        {
            if (mode != RigMode.Setup)
            {
                return;
            }

            firstLink.bodyType = RigidbodyType2D.Dynamic;
            secondLink.bodyType = RigidbodyType2D.Dynamic;
            Physics2D.SyncTransforms();
            firstLink.WakeUp();
            secondLink.WakeUp();
            mode = RigMode.Running;
        }

        // 速度を取ってから固める。
        [ContextMenu("物理を一時停止")]
        public void PauseSimulation()
        {
            if (mode != RigMode.Running)
            {
                return;
            }

            pausedFirstVelocity = firstLink.linearVelocity;
            pausedSecondVelocity = secondLink.linearVelocity;
            pausedFirstAngularVelocity = firstLink.angularVelocity;
            pausedSecondAngularVelocity = secondLink.angularVelocity;

            FreezeBody(firstLink);
            FreezeBody(secondLink);
            mode = RigMode.Paused;
        }

        // 止める前の勢いを戻す。
        [ContextMenu("物理を再開")]
        public void ResumeSimulation()
        {
            if (mode != RigMode.Paused)
            {
                return;
            }

            firstLink.bodyType = RigidbodyType2D.Dynamic;
            secondLink.bodyType = RigidbodyType2D.Dynamic;
            firstLink.linearVelocity = pausedFirstVelocity;
            secondLink.linearVelocity = pausedSecondVelocity;
            firstLink.angularVelocity = pausedFirstAngularVelocity;
            secondLink.angularVelocity = pausedSecondAngularVelocity;
            firstLink.WakeUp();
            secondLink.WakeUp();
            mode = RigMode.Running;
        }

        // END ならその場で止める。
        [ContextMenu("物理を停止")]
        public void StopSimulation()
        {
            if (mode == RigMode.Running || mode == RigMode.Paused)
            {
                FreezeBody(firstLink);
                FreezeBody(secondLink);
                mode = RigMode.Stopped;
            }
        }

        // いつもの待機姿勢へ。
        [ContextMenu("初期姿勢に戻す")]
        public void ResetSimulation()
        {
            ApplyConfiguration();
        }

        // 矢印から来る角度変更。
        public void SetBoomAngle(float angle)
        {
            if (!CanEditSetup)
            {
                return;
            }

            float newAngle = Mathf.Clamp(angle, minimumBoomAngle, maximumBoomAngle);
            if (Mathf.Approximately(newAngle, boomAngle))
            {
                return;
            }

            float angleDifference = newAngle - boomAngle;

            // 前の二本と槌も同じ角度だけ連れていく。
            boomAngle = newAngle;
            firstLinkWorldAngle += angleDifference;
            secondLinkWorldAngle += angleDifference;
            ApplyConfiguration();
        }

        // 一回だけ上げたい時用。
        public void IncreaseBoomAngle()
        {
            SetBoomAngle(boomAngle + boomAngleStep);
        }

        // 一回だけ下げたい時用。
        public void DecreaseBoomAngle()
        {
            SetBoomAngle(boomAngle - boomAngleStep);
        }

        // 二本の長さだけ更新。
        public void SetLinkLengths(float firstLength, float secondLength)
        {
            if (!CanEditSetup)
            {
                return;
            }

            firstLinkLength = Mathf.Max(MinLength, firstLength);
            secondLinkLength = Mathf.Max(MinLength, secondLength);
            ApplyConfiguration();
        }

        // 待機中の向きだけ更新。
        public void SetInitialLinkAngles(float firstAngle, float secondAngle)
        {
            if (!CanEditSetup)
            {
                return;
            }

            firstLinkWorldAngle = firstAngle;
            secondLinkWorldAngle = secondAngle;
            ApplyConfiguration();
        }

        // 姿勢をまとめて差し替える時用。
        public void SetConfiguration(
            float newBoomAngle,
            float newFirstLength,
            float newSecondLength,
            float newFirstWorldAngle,
            float newSecondWorldAngle)
        {
            if (!CanEditSetup)
            {
                return;
            }

            boomAngle = Mathf.Clamp(newBoomAngle, minimumBoomAngle, maximumBoomAngle);
            firstLinkLength = Mathf.Max(MinLength, newFirstLength);
            secondLinkLength = Mathf.Max(MinLength, newSecondLength);
            firstLinkWorldAngle = newFirstWorldAngle;
            secondLinkWorldAngle = newSecondWorldAngle;
            ApplyConfiguration();
        }

        // 槌の重さ調整。
        public void SetEndMassDensity(float density)
        {
            if (!CanEditSetup)
            {
                return;
            }

            endMassDensity = Mathf.Max(0f, density);
            if (endMassCollider != null && secondLink != null)
            {
                PrepareBodyForMassUpdate(secondLink);
                endMassCollider.density = endMassDensity;
                RefreshMass(secondLink);
                FreezeBody(secondLink);
            }
        }

        // 調整ハンドルが追いかける座標。
        public Vector2 GetSetupPoint(CraneSetupPoint point)
        {
            switch (point)
            {
                case CraneSetupPoint.BoomEnd:
                    return rootPoint;
                case CraneSetupPoint.FirstJoint:
                    return firstJointPoint;
                default:
                    return endPoint;
            }
        }

        // ドラッグ中の姿勢更新。
        public bool MoveSetupPoint(CraneSetupPoint point, Vector2 newPosition)
        {
            if (!CanEditSetup)
            {
                return false;
            }

            if (point == CraneSetupPoint.BoomEnd)
            {
                if (boomPivot == null)
                {
                    return false;
                }

                Vector2 direction = newPosition - (Vector2)boomPivot.position;
                if (direction.sqrMagnitude < MinLength * MinLength)
                {
                    return false;
                }

                float newAngle = Mathf.Clamp(
                    VectorAngle(direction),
                    minimumBoomAngle,
                    maximumBoomAngle);
                float angleDifference = newAngle - boomAngle;

                // 矢印と同じように、前側はまとめて回す。
                boomAngle = newAngle;
                firstLinkWorldAngle += angleDifference;
                secondLinkWorldAngle += angleDifference;
                rootPoint = (Vector2)boomPivot.position +
                    DirectionFromAngle(boomAngle) * boomLength;
                UpdatePointsFromSettings();
            }
            else if (point == CraneSetupPoint.FirstJoint)
            {
                Vector2 direction = newPosition - rootPoint;
                if (direction.sqrMagnitude < MinLength * MinLength)
                {
                    direction = DirectionFromAngle(firstLinkWorldAngle);
                }

                firstJointPoint = rootPoint + direction.normalized *
                    Mathf.Max(MinLength, direction.magnitude);
            }
            else
            {
                Vector2 direction = newPosition - firstJointPoint;
                if (direction.sqrMagnitude < MinLength * MinLength)
                {
                    direction = DirectionFromAngle(secondLinkWorldAngle);
                }

                endPoint = firstJointPoint + direction.normalized *
                    Mathf.Max(MinLength, direction.magnitude);
            }

            ReadSettingsFromPoints();
            ConfigurePhysicsParts();
            PlaceBodies();
            return true;
        }

        // Scene で置いた形を初期値として拾う。
        [ContextMenu("現在の配置を初期姿勢にする")]
        public void CaptureCurrentScenePose()
        {
            if (!ValidateRequiredReferences(true))
            {
                return;
            }

            rootPoint = rootAnchor.position;
            Vector2 toFirstCenter = firstLink.position - rootPoint;
            Vector2 firstEnd = rootPoint + toFirstCenter * 2f;
            Vector2 toSecondCenter = secondLink.position - firstEnd;

            if (toFirstCenter.sqrMagnitude < MinLength * MinLength ||
                toSecondCenter.sqrMagnitude < MinLength * MinLength)
            {
                Debug.LogError("三つの支点を離して配置してください。", this);
                return;
            }

            firstJointPoint = firstEnd;
            endPoint = firstEnd + toSecondCenter * 2f;
            ReadSettingsFromPoints();

            if (boomPivot != null)
            {
                Vector2 boomDirection = rootPoint - (Vector2)boomPivot.position;
                boomLength = boomDirection.magnitude;
                boomAngle = Mathf.Clamp(
                    VectorAngle(boomDirection),
                    minimumBoomAngle,
                    maximumBoomAngle);
            }

            ApplyConfiguration();
        }

        // 参照切れを見るための自分用チェック。
        [ContextMenu("物理設定を確認")]
        public void CheckConfiguration()
        {
            if (ValidateRequiredReferences(true))
            {
                Debug.Log("物理設定に必要な参照があります。", this);
            }
        }

        private void ConfigureRootPoint()
        {
            rootAnchor.bodyType = RigidbodyType2D.Kinematic;
            rootAnchor.gravityScale = 0f;
            rootAnchor.linearVelocity = Vector2.zero;
            rootAnchor.angularVelocity = 0f;
            rootAnchor.interpolation = interpolation;

            if (boomPivot != null)
            {
                rootAnchor.position = (Vector2)boomPivot.position +
                    DirectionFromAngle(boomAngle) * boomLength;
            }

            rootPoint = rootAnchor.position;
        }

        private void UpdatePointsFromSettings()
        {
            firstJointPoint = rootPoint +
                DirectionFromAngle(firstLinkWorldAngle) * firstLinkLength;
            endPoint = firstJointPoint +
                DirectionFromAngle(secondLinkWorldAngle) * secondLinkLength;
        }

        private void ReadSettingsFromPoints()
        {
            Vector2 firstVector = firstJointPoint - rootPoint;
            Vector2 secondVector = endPoint - firstJointPoint;

            firstLinkLength = Mathf.Max(MinLength, firstVector.magnitude);
            secondLinkLength = Mathf.Max(MinLength, secondVector.magnitude);
            firstLinkWorldAngle = VectorAngle(firstVector);
            secondLinkWorldAngle = VectorAngle(secondVector);
        }

        private void ConfigurePhysicsParts()
        {
            ConfigureBody(firstLink);
            ConfigureBody(secondLink);
            ConfigureColliders();
            ConfigureJoints();
            RefreshMass(firstLink);
            RefreshMass(secondLink);
            FreezeBody(firstLink);
            FreezeBody(secondLink);
        }

        private void ConfigureBody(Rigidbody2D body)
        {
            PrepareBodyForMassUpdate(body);
            body.gravityScale = gravityScale;
            body.linearDamping = linearDamping;
            body.angularDamping = angularDamping;
            body.collisionDetectionMode = collisionDetection;
            body.interpolation = interpolation;
        }

        // Auto Mass を計算し直す前の下準備。
        private static void PrepareBodyForMassUpdate(Rigidbody2D body)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.useAutoMass = true;
        }

        private void ConfigureColliders()
        {
            firstLinkCollider.size = new Vector2(firstLinkLength, firstLinkWidth);
            firstLinkCollider.offset = Vector2.zero;
            firstLinkCollider.density = firstLinkDensity;
            SetLinkRendererSize(
                ref firstLinkRenderer,
                firstLink,
                firstLinkLength,
                firstLinkWidth);

            secondLinkCollider.size = new Vector2(secondLinkLength, secondLinkWidth);
            secondLinkCollider.offset = Vector2.zero;
            secondLinkCollider.density = secondLinkDensity;
            SetLinkRendererSize(
                ref secondLinkRenderer,
                secondLink,
                secondLinkLength,
                secondLinkWidth);

            if (linkMaterial != null)
            {
                firstLinkCollider.sharedMaterial = linkMaterial;
                secondLinkCollider.sharedMaterial = linkMaterial;
            }

            SetMassCollider(
                firstJointMassCollider,
                firstLink,
                firstLinkLength * 0.5f,
                firstJointMassDensity);
            SetMassCollider(
                endMassCollider,
                secondLink,
                secondLinkLength * 0.5f,
                endMassDensity);

            // 当たるのは槌だけ。棒はすり抜けさせる。
            SetCollisionEnabled(firstLinkCollider, false);
            SetCollisionEnabled(secondLinkCollider, false);
            SetCollisionEnabled(firstJointMassCollider, false);
            SetCollisionEnabled(endMassCollider, true);
        }

        private static void SetCollisionEnabled(Collider2D target, bool value)
        {
            if (target == null)
            {
                return;
            }

            target.isTrigger = false;
            target.includeLayers = 0;
            target.excludeLayers = value ? 0 : Physics2D.AllLayers;
        }

        private void SetMassCollider(
            Collider2D massCollider,
            Rigidbody2D owner,
            float localX,
            float density)
        {
            if (massCollider == null)
            {
                return;
            }

            if (massCollider.attachedRigidbody != owner)
            {
                Debug.LogWarning(massCollider.name + " を正しい連桿の子にしてください。", massCollider);
                return;
            }

            if (massCollider.transform == owner.transform)
            {
                massCollider.offset = new Vector2(localX, 0f);
            }
            else
            {
                massCollider.transform.localPosition = new Vector3(localX, 0f, 0f);
                massCollider.transform.localRotation = Quaternion.identity;
                massCollider.offset = Vector2.zero;
            }

            massCollider.density = density;
            if (massMaterial != null)
            {
                massCollider.sharedMaterial = massMaterial;
            }
        }

        private static void SetLinkRendererSize(
            ref SpriteRenderer linkRenderer,
            Rigidbody2D linkBody,
            float length,
            float width)
        {
            if (linkRenderer == null)
            {
                linkRenderer = linkBody.GetComponent<SpriteRenderer>();
            }

            if (linkRenderer == null)
            {
                return;
            }

            linkRenderer.drawMode = SpriteDrawMode.Sliced;
            linkRenderer.size = new Vector2(length, width);
        }

        private void ConfigureJoints()
        {
            firstJoint.connectedBody = rootAnchor;
            firstJoint.autoConfigureConnectedAnchor = false;
            firstJoint.anchor = new Vector2(-firstLinkLength * 0.5f, 0f);
            firstJoint.connectedAnchor = Vector2.zero;
            firstJoint.enableCollision = false;

            secondJoint.connectedBody = firstLink;
            secondJoint.autoConfigureConnectedAnchor = false;
            secondJoint.anchor = new Vector2(-secondLinkLength * 0.5f, 0f);
            secondJoint.connectedAnchor = new Vector2(firstLinkLength * 0.5f, 0f);
            secondJoint.enableCollision = false;
        }

        private void PlaceBodies()
        {
            rootAnchor.position = rootPoint;
            SetBodyBetween(firstLink, rootPoint, firstJointPoint);
            SetBodyBetween(secondLink, firstJointPoint, endPoint);
            UpdateBoomRenderer();
            Physics2D.SyncTransforms();
        }

        // 固定吊り腕を二点の間に置き直す。
        private void UpdateBoomRenderer()
        {
            if (boomRenderer == null || boomPivot == null)
            {
                return;
            }

            Vector2 start = boomPivot.position;
            Vector2 end = rootPoint;
            Vector2 direction = end - start;
            Transform visual = boomRenderer.transform;
            Vector3 oldPosition = visual.position;

            visual.position = new Vector3(
                (start.x + end.x) * 0.5f,
                (start.y + end.y) * 0.5f,
                oldPosition.z);
            visual.rotation = Quaternion.Euler(0f, 0f, VectorAngle(direction));
            boomRenderer.drawMode = SpriteDrawMode.Sliced;
            boomRenderer.size = new Vector2(direction.magnitude, boomWidth);
        }

        private void ClampSettings()
        {
            boomLength = Mathf.Max(MinLength, boomLength);
            firstLinkLength = Mathf.Max(MinLength, firstLinkLength);
            secondLinkLength = Mathf.Max(MinLength, secondLinkLength);
            boomAngleStep = Mathf.Max(0.1f, boomAngleStep);
            boomWidth = Mathf.Max(MinLength, boomWidth);
            firstLinkWidth = Mathf.Max(MinLength, firstLinkWidth);
            secondLinkWidth = Mathf.Max(MinLength, secondLinkWidth);

            if (maximumBoomAngle < minimumBoomAngle)
            {
                maximumBoomAngle = minimumBoomAngle;
            }

            boomAngle = Mathf.Clamp(boomAngle, minimumBoomAngle, maximumBoomAngle);
            firstLinkDensity = Mathf.Max(0f, firstLinkDensity);
            secondLinkDensity = Mathf.Max(0f, secondLinkDensity);
            firstJointMassDensity = Mathf.Max(0f, firstJointMassDensity);
            endMassDensity = Mathf.Max(0f, endMassDensity);
            linearDamping = Mathf.Max(0f, linearDamping);
            angularDamping = Mathf.Max(0f, angularDamping);
        }

        private bool ValidateRequiredReferences(bool logError)
        {
            bool valid = rootAnchor != null &&
                firstLink != null &&
                secondLink != null &&
                firstJoint != null &&
                secondJoint != null &&
                firstLinkCollider != null &&
                secondLinkCollider != null;

            valid &= firstJoint == null || firstJoint.attachedRigidbody == firstLink;
            valid &= secondJoint == null || secondJoint.attachedRigidbody == secondLink;
            valid &= firstLinkCollider == null || firstLinkCollider.attachedRigidbody == firstLink;
            valid &= secondLinkCollider == null || secondLinkCollider.attachedRigidbody == secondLink;

            if (!valid && logError)
            {
                Debug.LogError(
                    "Rigidbody2D、HingeJoint2D、BoxCollider2D の参照を確認してください。",
                    this);
            }

            return valid;
        }

        private static void FreezeBody(Rigidbody2D body)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private static void RefreshMass(Rigidbody2D body)
        {
            body.useAutoMass = false;
            body.useAutoMass = true;
        }

        private static void SetBodyBetween(Rigidbody2D body, Vector2 start, Vector2 end)
        {
            Vector2 direction = end - start;
            body.position = (start + end) * 0.5f;
            body.rotation = VectorAngle(direction);
        }

        private static Vector2 DirectionFromAngle(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static float VectorAngle(Vector2 direction)
        {
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private void OnValidate()
        {
            ClampSettings();
        }
    }
}
