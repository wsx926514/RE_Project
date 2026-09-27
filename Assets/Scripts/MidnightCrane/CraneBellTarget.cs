using UnityEngine;

namespace MidnightCrane
{
    // 槌が鐘を通った瞬間だけゲーム側へ渡す。
    [DisallowMultipleComponent]
    public sealed class CraneBellTarget : MonoBehaviour
    {
        [SerializeField] private CraneGameFlow gameFlow;
        [SerializeField] private CranePhysicsRig rig;
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 0.5f;
        [SerializeField, Min(0f)] private float hitCooldown = 0.2f;

        private Collider2D bellCollider;
        private Rigidbody2D bellBody;
        private float nextHitTime;

        private void Awake()
        {
            bellCollider = GetComponent<Collider2D>();
            if (bellCollider == null)
            {
                Debug.LogError("鐘に Collider2D を追加してください。", this);
                enabled = false;
                return;
            }

            // 鐘側は Trigger。槌の勢いはここで殺さない。
            bellCollider.isTrigger = true;
            bellBody = bellCollider.attachedRigidbody;
        }

        private void OnTriggerEnter2D(Collider2D otherCollider)
        {
            if (gameFlow == null || rig == null || gameFlow.State != CraneGameState.Running)
            {
                return;
            }

            bool isHammer = rig.EndMassCollider != null
                ? otherCollider == rig.EndMassCollider
                : otherCollider.attachedRigidbody == rig.EndMassBody;

            if (!isHammer)
            {
                return;
            }

            Vector2 hammerCenter = otherCollider.bounds.center;
            Vector2 hitPoint = bellCollider.ClosestPoint(hammerCenter);

            // 先端の回転ぶんまで入れた速度を使う。
            Vector2 hammerVelocity = rig.GetEndMassVelocityAt(hitPoint);
            Vector2 bellVelocity = bellBody != null
                ? bellBody.GetPointVelocity(hitPoint)
                : Vector2.zero;
            float impactSpeed = (hammerVelocity - bellVelocity).magnitude;

            if (impactSpeed < minimumImpactSpeed)
            {
                return;
            }

            if (Time.time < nextHitTime)
            {
                return;
            }

            nextHitTime = Time.time + hitCooldown;
            gameFlow.RegisterBellHit(impactSpeed, hitPoint);
        }

        private void OnValidate()
        {
            minimumImpactSpeed = Mathf.Max(0f, minimumImpactSpeed);
            hitCooldown = Mathf.Max(0f, hitCooldown);

            Collider2D target = GetComponent<Collider2D>();
            if (target != null)
            {
                target.isTrigger = true;
            }
        }
    }
}
