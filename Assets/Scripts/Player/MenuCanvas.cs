#if CMPSETUP_COMPLETE
using AvocadoShark;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MenuCanvas : MonoBehaviour
{
    [SerializeField] private GameObject RoomCreationPanel;
    [SerializeField] private Toggle passwordToggle;
    [SerializeField] private Slider slider;
    [SerializeField] private Button refreshRooms;
    [SerializeField] private TextMeshProUGUI roomCountText, nameInputFieldLimitText, passwordInputFieldLimitText;
    public bool IsPasswordEnabled { private set; get; } = false;

    [SerializeField] private TMP_InputField nameInputField, passwordInputField;

    private void Awake()
    {
        slider.onValueChanged.AddListener(OnSliderValueChange);
        passwordInputField.interactable = IsPasswordEnabled;
        passwordToggle.isOn = IsPasswordEnabled;
        passwordToggle.onValueChanged.AddListener(TogglePassword);
        nameInputField.onValueChanged.AddListener(NameInputUpdate);
        passwordInputField.onValueChanged.AddListener(PasswordInputUpdate);
    }

    private void TogglePassword(bool value)
    {
        IsPasswordEnabled = value;
        passwordInputField.interactable = value;
    }

    public void RefreshRooms()
    {
        FusionConnection.Instance.RefreshRoomList();
    }

    public string GetRoomName()
    {
        return nameInputField.text;
    }

    public string GetPassword()
    {
        return passwordInputField.text;
    }

    public int GetMaxPlayers()
    {
        return (int)slider.value;
    }

    private void OnSliderValueChange(float value)
    {
        roomCountText.text = Mathf.RoundToInt(value).ToString();
    }

    public void PasswordInputUpdate(string text)
    {
        passwordInputFieldLimitText.text = text.Count() == passwordInputField.characterLimit
            ? $"<color=#D96222>{text.Count()}/{passwordInputField.characterLimit}</color>"
            : $"{text.Count()}/{passwordInputField.characterLimit}";
    }

    public void NameInputUpdate(string text)
    {
        nameInputFieldLimitText.text = text.Count() == nameInputField.characterLimit
            ? $"<color=#D96222>{text.Count()}/{nameInputField.characterLimit}</color>"
            : $"{text.Count()}/{nameInputField.characterLimit}";
    }


    /// <summary>
    /// Disconnect Web3 user and return to login scene
    /// </summary>
    public void DisconnectAndReturnToLogin()
    {
        Debug.Log("🔴 Back button pressed - disconnecting Web3 user");

        // Clear all Web3 authentication data
        Web3Integration.ClearWeb3Data();

        // Shutdown Fusion connection if active
        if (FusionConnection.Instance != null && FusionConnection.Instance.Runner != null)
        {
            FusionConnection.Instance.Runner.Shutdown();
        }

        Debug.Log("✅ Web3 disconnected, returning to login scene");

        // Return to login scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    // ADD this Web3Integration class at the bottom of MenuCanvas.cs (outside the MenuCanvas class):
    public static class Web3Integration
    {
        public static bool IsWeb3Authenticated()
        {
            return PlayerPrefs.GetString("Web3_AuthComplete", "false") == "true";
        }

        public static string GetWeb3Username()
        {
            return PlayerPrefs.GetString("Web3_Username", "");
        }

        public static string GetWeb3WalletAddress()
        {
            return PlayerPrefs.GetString("Web3_WalletAddress", "");
        }

        public static int GetWeb3NFTBalance()
        {
            return PlayerPrefs.GetInt("Web3_NFTBalance", 0);
        }

        public static bool HasWeb3NFTs()
        {
            return PlayerPrefs.GetInt("Web3_HasNFTs", 0) == 1;
        }

        public static void ClearWeb3Data()
        {
            PlayerPrefs.DeleteKey("Web3_Username");
            PlayerPrefs.DeleteKey("Web3_WalletAddress");
            PlayerPrefs.DeleteKey("Web3_NFTBalance");
            PlayerPrefs.DeleteKey("Web3_HasNFTs");
            PlayerPrefs.DeleteKey("Web3_AuthComplete");
            PlayerPrefs.Save();
            Debug.Log("🧹 Web3 data cleared from PlayerPrefs");
        }
    }
}
#endif