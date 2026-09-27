using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MidnightCrane
{
    // 画面に出す数字まわり。
    [DisallowMultipleComponent]
    public sealed class CraneGameUI : MonoBehaviour
    {
        [Header("UI 文字")]
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text hitCountText;
        [SerializeField] private TMP_Text desiresText;
        [SerializeField] private TMP_Text resultText;

        [Header("日本語フォント")]
        [SerializeField] private Font japaneseSourceFont;

        [Header("表示形式")]
        [SerializeField] private string timeFormat = "年明けまであと {0} 秒";
        [SerializeField] private string hitCountFormat = "HITS {0}";
        [SerializeField] private string desiresFormat = "残り煩悩 {0}";
        [SerializeField] private bool showHitCount;
        [SerializeField, Min(0f)] private float desiresCountDuration = 0.35f;

        private Coroutine desiresCountRoutine;
        private TMP_FontAsset runtimeJapaneseFont;
        private int displayedDesires;
        private bool hasDisplayedDesires;

        public TMP_FontAsset RuntimeJapaneseFont => runtimeJapaneseFont;

        private void Awake()
        {
            CreateJapaneseFont();

            if (hitCountText != null)
            {
                hitCountText.gameObject.SetActive(showHitCount);
            }
        }

        // 元の TTF から実行中だけ使う Font Asset を作る。
        private void CreateJapaneseFont()
        {
            if (japaneseSourceFont == null)
            {
                return;
            }

            runtimeJapaneseFont = TMP_FontAsset.CreateFontAsset(
                japaneseSourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            runtimeJapaneseFont.name = japaneseSourceFont.name + " Runtime TMP";

            SetFont(timeText);
            SetFont(hitCountText);
            SetFont(desiresText);
            SetFont(resultText);
        }

        private void SetFont(TMP_Text target)
        {
            if (target != null && runtimeJapaneseFont != null)
            {
                target.font = runtimeJapaneseFont;
            }
        }

        // 残り時間は端数を切り上げ。
        public void SetTime(float seconds)
        {
            if (timeText != null)
            {
                int displaySeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
                timeText.text = string.Format(timeFormat, displaySeconds);
            }
        }

        // HITS を出す時だけ更新。
        public void SetHitCount(int hitCount)
        {
            if (showHitCount && hitCountText != null)
            {
                hitCountText.text = string.Format(hitCountFormat, hitCount);
            }
        }

        // 残り煩悩は少し遅れて追いかける。
        public void SetDesires(int desires)
        {
            if (desiresText == null)
            {
                return;
            }

            int newValue = Mathf.Max(0, desires);
            if (!hasDisplayedDesires ||
                newValue >= displayedDesires ||
                desiresCountDuration <= 0f ||
                !isActiveAndEnabled)
            {
                StopDesiresCount();
                displayedDesires = newValue;
                hasDisplayedDesires = true;
                UpdateDesiresText();
                return;
            }

            StopDesiresCount();
            desiresCountRoutine = StartCoroutine(CountDesiresDown(newValue));
        }

        private IEnumerator CountDesiresDown(int targetValue)
        {
            int startValue = displayedDesires;
            float elapsed = 0f;

            while (elapsed < desiresCountDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / desiresCountDuration);
                displayedDesires = Mathf.RoundToInt(
                    Mathf.Lerp(startValue, targetValue, progress));
                UpdateDesiresText();
                yield return null;
            }

            displayedDesires = targetValue;
            UpdateDesiresText();
            desiresCountRoutine = null;
        }

        private void StopDesiresCount()
        {
            if (desiresCountRoutine != null)
            {
                StopCoroutine(desiresCountRoutine);
                desiresCountRoutine = null;
            }
        }

        private void UpdateDesiresText()
        {
            desiresText.text = string.Format(desiresFormat, displayedDesires);
        }

        // 終了メッセージ。
        public void ShowResult(CraneGameResult result)
        {
            if (resultText == null)
            {
                return;
            }

            resultText.gameObject.SetActive(true);
            resultText.text = result == CraneGameResult.DesiresCleared
                ? "百八煩悩を払いました"
                : "時間切れ";
        }

        // 次の開始まで隠す。
        public void HideResult()
        {
            if (resultText != null)
            {
                resultText.gameObject.SetActive(false);
            }
        }

        private void OnValidate()
        {
            desiresCountDuration = Mathf.Max(0f, desiresCountDuration);
        }

        private void OnDestroy()
        {
            if (runtimeJapaneseFont != null)
            {
                Destroy(runtimeJapaneseFont);
            }
        }
    }
}
