using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Updated NetworkManager with better user feedback and connection progress
/// </summary>
public class NetworkManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int serverSceneIndex = 1;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private UnityEngine.UI.Button connectWalletButton;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private UnityEngine.UI.Slider progressSlider; // New: Progress bar
    [SerializeField] private UnityEngine.UI.Button cancelButton; // New: Cancel button

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
        ClearStatusMessage();
        SetButtonText(BUTTON_TEXT_CONNECT);
        SetProgress(0f);
        DebugLog("NetworkManager initialized with optimized connection flow");
    }

    /// <summary>
    /// Setup UI elements including new progress indicators
    /// </summary>
    private void SetupUI()
    {
        if (connectWalletButton != null)
        {
            connectWalletButton.onClick.AddListener(StartConnectionProcess);
            
            if (buttonText == null && connectWalletButton.transform.childCount > 0)
            {
                buttonText = connectWalletButton.GetComponentInChildren<TextMeshProUGUI>();
            }
        }
        
        // Setup cancel button
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(CancelConnection);
            cancelButton.gameObject.SetActive(false); // Hidden by default
        }
        
        // Setup progress slider
        if (progressSlider != null)
        {
            progressSlider.gameObject.SetActive(false); // Hidden by default
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 0f;
        }
    }

    /// <summary>
    /// Setup event listeners including new progress events
    /// </summary>
    private void SetupManagerEvents()
    {
        if (web3Manager != null)
        {
            web3Manager.OnWalletConnected += OnWalletConnected;
            web3Manager.OnNFTBalanceChecked += OnNFTBalanceChecked;
            web3Manager.OnWeb3Error += OnWeb3Error;
            web3Manager.OnConnectionProgress += OnConnectionProgress; // New: Progress updates
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
    /// Start the connection process with improved feedback
    /// </summary>
    public async void StartConnectionProcess()
    {
        if (isProcessing) return;

        DebugLog("🚀 Starting optimized connection process...");
        isProcessing = true;

        ResetConnectionState();
        ShowConnectionUI(true);
        SetProgress(0.1f, "Initializing connection...");

        try
        {
            // Step 1: Connect Wallet (this is the slow part)
            SetProgress(0.2f, "Connecting to wallet...");
            bool walletSuccess = await web3Manager.ConnectWallet();

            if (!walletSuccess)
            {
                ShowError("Wallet connection failed");
                return;
            }

            // Flow continues in OnWalletConnected()
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ Connection process failed: {ex.Message}");
            ShowError($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancel the current connection attempt
    /// </summary>
    public void CancelConnection()
    {
        if (!isProcessing) return;
        
        DebugLog("🛑 User cancelled connection");
        
        // Cancel the web3 connection
        if (web3Manager != null)
        {
            web3Manager.CancelConnection();
        }
        
        ShowError("Connection cancelled");
    }

    /// <summary>
    /// Show/hide connection UI elements
    /// </summary>
    private void ShowConnectionUI(bool connecting)
    {
        // Only change button text if we don't have a username set
        if (string.IsNullOrEmpty(currentUsername))
        {
            SetButtonText(connecting ? BUTTON_TEXT_CONNECTING : BUTTON_TEXT_CONNECT);
        }
        // If we have a username, keep it displayed

        SetButtonsEnabled(!connecting);

        if (progressSlider != null)
        {
            progressSlider.gameObject.SetActive(connecting);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(connecting);
        }
    }

    /// <summary>
    /// Update progress bar and status
    /// </summary>
    private void SetProgress(float progress, string status = "")
    {
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }
        
        if (!string.IsNullOrEmpty(status))
        {
            ShowStatusMessage(status);
        }
        
        DebugLog($"Progress: {progress * 100:F0}% - {status}");
    }

    /// <summary>
    /// Handle connection progress updates from Web3Manager
    /// </summary>
    private void OnConnectionProgress(string progressMessage)
    {
        DebugLog($"Connection progress: {progressMessage}");
        
        // Update status text with current progress
        if (statusText != null)
        {
            statusText.text = progressMessage;
        }
        
        // Update progress bar based on message content
        float progress = GetProgressFromMessage(progressMessage);
        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }
    }

    /// <summary>
    /// Estimate progress from connection messages
    /// </summary>
    private float GetProgressFromMessage(string message)
    {
        message = message.ToLower();
        
        if (message.Contains("quick reconnect")) return 0.3f;
        if (message.Contains("walletconnect")) return 0.4f;
        if (message.Contains("metamask")) return 0.5f;
        if (message.Contains("coinbase")) return 0.6f;
        if (message.Contains("attempt 2")) return 0.7f;
        if (message.Contains("attempt 3")) return 0.8f;
        
        return 0.3f; // Default progress for wallet connection phase
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
        
        SetProgress(0.5f, "Wallet connected! Connecting to PlayFab...");

        // Step 2: Connect to PlayFab
        bool playFabSuccess = await playFabManager.ConnectToPlayFab(walletAddress);

        if (!playFabSuccess)
        {
            ShowError("PlayFab connection failed - please try again");
            return;
        }
    }

    /// <summary>
    /// Event: PlayFab connected successfully
    /// </summary>
    private async void OnPlayFabConnected()
    {
        playFabConnected = true;
        DebugLog("✅ PlayFab connected");
        
        SetProgress(0.7f, "PlayFab connected! Checking NFTs...");

        // Step 3: Check NFT Balance
        int balance = await web3Manager.CheckNFTBalance();
    }

    /// <summary>
    /// Event: NFT balance checked
    /// </summary>
    private async void OnNFTBalanceChecked(int balance)
    {
        nftChecked = true;
        currentNFTBalance = balance;
        DebugLog($"✅ NFT balance: {balance}");
        
        SetProgress(0.85f, "NFTs checked! Verifying username...");

        // Step 4: Check Username
        bool hasUsername = await playFabManager.CheckUsernameStatus();

        if (!hasUsername)
        {
            SetProgress(0.9f, "Username required...");
            playFabManager.ShowUsernameModal();
        }
    }

    /// <summary>
    /// Event: Username is ready
    /// </summary>
    private async void OnUsernameReady(string username)
    {
        usernameReady = true;
        currentUsername = username;
        DebugLog($"✅ Username ready: {username}");

        SetProgress(0.95f, "Finalizing...");
        SetButtonText(username);

        await FinalizeAndProceed();
    }

    /// <summary>
    /// Event: Username modal was shown
    /// </summary>
    private void OnUsernameModalShown()
    {
        SetProgress(0.9f, "Please choose a username...");
        ShowConnectionUI(true); // Keep connection UI active
    }

    /// <summary>
    /// Finalize setup and proceed to server scene
    /// </summary>
    private async System.Threading.Tasks.Task FinalizeAndProceed()
    {
        try
        {
            SetProgress(0.98f, "Saving player data...");
            
            bool saveSuccess = await playFabManager.SavePlayerData(
                currentWalletAddress,
                currentUsername,
                currentNFTBalance
            );

            SetProgress(1.0f, "Complete! Loading game...");
            
            // Brief pause to show completion
            await System.Threading.Tasks.Task.Delay(1000);

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
            ShowConnectionUI(false);
        }
    }

    /// <summary>
    /// Load the game scene
    /// </summary>
    private void LoadServerScene()
    {
        DebugLog($"🚀 Loading game scene with Web3 player data");

        // Store Web3 authentication data
        PlayerPrefs.SetString("Web3_Username", currentUsername);
        PlayerPrefs.SetString("Web3_WalletAddress", currentWalletAddress);
        PlayerPrefs.SetInt("Web3_NFTBalance", currentNFTBalance);
        PlayerPrefs.SetInt("Web3_HasNFTs", HasNFTs() ? 1 : 0);
        PlayerPrefs.SetString("Web3_AuthComplete", "true");
        PlayerPrefs.Save();

        DebugLog($"✅ Web3 data stored - Username: {currentUsername}, NFTs: {currentNFTBalance}");

        SceneManager.LoadScene(1);
    }

    /// <summary>
    /// Event: Web3 error occurred
    /// </summary>
    private void OnWeb3Error(string errorMessage)
    {
        ShowError("Wallet connection failed - please try again");
    }

    /// <summary>
    /// Event: PlayFab error occurred
    /// </summary>
    private void OnPlayFabError(string errorMessage)
    {
        if (errorMessage.Contains("Username is required"))
        {
            DebugLog("Username required - resetting connection");

            if (web3Manager != null && web3Manager.IsWalletConnected)
            {
                web3Manager.DisconnectWallet();
            }

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
        
        ShowStatusMessage(errorMessage);
        ShowConnectionUI(false);
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
    /// Show status message
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
        
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(CancelConnection);
        }

        // Cleanup manager events
        if (web3Manager != null)
        {
            web3Manager.OnWalletConnected -= OnWalletConnected;
            web3Manager.OnNFTBalanceChecked -= OnNFTBalanceChecked;
            web3Manager.OnWeb3Error -= OnWeb3Error;
            web3Manager.OnConnectionProgress -= OnConnectionProgress;
        }

        if (playFabManager != null)
        {
            playFabManager.OnPlayFabConnected -= OnPlayFabConnected;
            playFabManager.OnUsernameReady -= OnUsernameReady;
            playFabManager.OnPlayFabError -= OnPlayFabError;
            playFabManager.OnUsernameModalShown -= OnUsernameModalShown;
        }
    }
}