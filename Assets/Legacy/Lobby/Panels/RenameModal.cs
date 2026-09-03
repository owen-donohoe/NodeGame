using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NodeWar.Lobby
{
    public class RenameModal : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI charCountText;
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private System.Action<string> onComplete;
        private const int MAX_LENGTH = 16;

        private void Awake()
        {
            if (modalRoot != null)
                modalRoot.SetActive(false);

            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirm);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancel);
            if (inputField != null)
                inputField.onValueChanged.AddListener(OnInputChanged);
        }

        public void Open(System.Action<string> completionCallback)
        {
            onComplete = completionCallback;

            PlayerProfile profile = PlayerProfile.Instance;
            if (profile != null)
                inputField.text = profile.Username;

            if (errorText != null)
                errorText.text = "";

            UpdateCharCount();
            confirmButton.interactable = true;
            modalRoot.SetActive(true);
            inputField.Select();
            inputField.ActivateInputField();
        }

        public void Close()
        {
            modalRoot.SetActive(false);
            // Do NOT null onComplete here — caller handles it
        }

        private void OnInputChanged(string value)
        {
            if (value.Length > MAX_LENGTH)
            {
                inputField.text = value.Substring(0, MAX_LENGTH);
                return;
            }

            UpdateCharCount();
            ValidateAndShowError(value);
        }

        private void UpdateCharCount()
        {
            if (charCountText != null)
                charCountText.text = inputField.text.Length + "/" + MAX_LENGTH;
        }

        private bool ValidateAndShowError(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                SetError("Name cannot be empty");
                return false;
            }

            if (!PlayerProfile.ValidateUsername(value))
            {
                SetError("Letters, numbers, spaces, _ only");
                return false;
            }

            SetError("");
            return true;
        }

        private void SetError(string msg)
        {
            if (errorText != null)
                errorText.text = msg;

            if (confirmButton != null)
                confirmButton.interactable = string.IsNullOrEmpty(msg);
        }

        private void OnConfirm()
        {
            string value = inputField.text.Trim();
            if (!PlayerProfile.ValidateUsername(value)) return;

            System.Action<string> callback = onComplete;
            onComplete = null;
            Close();
            callback?.Invoke(value);
        }

        private void OnCancel()
        {
            onComplete = null;
            Close();
        }

        private void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
            if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();
            if (inputField != null) inputField.onValueChanged.RemoveAllListeners();
        }
    }
}