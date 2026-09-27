using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MidnightCrane
{
    // 準備中に調整点をつかむための処理。
    [RequireComponent(typeof(CircleCollider2D))]
    [DisallowMultipleComponent]
    public sealed class CraneSetupDragHandle : MonoBehaviour
    {
        [SerializeField] private CranePhysicsRig rig;
        [SerializeField] private CraneSetupPoint point;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Collider2D hitArea;

        private static CraneSetupDragHandle activeHandle;
        private Collider2D[] hitAreas;
        private bool isDragging;

        private void Awake()
        {
            hitAreas = GetComponents<Collider2D>();

            if (hitArea == null)
            {
                hitArea = hitAreas.Length > 0 ? hitAreas[0] : null;
            }

            // これはつかむためだけ。当たり判定には混ぜない。
            for (int i = 0; i < hitAreas.Length; i++)
            {
                hitAreas[i].isTrigger = true;
            }

            if (sceneCamera == null)
            {
                sceneCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (rig == null || sceneCamera == null || hitArea == null || hitAreas.Length == 0)
            {
                return;
            }

            bool canDrag = rig.CanEditSetup;
            SetHitAreasEnabled(canDrag);

            if (!canDrag)
            {
                ReleaseHandle();
                return;
            }

            if (!TryReadPointer(
                    out Vector2 screenPoint,
                    out bool pressedThisFrame,
                    out bool isPressed,
                    out bool releasedThisFrame,
                    out int pointerId))
            {
                return;
            }

            Vector2 pointer = GetWorldPointer(screenPoint);

            if (activeHandle == null &&
                pressedThisFrame &&
                !PointerIsOverUi(pointerId) &&
                PointerHitsHandle(pointer))
            {
                activeHandle = this;
                isDragging = true;
            }

            if (activeHandle != this)
            {
                return;
            }

            if (isPressed)
            {
                rig.MoveSetupPoint(point, pointer);
            }

            if (releasedThisFrame)
            {
                ReleaseHandle();
            }
        }

        private void LateUpdate()
        {
            if (rig == null || !rig.CanEditSetup || isDragging)
            {
                return;
            }

            Vector3 position = transform.position;
            Vector2 setupPoint = rig.GetSetupPoint(point);
            transform.position = new Vector3(setupPoint.x, setupPoint.y, position.z);
        }

        private bool TryReadPointer(
            out Vector2 screenPoint,
            out bool pressedThisFrame,
            out bool isPressed,
            out bool releasedThisFrame,
            out int pointerId)
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touch = touchscreen.primaryTouch;
                bool touchIsActive = touch.press.isPressed ||
                    touch.press.wasPressedThisFrame ||
                    touch.press.wasReleasedThisFrame;

                if (touchIsActive)
                {
                    screenPoint = touch.position.ReadValue();
                    pressedThisFrame = touch.press.wasPressedThisFrame;
                    isPressed = touch.press.isPressed;
                    releasedThisFrame = touch.press.wasReleasedThisFrame;
                    pointerId = touch.touchId.ReadValue();
                    return true;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                screenPoint = mouse.position.ReadValue();
                pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
                isPressed = mouse.leftButton.isPressed;
                releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
                pointerId = -1;
                return true;
            }

            screenPoint = Vector2.zero;
            pressedThisFrame = false;
            isPressed = false;
            releasedThisFrame = false;
            pointerId = -1;
            return false;
        }

        private Vector2 GetWorldPointer(Vector2 screenPoint)
        {
            float distance = Mathf.Abs(transform.position.z - sceneCamera.transform.position.z);
            Vector3 worldPoint = sceneCamera.ScreenToWorldPoint(
                new Vector3(screenPoint.x, screenPoint.y, distance));
            return worldPoint;
        }

        private bool PointerHitsHandle(Vector2 pointer)
        {
            for (int i = 0; i < hitAreas.Length; i++)
            {
                if (hitAreas[i].enabled && hitAreas[i].OverlapPoint(pointer))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PointerIsOverUi(int pointerId)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return pointerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
        }

        private void SetHitAreasEnabled(bool value)
        {
            if (hitAreas == null)
            {
                return;
            }

            for (int i = 0; i < hitAreas.Length; i++)
            {
                hitAreas[i].enabled = value;
            }
        }

        private void ReleaseHandle()
        {
            isDragging = false;
            if (activeHandle == this)
            {
                activeHandle = null;
            }
        }

        private void OnDisable()
        {
            ReleaseHandle();
            SetHitAreasEnabled(false);
        }
    }

    // どの点を動かしているか。BoomEnd は矢印用に残してある。
    public enum CraneSetupPoint
    {
        BoomEnd,
        FirstJoint,
        EndMass
    }
}
