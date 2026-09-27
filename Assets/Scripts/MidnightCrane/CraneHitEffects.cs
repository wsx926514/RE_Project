using System.Collections;
using TMPro;
using UnityEngine;

namespace MidnightCrane
{
    // ヒット数字とカメラ揺れ。
    [DisallowMultipleComponent]
    public sealed class CraneHitEffects : MonoBehaviour
    {
        [Header("浮動文字")]
        [SerializeField] private RectTransform floatingTextRoot;
        [SerializeField] private TMP_Text floatingTextTemplate;
        [SerializeField] private CraneGameUI gameUI;
        [SerializeField] private string floatingTextFormat = "{0}煩悩";
        [SerializeField, Min(0.1f)] private float floatingDuration = 1.3f;
        [SerializeField, Min(0f)] private float floatingDistance = 90f;

        [Header("画面揺れ")]
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(0f)] private float minimumShakeImpact = 1f;
        [SerializeField, Min(0.01f)] private float maximumShakeImpact = 12f;
        [SerializeField, Min(0f)] private float minimumShakeDistance = 0.015f;
        [SerializeField, Min(0f)] private float maximumShakeDistance = 0.18f;
        [SerializeField, Min(0f)] private float minimumShakeDuration = 0.08f;
        [SerializeField, Min(0f)] private float maximumShakeDuration = 0.28f;

        private Coroutine shakeRoutine;
        private Vector3 cameraStartPosition;
        private float shakeTime;
        private float shakeDuration;
        private float shakeDistance;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (floatingTextRoot == null && floatingTextTemplate != null)
            {
                floatingTextRoot = floatingTextTemplate.transform.parent as RectTransform;
            }

            if (gameUI == null)
            {
                gameUI = GetComponent<CraneGameUI>();
            }

            if (floatingTextTemplate != null)
            {
                floatingTextTemplate.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            // UI 側で作った日本語フォントをそのまま借りる。
            if (floatingTextTemplate != null &&
                gameUI != null &&
                gameUI.RuntimeJapaneseFont != null)
            {
                floatingTextTemplate.font = gameUI.RuntimeJapaneseFont;
            }
        }

        // 一発分の見た目をまとめて出す。
        public void PlayHit(Vector2 worldPoint, float impactSpeed, int clearedDesires)
        {
            ShowFloatingText(worldPoint, clearedDesires);
            StartShake(impactSpeed);
        }

        private void ShowFloatingText(Vector2 worldPoint, int clearedDesires)
        {
            if (floatingTextRoot == null ||
                floatingTextTemplate == null ||
                targetCamera == null)
            {
                return;
            }

            TMP_Text newText = Instantiate(floatingTextTemplate, floatingTextRoot);
            newText.gameObject.SetActive(true);
            newText.text = string.Format(floatingTextFormat, clearedDesires);

            Camera uiCamera = null;
            Canvas canvas = floatingTextRoot.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = canvas.worldCamera != null ? canvas.worldCamera : targetCamera;
            }

            Vector2 screenPoint = targetCamera.WorldToScreenPoint(worldPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                floatingTextRoot,
                screenPoint,
                uiCamera,
                out Vector2 localPoint);

            RectTransform textRect = newText.rectTransform;
            textRect.anchoredPosition = localPoint;
            StartCoroutine(FloatAndFade(newText, textRect, localPoint));
        }

        private IEnumerator FloatAndFade(
            TMP_Text text,
            RectTransform textRect,
            Vector2 startPosition)
        {
            Color startColor = text.color;
            float elapsed = 0f;

            while (elapsed < floatingDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / floatingDuration);
                float moveProgress = 1f - (1f - progress) * (1f - progress);

                textRect.anchoredPosition = startPosition +
                    Vector2.up * floatingDistance * moveProgress;
                text.color = new Color(
                    startColor.r,
                    startColor.g,
                    startColor.b,
                    1f - progress);
                yield return null;
            }

            Destroy(text.gameObject);
        }

        private void StartShake(float impactSpeed)
        {
            if (targetCamera == null || impactSpeed < minimumShakeImpact)
            {
                return;
            }

            float strength = Mathf.InverseLerp(
                minimumShakeImpact,
                maximumShakeImpact,
                impactSpeed);
            float newDuration = Mathf.Lerp(
                minimumShakeDuration,
                maximumShakeDuration,
                strength);
            float newDistance = Mathf.Lerp(
                minimumShakeDistance,
                maximumShakeDistance,
                strength);

            shakeTime = Mathf.Max(shakeTime, newDuration);
            shakeDuration = Mathf.Max(shakeDuration, newDuration);
            shakeDistance = Mathf.Max(shakeDistance, newDistance);

            if (shakeRoutine == null)
            {
                cameraStartPosition = targetCamera.transform.localPosition;
                shakeRoutine = StartCoroutine(ShakeCamera());
            }
        }

        private IEnumerator ShakeCamera()
        {
            while (shakeTime > 0f)
            {
                float fade = shakeDuration > 0f
                    ? Mathf.Clamp01(shakeTime / shakeDuration)
                    : 0f;
                Vector2 randomOffset = Random.insideUnitCircle * shakeDistance * fade;

                targetCamera.transform.localPosition = cameraStartPosition +
                    new Vector3(randomOffset.x, randomOffset.y, 0f);
                shakeTime -= Time.unscaledDeltaTime;
                yield return null;
            }

            targetCamera.transform.localPosition = cameraStartPosition;
            shakeTime = 0f;
            shakeDuration = 0f;
            shakeDistance = 0f;
            shakeRoutine = null;
        }

        private void OnDisable()
        {
            bool wasShaking = shakeRoutine != null;
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
            }

            if (wasShaking && targetCamera != null)
            {
                targetCamera.transform.localPosition = cameraStartPosition;
            }

            shakeTime = 0f;
            shakeDuration = 0f;
            shakeDistance = 0f;
            shakeRoutine = null;
        }

        private void OnValidate()
        {
            floatingDuration = Mathf.Max(0.1f, floatingDuration);
            floatingDistance = Mathf.Max(0f, floatingDistance);
            minimumShakeImpact = Mathf.Max(0f, minimumShakeImpact);
            maximumShakeImpact = Mathf.Max(minimumShakeImpact + 0.01f, maximumShakeImpact);
            minimumShakeDistance = Mathf.Max(0f, minimumShakeDistance);
            maximumShakeDistance = Mathf.Max(minimumShakeDistance, maximumShakeDistance);
            minimumShakeDuration = Mathf.Max(0f, minimumShakeDuration);
            maximumShakeDuration = Mathf.Max(minimumShakeDuration, maximumShakeDuration);
        }
    }
}
