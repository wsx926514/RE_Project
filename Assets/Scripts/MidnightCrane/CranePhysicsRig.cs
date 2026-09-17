using UnityEngine;

namespace MidnightCrane
{
    /// <summary>
    /// 設定雙節吊臂擺錘，設定完成後將所有運動交給 Unity Physics 2D。
    /// 此元件不會建立物件、畫面、輸入或 UI。
    ///
    /// 兩段連桿皆以本地座標 +X，表示從鉸鏈指向自由端的方向。
    /// 選用的質量碰撞器應放在子物件上，而且子物件不可擁有 Rigidbody2D。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CranePhysicsRig : MonoBehaviour
    {
        // 避免長度或寬度為 0，造成碰撞器與關節計算失效。
        private const float MinimumDimension = 0.01f;

        [Header("必要的場景參照")]
        // 根部錨點不受重力影響；第一段連桿會接在這個剛體上。
        [SerializeField] private Rigidbody2D rootAnchor;
        // 兩段會實際參與模擬的動態剛體。
        [SerializeField] private Rigidbody2D firstLink;
        [SerializeField] private Rigidbody2D secondLink;
        // 每個 HingeJoint2D 必須掛在對應的連桿剛體上。
        [SerializeField] private HingeJoint2D firstJoint;
        [SerializeField] private HingeJoint2D secondJoint;
        // 連桿本體的碰撞範圍，同時用於依密度計算質量。
        [SerializeField] private BoxCollider2D firstLinkCollider;
        [SerializeField] private BoxCollider2D secondLinkCollider;

        [Header("選用的場景參照")]
        [Tooltip("指定後，根部錨點會放在距離此 Transform 為 boomLength 的位置。")]
        [SerializeField] private Transform boomPivot;
        [Tooltip("第一段連桿自由端的子物件碰撞器。")]
        [SerializeField] private Collider2D firstJointMassCollider;
        [Tooltip("第二段連桿自由端的子物件碰撞器。")]
        [SerializeField] private Collider2D endMassCollider;

        [Header("初始幾何設定")]
        // 吊臂角度以世界座標的 +X 軸為 0 度，逆時針為正方向。
        [SerializeField, Min(MinimumDimension)] private float boomLength = 4.15f;
        [SerializeField] private float minimumBoomAngle = 20f;
        [SerializeField] private float maximumBoomAngle = 80f;
        [SerializeField] private float boomAngle = 57f;
        [SerializeField, Min(MinimumDimension)] private float firstLinkLength = 2.75f;
        [SerializeField, Min(MinimumDimension)] private float secondLinkLength = 2.9f;
        // 這兩個角度只決定釋放前的初始姿勢，不會在模擬中持續限制角度。
        [SerializeField] private float firstLinkWorldAngle = -13f;
        [SerializeField] private float secondLinkWorldAngle = 47f;
        [SerializeField, Min(MinimumDimension)] private float firstLinkWidth = 0.25f;
        [SerializeField, Min(MinimumDimension)] private float secondLinkWidth = 0.22f;

        [Header("依碰撞器密度計算質量")]
        // Rigidbody2D.useAutoMass 開啟後，Unity 會依所有所屬碰撞器的
        // 面積與 density 自動計算剛體質量及質心。
        [SerializeField, Min(0f)] private float firstLinkDensity = 0.22f;
        [SerializeField, Min(0f)] private float secondLinkDensity = 0.16f;
        [SerializeField, Min(0f)] private float firstJointMassDensity = 8.5f;
        [SerializeField, Min(0f)] private float endMassDensity = 11.5f;

        [Header("物理材質（選用）")]
        [SerializeField] private PhysicsMaterial2D linkMaterial;
        [SerializeField] private PhysicsMaterial2D massMaterial;

        [Header("剛體模擬設定")]
        [SerializeField] private bool startSimulatingOnAwake = true;
        [SerializeField] private float gravityScale = 1f;
        [SerializeField, Min(0f)] private float linearDamping = 0.005f;
        [SerializeField, Min(0f)] private float angularDamping = 0.012f;
        [SerializeField] private CollisionDetectionMode2D collisionDetection =
            CollisionDetectionMode2D.Continuous;
        [SerializeField] private RigidbodyInterpolation2D interpolation =
            RigidbodyInterpolation2D.Interpolate;

        private bool configurationApplied;

        public float BoomAngle => boomAngle;
        public float FirstLinkLength => firstLinkLength;
        public float SecondLinkLength => secondLinkLength;
        public bool IsSimulating { get; private set; }

        private void Awake()
        {
            // 缺少必要元件時停用腳本，避免模擬開始後連續產生 NullReference。
            if (!ValidateRequiredReferences(true))
            {
                enabled = false;
                return;
            }

            ApplyConfiguration();
            // 關閉此選項時，外部 UI 或其他腳本可自行決定釋放時機。
            if (startSimulatingOnAwake)
            {
                StartSimulation();
            }
        }

        /// <summary>
        /// 設定所有可調整的幾何參數。連桿長度沒有上限。
        /// 在模擬中呼叫時，物理結構會先回到設定姿勢。
        /// </summary>
        public void SetConfiguration(
            float newBoomAngle,
            float newFirstLinkLength,
            float newSecondLinkLength,
            float newFirstLinkWorldAngle,
            float newSecondLinkWorldAngle)
        {
            boomAngle = Mathf.Clamp(newBoomAngle, minimumBoomAngle, maximumBoomAngle);
            firstLinkLength = Mathf.Max(MinimumDimension, newFirstLinkLength);
            secondLinkLength = Mathf.Max(MinimumDimension, newSecondLinkLength);
            firstLinkWorldAngle = newFirstLinkWorldAngle;
            secondLinkWorldAngle = newSecondLinkWorldAngle;
            ApplyConfiguration();
        }

        public void SetBoomAngle(float angle)
        {
            // 第一支固定吊臂只允許在指定角度範圍內移動。
            boomAngle = Mathf.Clamp(angle, minimumBoomAngle, maximumBoomAngle);
            ApplyConfiguration();
        }

        /// <summary>
        /// 設定兩段連桿長度。只有防止零尺寸的下限，沒有最大長度限制。
        /// </summary>
        public void SetLinkLengths(float firstLength, float secondLength)
        {
            firstLinkLength = Mathf.Max(MinimumDimension, firstLength);
            secondLinkLength = Mathf.Max(MinimumDimension, secondLength);
            ApplyConfiguration();
        }

        /// <summary>
        /// 改變末端質量碰撞器的密度，並立即要求 Unity 重新計算質量。
        /// </summary>
        public void SetEndMassDensity(float density)
        {
            endMassDensity = Mathf.Max(0f, density);
            if (endMassCollider != null)
            {
                endMassCollider.density = endMassDensity;
                RefreshAutomaticMass(secondLink);
            }
        }

        /// <summary>
        /// 套用尺寸、質量、關節錨點與指定的初始姿勢。
        /// 呼叫 StartSimulation 前，兩段剛體會維持 Kinematic。
        /// </summary>
        [ContextMenu("套用物理設定")]
        public void ApplyConfiguration()
        {
            ClampConfiguration();
            if (!ValidateRequiredReferences(Application.isPlaying))
            {
                configurationApplied = false;
                return;
            }

            // 先停止剛體再改變長度與位置，避免保留上一輪模擬的速度。
            StopBody(firstLink);
            StopBody(secondLink);

            // 設定順序：固定點 → 剛體參數 → 碰撞器 → 關節 → 初始姿勢。
            ConfigureRootAnchor();
            ConfigureBodies();
            ConfigureColliders();
            ConfigureJoints();
            ApplyInitialPose();
            RefreshAutomaticMass(firstLink);
            RefreshAutomaticMass(secondLink);

            configurationApplied = true;
            IsSimulating = false;
        }

        /// <summary>
        /// 釋放兩段連桿。從此刻起，其運動與碰撞反應完全由
        /// Unity Physics 2D 控制。
        /// </summary>
        [ContextMenu("開始物理模擬")]
        public void StartSimulation()
        {
            if (!configurationApplied)
            {
                ApplyConfiguration();
            }

            if (!configurationApplied)
            {
                return;
            }

            firstLink.bodyType = RigidbodyType2D.Dynamic;
            secondLink.bodyType = RigidbodyType2D.Dynamic;
            firstLink.WakeUp();
            secondLink.WakeUp();
            IsSimulating = true;
        }

        /// <summary>
        /// 停止兩段剛體，並恢復指定的幾何設定與初始姿勢。
        /// </summary>
        [ContextMenu("重置物理模擬")]
        public void ResetSimulation()
        {
            ApplyConfiguration();
        }

        private void ConfigureRootAnchor()
        {
            // 根部只提供關節固定點，不參與重力與碰撞動力反應。
            rootAnchor.bodyType = RigidbodyType2D.Kinematic;
            rootAnchor.gravityScale = 0f;
            rootAnchor.linearVelocity = Vector2.zero;
            rootAnchor.angularVelocity = 0f;

            if (boomPivot != null)
            {
                // 由吊臂基點、長度與角度計算第一個鉸鏈的世界座標。
                rootAnchor.position = (Vector2)boomPivot.position +
                    DirectionFromAngle(boomAngle) * boomLength;
            }
        }

        private void ConfigureBodies()
        {
            ConfigureBody(firstLink);
            ConfigureBody(secondLink);
        }

        private void ConfigureBody(Rigidbody2D body)
        {
            // 質量由碰撞器尺寸及密度決定，不在程式內寫死 mass。
            body.useAutoMass = true;
            body.gravityScale = gravityScale;
            body.linearDamping = linearDamping;
            body.angularDamping = angularDamping;
            body.collisionDetectionMode = collisionDetection;
            body.interpolation = interpolation;
        }

        private void ConfigureColliders()
        {
            // 連桿剛體位於兩端點的中間，因此碰撞器以原點為中心。
            firstLinkCollider.size = new Vector2(firstLinkLength, firstLinkWidth);
            firstLinkCollider.offset = Vector2.zero;
            firstLinkCollider.density = firstLinkDensity;

            secondLinkCollider.size = new Vector2(secondLinkLength, secondLinkWidth);
            secondLinkCollider.offset = Vector2.zero;
            secondLinkCollider.density = secondLinkDensity;

            if (linkMaterial != null)
            {
                firstLinkCollider.sharedMaterial = linkMaterial;
                secondLinkCollider.sharedMaterial = linkMaterial;
            }

            ConfigureMassCollider(
                firstJointMassCollider,
                firstLink,
                firstLinkLength * 0.5f,
                firstJointMassDensity);
            ConfigureMassCollider(
                endMassCollider,
                secondLink,
                secondLinkLength * 0.5f,
                endMassDensity);
        }

        private void ConfigureMassCollider(
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
                Debug.LogWarning(
                    massCollider.name +
                    " 必須屬於 " + owner.name +
                    "，而且不可擁有獨立的 Rigidbody2D。",
                    massCollider);
                return;
            }

            if (massCollider.transform == owner.transform)
            {
                // 碰撞器與剛體在同一物件時，用 offset 放到連桿末端。
                massCollider.offset = new Vector2(localX, 0f);
            }
            else
            {
                // 子物件碰撞器會自動成為父剛體的複合碰撞器。
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

        private void ConfigureJoints()
        {
            // 第一段左端接固定錨點。
            firstJoint.connectedBody = rootAnchor;
            firstJoint.autoConfigureConnectedAnchor = false;
            firstJoint.anchor = new Vector2(-firstLinkLength * 0.5f, 0f);
            firstJoint.connectedAnchor = Vector2.zero;
            firstJoint.enableCollision = false;

            // 第二段左端接第一段右端，形成雙擺結構。
            secondJoint.connectedBody = firstLink;
            secondJoint.autoConfigureConnectedAnchor = false;
            secondJoint.anchor = new Vector2(-secondLinkLength * 0.5f, 0f);
            secondJoint.connectedAnchor = new Vector2(firstLinkLength * 0.5f, 0f);
            secondJoint.enableCollision = false;
        }

        private void ApplyInitialPose()
        {
            // 依「起點 + 方向向量 × 長度」依序求出三個支點。
            Vector2 rootPoint = rootAnchor.position;
            Vector2 firstEnd = rootPoint +
                DirectionFromAngle(firstLinkWorldAngle) * firstLinkLength;
            Vector2 secondEnd = firstEnd +
                DirectionFromAngle(secondLinkWorldAngle) * secondLinkLength;

            SetBodyBetween(firstLink, rootPoint, firstEnd);
            SetBodyBetween(secondLink, firstEnd, secondEnd);
            Physics2D.SyncTransforms();
        }

        private static void StopBody(Rigidbody2D body)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private static void RefreshAutomaticMass(Rigidbody2D body)
        {
            // 重新切換 Auto Mass，要求 Unity 立即依最新 Collider 重算質量。
            body.useAutoMass = false;
            body.useAutoMass = true;
        }

        private static void SetBodyBetween(Rigidbody2D body, Vector2 start, Vector2 end)
        {
            // 連桿中心位於兩端點中間，旋轉角度則由兩點差向量取得。
            Vector2 delta = end - start;
            body.position = (start + end) * 0.5f;
            body.rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        }

        private static Vector2 DirectionFromAngle(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private bool ValidateRequiredReferences(bool logError)
        {
            // 除了檢查空參照，也確認 Joint 與 Collider 確實屬於指定剛體。
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
                    "CranePhysicsRig 的參照缺失，或元件掛在錯誤的剛體上。" +
                    "請在 Inspector 指定根部錨點、兩段連桿剛體、各自的 HingeJoint2D，" +
                    "以及各自的 BoxCollider2D。",
                    this);
            }

            return valid;
        }

        private void ClampConfiguration()
        {
            boomLength = Mathf.Max(MinimumDimension, boomLength);
            firstLinkLength = Mathf.Max(MinimumDimension, firstLinkLength);
            secondLinkLength = Mathf.Max(MinimumDimension, secondLinkLength);
            firstLinkWidth = Mathf.Max(MinimumDimension, firstLinkWidth);
            secondLinkWidth = Mathf.Max(MinimumDimension, secondLinkWidth);

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

        private void OnValidate()
        {
            ClampConfiguration();
        }
    }
}
