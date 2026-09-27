using UnityEngine;
using UnityEngine.UI;

namespace MidnightCrane
{
    // START と END はこのボタン一つ。
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class CraneStartEndButton : MonoBehaviour
    {
        [Header("ゲーム設定")]
        [SerializeField] private CraneGameFlow gameFlow;

        [Header("ボタン画像")]
        [SerializeField] private Image buttonImage;
        [SerializeField] private Sprite startSprite;
        [SerializeField] private Sprite endSprite;

        private Button button;
        private CraneGameState lastState;

        private void Awake()
        {
            button = GetComponent<Button>();

            if (buttonImage == null)
            {
                buttonImage = GetComponent<Image>();
            }

            if (gameFlow == null)
            {
                Debug.LogError("CraneGameFlow を設定してください。", this);
                enabled = false;
                return;
            }

            button.onClick.AddListener(ToggleGame);
            lastState = gameFlow.State;
            UpdateButtonImage(lastState);
        }

        private void Update()
        {
            if (gameFlow == null || gameFlow.State == lastState)
            {
                return;
            }

            lastState = gameFlow.State;
            UpdateButtonImage(lastState);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(ToggleGame);
            }

        }

        // 待機なら開始、それ以外はやり直し。
        private void ToggleGame()
        {
            if (gameFlow.State == CraneGameState.Ready)
            {
                gameFlow.StartGame();
            }
            else
            {
                gameFlow.ResetGame();
            }
        }

        // 状態に合わせて絵だけ差し替える。
        private void UpdateButtonImage(CraneGameState state)
        {
            if (buttonImage == null)
            {
                return;
            }

            Sprite newSprite = state == CraneGameState.Ready ? startSprite : endSprite;
            if (newSprite != null)
            {
                buttonImage.sprite = newSprite;
            }
        }
    }
}
