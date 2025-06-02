using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// NetworkManager - Main Orchestration Controller
/// Coordinates Web3Manager and PlayFabManager for complete authentication flow
/// Simplified and focused on scene flow and UI coordination
/// </summary>
public class NetworkManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int serverSceneIndex = 1; // Scene 1 = ServerRoomScene

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private UnityEngine.UI.Button connectWalletButton;
    [SerializeField] private TextMeshProUGUI buttonText; // Add reference to button's text component

    [Header("Manager References")]
    [SerializeField] private Web3Manager web3Manager;
    [SerializeField] private PlayFabManager playFabManager;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // Public Properties - API Interface
    public bool IsFullyConnected => walletConnected && playFabConnected && nftChecked && usernameReady;
    public string GetWalletAddress() => currentWalletAddress;
    public string GetPlayerUsername() => currentUsername;
    public int GetNFTBalance() => currentNFTBalance;
    public bool HasNFTs() => currentNFTBalance > 0;

    // State Management
    private bool isProcessing = false;
    private string currentWalletAddress = "";
    private string currentUsername = "";
    private int currentNFTBalance = 0;

    // Flow tracking
    private bool walletConnected = false;
    private bool playFabConnected = false;
    private bool nftChecked = false;
    private bool usernameReady = false;

    // UI Text Constants
    private const string BUTTON_TEXT_CONNECT = "Connect Wallet";
    private const string BUTTON_TEXT_CONNECTING = "Connecting...";

    private void Start()
    {
        SetupUI();
        SetupManagerEvents();
        ClearStatusMessage(); // Start with clean status
        SetButtonText(BUTTON_TEXT_CONNECT);
        DebugLog("NetworkManager initialized - ready for one-click connection");
    }

    /// <summary>
    /// Setup UI elements
    /// </summary>
    private void SetupUI()
    {
        if (connectWalletButton != null)
        {
            connectWalletButton.onClick.AddListener(StartConnectionProcess);
            
            // Get button text component if not assigned
            if (buttonText == null && connectWalletButton.transform.childCount > 0)
            {
                buttonText = connectWalletButton.GetComponentInChildren<TextMeshProUGUI>();
            }
        }
    }

    /// <summary>
    /// Setup event listeners for both managers
    /// </summary>
    private void SetupManagerEvents()
    {
        if (web3Manager != null)
        {
            web3Manager.OnWalletConnected += OnWalletConnected;
            web3Manager.OnNFTBalanceChecked += OnNFTBalanceChecked;
            web3Manager.OnWeb3Error += OnWeb3Error;
        }

        if (playFabManager != null)
        {
            playFabManager.OnPlayFabConnected += OnPlayFabConnected;
            playFabManager.OnUsernameReady += OnUsernameReady;
            playFabManager.OnPlayFabError += OnPlayFabError;
            playFabManager.OnUsernameModalShown += OnUsernameModalShown;
        }
    }

    /// <summary>
    /// Start the complete connection process (ONE BUTTON DOES EVERYTHING)
    /// </summary>
    public async void StartConnectionProcess()
    {
        if (isProcessing) return;

        DebugLog("🚀 Starting complete connection process...");
        isProcessing = true;

        // Reset state
        ResetConnectionState();

        // Update UI to show connecting
        SetButtonText(BUTTON_TEXT_CONNECTING);
        ClearStatusMessage();
        SetButtonsEnabled(false);

        try
        {
            // Step 1: Connect Wallet
            bool walletSuccess = await web3Manager.ConnectWallet();

            if (!walletSuccess)
            {
                ShowError("Wallet connection failed");
                return;
            }

            // Wait for wallet connected event to proceed
            // The flow continues in OnWalletConnected()
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ Connection process failed: {ex.Message}");
            ShowError($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Reset all connection state
    /// </summary>
    private void ResetConnectionState()
    {
        walletConnected = false;
        playFabConnected = false;
        nftChecked = false;
        usernameReady = false;
        currentWalletAddress = "";
        currentUsername = "";
        currentNFTBalance = 0;
    }

    /// <summary>
    /// Event: Wallet connected successfully
    /// </summary>
    private async void OnWalletConnected(string walletAddress)
    {
        walletConnected = true;
        currentWalletAddress = walletAddress;
        DebugLog($"✅ Wallet connected: {walletAddress}");

        // Step 2: Connect to PlayFab (silently)
        bool playFabSuccess = await playFabManager.ConnectToPlayFab(walletAddress);

        if (!playFabSuccess)
        {
            ShowError("Connection failed - please try again");
            return;
        }

        // The flow continues in OnPlayFabConnected()
    }

    /// <summary>
    /// Event: PlayFab connected successfully
    /// </summary>
    private async void OnPlayFabConnected()
    {
        playFabConnected = true;
        DebugLog("✅ PlayFab connected");

        // Step 3: Check NFT Balance (silently)
        int balance = await web3Manager.CheckNFTBalance();

        // The flow continues in OnNFTBalanceChecked()
    }

    /// <summary>
    /// Event: NFT balance checked
    /// </summary>
    private async void OnNFTBalanceChecked(int balance)
    {
        nftChecked = true;
        currentNFTBalance = balance;
        DebugLog($"✅ NFT balance: {balance}");

        // Step 4: Check Username (silently)
        bool hasUsername = await playFabManager.CheckUsernameStatus();

        if (!hasUsername)
        {
            // Show username modal and wait for completion
            playFabManager.ShowUsernameModal();
            // The flow continues in OnUsernameReady()
        }
        else
        {
            // Username already exists, flow continues in OnUsernameReady()
        }
    }

    /// <summary>
    /// Event: Username is ready (either existing or newly created)
    /// </summary>
    private async void OnUsernameReady(string username)
    {
        usernameReady = true;
        currentUsername = username;
        DebugLog($"✅ Username ready: {username}");

        // Update button to show username
        SetButtonText(username);

        // Step 5: Save final data and proceed
        await FinalizeAndProceed();
    }

    /// <summary>
    /// Event: Username modal was shown (keep connecting state)
    /// </summary>
    private void OnUsernameModalShown()
    {
        // Keep button disabled and showing "Connecting..." while modal is shown
        SetButtonsEnabled(false);
        SetButtonText(BUTTON_TEXT_CONNECTING);
    }

    /// <summary>
    /// Finalize setup and proceed to server scene
    /// </summary>
    private async System.Threading.Tasks.Task FinalizeAndProceed()
    {
        try
        {
            // Save all player data to PlayFab (silently)
            bool saveSuccess = await playFabManager.SavePlayerData(
                currentWalletAddress,
                currentUsername,
                currentNFTBalance
            );

            // Brief pause to show username, then proceed
            await System.Threading.Tasks.Task.Delay(1500);

            LoadServerScene();
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ Finalization failed: {ex.Message}");
            LoadServerScene(); // Continue anyway
        }
        finally
        {
            isProcessing = false;
        }
    }

    /// <summary>
    /// Load the CMP Menu scene with Web3 player data
    /// </summary>
    private void LoadServerScene()
    {
        DebugLog($"🚀 Loading CMP Menu scene with Web3 player data");

        // Store Web3 authentication data for CMP to use
        PlayerPrefs.SetString("Web3_Username", currentUsername);
        PlayerPrefs.SetString("Web3_WalletAddress", currentWalletAddress);
        PlayerPrefs.SetInt("Web3_NFTBalance", currentNFTBalance);
        PlayerPrefs.SetInt("Web3_HasNFTs", HasNFTs() ? 1 : 0);
        PlayerPrefs.SetString("Web3_AuthComplete", "true");
        PlayerPrefs.Save();

        DebugLog($"✅ Web3 data stored - Username: {currentUsername}, NFTs: {currentNFTBalance}");

        // Load CMP's Menu scene instead of ServerRoomScene
        SceneManager.LoadScene("Menu");
    }

    /// <summary>
    /// Event: Web3 error occurred
    /// </summary>
    private void OnWeb3Error(string errorMessage)
    {
        ShowError("Wallet connection failed");
    }

    /// <summary>
    /// Event: PlayFab error occurred
    /// </summary>
    private void OnPlayFabError(string errorMessage)
    {
        // If error is about username requirement, reset everything
        if (errorMessage.Contains("Username is required"))
        {
            DebugLog("Username required - resetting connection");

            // Disconnect wallet
            if (web3Manager != null && web3Manager.IsWalletConnected)
            {
                web3Manager.DisconnectWallet();
            }

            // Reset all state
            ResetConnectionState();

            ShowError("Username is required - please try again");
        }
        else
        {
            ShowError("Connection failed - please try again");
        }
    }

    /// <summary>
    /// Show error and enable retry
    /// </summary>
    private void ShowError(string errorMessage)
    {
        DebugLog($"❌ Error: {errorMessage}");
        
        // Show error in status text
        ShowStatusMessage(errorMessage);
        
        // Reset button to allow retry
        SetButtonText(BUTTON_TEXT_CONNECT);
        SetButtonsEnabled(true);
        isProcessing = false;
    }

    /// <summary>
    /// Set button text
    /// </summary>
    private void SetButtonText(string text)
    {
        if (buttonText != null)
        {
            buttonText.text = text;
        }
    }

    /// <summary>
    /// Show status message (for errors only)
    /// </summary>
    private void ShowStatusMessage(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    /// <summary>
    /// Clear status message
    /// </summary>
    private void ClearStatusMessage()
    {
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    /// <summary>
    /// Enable/disable UI buttons
    /// </summary>
    private void SetButtonsEnabled(bool enabled)
    {
        if (connectWalletButton != null)
        {
            connectWalletButton.interactable = enabled;
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NetworkManager] {message}");
        }
    }

    private void OnDestroy()
    {
        // Cleanup UI events
        if (connectWalletButton != null)
        {
            connectWalletButton.onClick.RemoveListener(StartConnectionProcess);
        }

        // Cleanup manager events
        if (web3Manager != null)
        {
            web3Manager.OnWalletConnected -= OnWalletConnected;
            web3Manager.OnNFTBalanceChecked -= OnNFTBalanceChecked;
            web3Manager.OnWeb3Error -= OnWeb3Error;
        }

        if (playFabManager != null)
        {
            playFabManager.OnPlayFabConnected -= OnPlayFabConnected;
            playFabManager.OnUsernameReady -= OnUsernameReady;
            playFabManager.OnPlayFabError -= OnPlayFabError;
            playFabManager.OnUsernameModalShown -= OnUsernameModalShown;
        }
    }

    /// <summary>
    /// Static helper class for Web3 authentication data (for CMP integration)
    /// </summary>
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