using UnityEngine;

namespace MidnightCrane
{
    // 鐘側の動きだけをまとめた。
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HingeJoint2D))]
    [DisallowMultipleComponent]
    public sealed class CraneBellPendulum : MonoBehaviour
    {
        [Header("物理物体")]
        [SerializeField] private Rigidbody2D bellBody;
        [SerializeField] private HingeJoint2D bellJoint;
        [SerializeField] private Rigidbody2D anchorBody;

        [Header("支点")]
        [SerializeField] private Vector2 localHingePoint = new Vector2(0f, 0.6f);

        [Header("物理設定")]
        [SerializeField, Min(0.001f)] private float mass = 4f;
        [SerializeField] private float gravityScale = 1f;
        [SerializeField, Min(0f)] private float linearDamping = 0.02f;
        [SerializeField, Min(0f)] private float angularDamping = 0.08f;

        private Vector2 initialPosition;
        private float initialRotation;
        private Vector2 worldHingePoint;
        private Vector2 pausedVelocity;
        private float pausedAngularVelocity;
        private bool initialized;
        private bool paused;

        private void Awake()
        {
            FindParts();
            CaptureInitialPose();
            ResetBell();
        }

        // 今の置き方を基準にする。
        [ContextMenu("現在の姿勢を初期姿勢にする")]
        public void CaptureInitialPose()
        {
            FindParts();
            if (bellBody == null || bellJoint == null)
            {
                return;
            }

            initialPosition = bellBody.position;
            initialRotation = bellBody.rotation;
            worldHingePoint = bellBody.transform.TransformPoint(localHingePoint);
            ConfigureParts();
            initialized = true;
        }

        // 鐘を最初の場所へ戻して待機。
        public void ResetBell()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            bellBody.bodyType = RigidbodyType2D.Kinematic;
            bellBody.linearVelocity = Vector2.zero;
            bellBody.angularVelocity = 0f;
            bellBody.position = initialPosition;
            bellBody.rotation = initialRotation;
            bellJoint.enabled = true;
            paused = false;
            Physics2D.SyncTransforms();
        }

        // ここから鐘を自由にする。
        public void StartBell()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            bellBody.bodyType = RigidbodyType2D.Dynamic;
            bellBody.WakeUp();
            paused = false;
        }

        // いまの勢いを覚えて止める。
        public void PauseBell()
        {
            if (!EnsureInitialized() || bellBody.bodyType != RigidbodyType2D.Dynamic)
            {
                return;
            }

            pausedVelocity = bellBody.linearVelocity;
            pausedAngularVelocity = bellBody.angularVelocity;
            FreezeBell();
            paused = true;
        }

        // 覚えていた勢いから再開。
        public void ResumeBell()
        {
            if (!EnsureInitialized() || !paused)
            {
                return;
            }

            bellBody.bodyType = RigidbodyType2D.Dynamic;
            bellBody.linearVelocity = pausedVelocity;
            bellBody.angularVelocity = pausedAngularVelocity;
            bellBody.WakeUp();
            paused = false;
        }

        // 終了時はその場で固定。
        public void StopBell()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            FreezeBell();
            paused = false;
        }

        private void FindParts()
        {
            if (bellBody == null)
            {
                bellBody = GetComponent<Rigidbody2D>();
            }

            if (bellJoint == null)
            {
                bellJoint = GetComponent<HingeJoint2D>();
            }
        }

        private bool EnsureInitialized()
        {
            if (!initialized)
            {
                CaptureInitialPose();
            }

            return initialized;
        }

        private void ConfigureParts()
        {
            bellBody.useAutoMass = false;
            bellBody.mass = Mathf.Max(0.001f, mass);
            bellBody.gravityScale = gravityScale;
            bellBody.linearDamping = Mathf.Max(0f, linearDamping);
            bellBody.angularDamping = Mathf.Max(0f, angularDamping);
            bellBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            bellBody.interpolation = RigidbodyInterpolation2D.None;

            bellJoint.connectedBody = anchorBody;
            bellJoint.autoConfigureConnectedAnchor = false;
            bellJoint.anchor = localHingePoint;
            bellJoint.connectedAnchor = anchorBody == null
                ? worldHingePoint
                : (Vector2)anchorBody.transform.InverseTransformPoint(worldHingePoint);
            bellJoint.enableCollision = false;
        }

        private void FreezeBell()
        {
            bellBody.bodyType = RigidbodyType2D.Kinematic;
            bellBody.linearVelocity = Vector2.zero;
            bellBody.angularVelocity = 0f;
        }

        private void OnValidate()
        {
            mass = Mathf.Max(0.001f, mass);
            linearDamping = Mathf.Max(0f, linearDamping);
            angularDamping = Mathf.Max(0f, angularDamping);
        }
    }
}
