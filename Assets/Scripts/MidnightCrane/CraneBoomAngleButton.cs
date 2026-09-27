using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MidnightCrane
{
    // 吊り腕の矢印ボタン。押している間だけ動かす。
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class CraneBoomAngleButton : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        [SerializeField] private CranePhysicsRig rig;
        [SerializeField] private bool increaseAngle = true;
        [SerializeField, Min(1f)] private float rotationSpeed = 35f;

        private Button button;
        private bool isPressed;

        private void Awake()
        {
            button = GetComponent<Button>();
            UpdateInteractable();
        }

        private void Update()
        {
            UpdateInteractable();

            if (!isPressed || rig == null || !rig.CanEditSetup)
            {
                return;
            }

            // 押しっぱなしでも速度は変えない。
            float direction = increaseAngle ? 1f : -1f;
            float angle = rig.BoomAngle +
                direction * rotationSpeed * Time.unscaledDeltaTime;
            rig.SetBoomAngle(angle);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = button != null && button.interactable;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPressed = false;
        }

        private void UpdateInteractable()
        {
            if (button != null)
            {
                button.interactable = rig != null && rig.CanEditSetup;
            }

            if (button == null || !button.interactable)
            {
                isPressed = false;
            }
        }

        private void OnDisable()
        {
            isPressed = false;
        }
    }
}
