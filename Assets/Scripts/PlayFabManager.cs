using System.Collections;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;

/// <summary>
/// PlayFabManager - Handles PlayFab Authentication and Player Data
/// Extracted from NetworkManager for better code organization
/// </summary>
public class PlayFabManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private string playFabTitleId = "105E07";
    
    [Header("Username Creation UI")]
    [SerializeField] private GameObject usernameModal;
    [SerializeField] private TMPro.TMP_InputField usernameInputField;
    [SerializeField] private UnityEngine.UI.Button confirmUsernameButton;
    [SerializeField] private UnityEngine.UI.Button cancelUsernameButton;
    [SerializeField] private TextMeshProUGUI usernameErrorText;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // State
    private bool isPlayFabConnected = false;
    private bool hasUsername = false;
    private string playerUsername = "";
    private string walletAddress = "";
    
    // Events for NetworkManager to listen to
    public System.Action OnPlayFabConnected;
    public System.Action<string> OnUsernameReady;
    public System.Action<string> OnPlayFabError;
    public System.Action OnUsernameModalShown;
    
    // Public Properties - API Interface
    public bool IsPlayFabConnected => isPlayFabConnected;
    public bool HasUsername => hasUsername;
    public string GetPlayerUsername() => playerUsername;
    
    private void Start()
    {
        InitializePlayFab();
        SetupUsernameUI();
    }
    
    /// <summary>
    /// Initialize PlayFab settings
    /// </summary>
    private void InitializePlayFab()
    {
        if (!string.IsNullOrEmpty(playFabTitleId))
        {
            PlayFabSettings.staticSettings.TitleId = playFabTitleId;
            DebugLog($"PlayFab initialized with Title ID: {playFabTitleId}");
        }
        else
        {
            DebugLog("❌ PlayFab Title ID not set!");
        }
    }
    
    /// <summary>
    /// Setup username modal UI
    /// </summary>
    private void SetupUsernameUI()
    {
        if (usernameModal != null)
        {
            usernameModal.SetActive(false);
        }
        
        if (confirmUsernameButton != null)
        {
            confirmUsernameButton.onClick.AddListener(OnConfirmUsername);
        }
        
        if (cancelUsernameButton != null)
        {
            cancelUsernameButton.onClick.AddListener(OnCancelUsername);
        }
        
        if (usernameInputField != null)
        {
            usernameInputField.onValueChanged.AddListener(OnUsernameInputChanged);
            usernameInputField.characterLimit = 20;
        }
    }
    
    /// <summary>
    /// Connect to PlayFab using wallet address
    /// </summary>
    public async System.Threading.Tasks.Task<bool> ConnectToPlayFab(string walletAddr)
    {
        try
        {
            walletAddress = walletAddr;
            DebugLog($"Connecting to PlayFab with wallet: {walletAddress}");
            
            var request = new LoginWithCustomIDRequest
            {
                CustomId = walletAddress.ToLower(),
                CreateAccount = true,
                InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
                {
                    GetPlayerProfile = true,
                    GetUserData = true
                }
            };
            
            var tcs = new System.Threading.Tasks.TaskCompletionSource<LoginResult>();
            
            PlayFabClientAPI.LoginWithCustomID(request,
                result => tcs.SetResult(result),
                error => tcs.SetException(new System.Exception(error.ErrorMessage))
            );
            
            var loginResult = await tcs.Task;
            isPlayFabConnected = true;
            
            DebugLog($"✅ PlayFab connected! PlayFab ID: {loginResult.PlayFabId}");
            
            if (loginResult.NewlyCreated)
            {
                DebugLog("🆕 New PlayFab account created");
            }
            else
            {
                DebugLog("👋 Existing PlayFab account found");
            }
            
            OnPlayFabConnected?.Invoke();
            return true;
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ PlayFab connection failed: {ex.Message}");
            OnPlayFabError?.Invoke($"PlayFab connection failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Check if player has a username
    /// </summary>
    public async System.Threading.Tasks.Task<bool> CheckUsernameStatus()
    {
        try
        {
            DebugLog("Checking if player has username...");
            
            var request = new GetUserDataRequest
            {
                Keys = new System.Collections.Generic.List<string> { "Username" }
            };
            
            var tcs = new System.Threading.Tasks.TaskCompletionSource<GetUserDataResult>();
            
            PlayFabClientAPI.GetUserData(request,
                result => tcs.SetResult(result),
                error => tcs.SetException(new System.Exception(error.ErrorMessage))
            );
            
            var getUserDataResult = await tcs.Task;
            
            if (getUserDataResult.Data.ContainsKey("Username"))
            {
                playerUsername = getUserDataResult.Data["Username"].Value;
                hasUsername = !string.IsNullOrEmpty(playerUsername);
                
                if (hasUsername)
                {
                    DebugLog($"✅ Existing username found: {playerUsername}");
                    OnUsernameReady?.Invoke(playerUsername);
                    return true;
                }
                else
                {
                    DebugLog("❌ Username field exists but is empty");
                }
            }
            else
            {
                DebugLog("❌ No username found - need to create one");
            }
            
            hasUsername = false;
            return false;
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ Failed to check username: {ex.Message}");
            hasUsername = false;
            return false;
        }
    }
    
    /// <summary>
    /// Show username creation modal
    /// </summary>
    public void ShowUsernameModal()
    {
        DebugLog("Showing username creation modal");
        
        if (usernameModal != null)
        {
            usernameModal.SetActive(true);
        }
        
        if (usernameInputField != null)
        {
            usernameInputField.text = "";
            usernameInputField.Select();
            usernameInputField.ActivateInputField();
        }
        
        if (usernameErrorText != null)
        {
            usernameErrorText.text = "";
        }
        
        OnUsernameModalShown?.Invoke();
    }
    
    /// <summary>
    /// Hide username creation modal
    /// </summary>
    public void HideUsernameModal()
    {
        if (usernameModal != null)
        {
            usernameModal.SetActive(false);
        }
    }
    
    /// <summary>
    /// Save all player data to PlayFab
    /// </summary>
    public async System.Threading.Tasks.Task<bool> SavePlayerData(string walletAddr, string username, int nftBalance)
    {
        try
        {
            DebugLog("Saving player data to PlayFab...");
            
            var dataDict = new System.Collections.Generic.Dictionary<string, string>
            {
                {"WalletAddress", walletAddr},
                {"Username", username},
                {"NFTBalance", nftBalance.ToString()},
                {"LastLogin", System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")},
                {"LastNFTCheck", System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}
            };
            
            var request = new UpdateUserDataRequest { Data = dataDict };
            
            var tcs = new System.Threading.Tasks.TaskCompletionSource<UpdateUserDataResult>();
            
            PlayFabClientAPI.UpdateUserData(request,
                result => tcs.SetResult(result),
                error => tcs.SetException(new System.Exception(error.ErrorMessage))
            );
            
            await tcs.Task;
            
            DebugLog("✅ Player data saved to PlayFab");
            return true;
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ Failed to save player data: {ex.Message}");
            OnPlayFabError?.Invoke($"Failed to save player data: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Save username to PlayFab (User Data only, DisplayName already set during uniqueness check)
    /// </summary>
    private async System.Threading.Tasks.Task SaveUsernameToPlayFab(string username)
    {
        // Save to User Data only (DisplayName was already set during uniqueness check)
        var userDataRequest = new UpdateUserDataRequest
        {
            Data = new System.Collections.Generic.Dictionary<string, string>
            {
                {"Username", username},
                {"UsernameCreatedAt", System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}
            }
        };
        
        var userDataTcs = new System.Threading.Tasks.TaskCompletionSource<UpdateUserDataResult>();
        
        PlayFabClientAPI.UpdateUserData(userDataRequest,
            result => userDataTcs.SetResult(result),
            error => userDataTcs.SetException(new System.Exception(error.ErrorMessage))
        );
        
        await userDataTcs.Task;
        
        DebugLog($"✅ Username '{username}' saved to User Data (DisplayName already set)");
    }
    
    /// <summary>
    /// Username validation
    /// </summary>
    private bool IsValidUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        if (username.Length < 3) return false;
        if (username.Length > 20) return false;
        
        foreach (char c in username)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Get username validation error message
    /// </summary>
    private string GetUsernameError(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "Username cannot be empty";
        
        if (username.Length < 3)
            return "Username must be at least 3 characters";
        
        if (username.Length > 20)
            return "Username must be 20 characters or less";
        
        foreach (char c in username)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                return "Username can only contain letters, numbers, _ and -";
        }
        
        return "";
    }
    
    /// <summary>
    /// Handle username input changes (real-time validation)
    /// </summary>
    private void OnUsernameInputChanged(string username)
    {
        if (usernameErrorText != null)
        {
            string error = GetUsernameError(username);
            usernameErrorText.text = error;
            usernameErrorText.color = string.IsNullOrEmpty(error) ? Color.green : Color.red;
        }
        
        if (confirmUsernameButton != null)
        {
            confirmUsernameButton.interactable = IsValidUsername(username);
        }
    }
    
    /// <summary>
    /// Handle confirm username button click
    /// </summary>
    private async void OnConfirmUsername()
    {
        if (usernameInputField == null)
        {
            DebugLog("❌ Username input field not found");
            return;
        }
        
        string username = usernameInputField.text.Trim();
        
        if (!IsValidUsername(username))
        {
            DebugLog("❌ Invalid username");
            return;
        }
        
        DebugLog($"Checking username uniqueness: {username}");
        
        if (confirmUsernameButton != null)
        {
            confirmUsernameButton.interactable = false;
        }
        
        if (usernameErrorText != null)
        {
            usernameErrorText.text = "Checking if username is available...";
            usernameErrorText.color = Color.yellow;
        }
        
        try
        {
            // Check if username is unique
            bool isUnique = await IsUsernameUnique(username);
            
            if (!isUnique)
            {
                if (usernameErrorText != null)
                {
                    usernameErrorText.text = "Username already taken. Please choose another.";
                    usernameErrorText.color = Color.red;
                }
                
                if (confirmUsernameButton != null)
                {
                    confirmUsernameButton.interactable = true;
                }
                return;
            }
            
            // Username is unique, save it
            await SaveUsernameToPlayFab(username);
            
            playerUsername = username;
            hasUsername = true;
            
            DebugLog($"✅ Username created: {username}");
            
            HideUsernameModal();
            OnUsernameReady?.Invoke(username);
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ Failed to save username: {ex.Message}");
            
            if (usernameErrorText != null)
            {
                usernameErrorText.text = "Failed to save username. Please try again.";
                usernameErrorText.color = Color.red;
            }
            
            if (confirmUsernameButton != null)
            {
                confirmUsernameButton.interactable = true;
            }
        }
    }
    
    /// <summary>
    /// Handle cancel username button click
    /// </summary>
    /// <summary>
    /// Handle cancel username button click
    /// </summary>
    private void OnCancelUsername()
    {
        DebugLog("Username creation cancelled by user");
        HideUsernameModal();
        
        // Username is REQUIRED - disconnect and reset everything
        OnPlayFabError?.Invoke("Username is required to continue. Please reconnect your wallet.");
    }
    
    /// <summary>
    /// Check if username is unique across all players
    /// </summary>
    private async System.Threading.Tasks.Task<bool> IsUsernameUnique(string username)
    {
        try
        {
            DebugLog($"Checking uniqueness for username: {username}");
            
            // Test uniqueness by trying to set it as DisplayName
            // PlayFab will reject if the DisplayName is already taken
            var testRequest = new UpdateUserTitleDisplayNameRequest
            {
                DisplayName = username
            };
            
            var tcs = new System.Threading.Tasks.TaskCompletionSource<UpdateUserTitleDisplayNameResult>();
            
            PlayFabClientAPI.UpdateUserTitleDisplayName(testRequest,
                result => {
                    DebugLog($"✅ Username '{username}' is available");
                    tcs.SetResult(result);
                },
                error => {
                    DebugLog($"❌ Username '{username}' is not available: {error.ErrorMessage}");
                    tcs.SetException(new System.Exception(error.ErrorMessage));
                }
            );
            
            try
            {
                await tcs.Task;
                return true; // Username is unique and already set!
            }
            catch (System.Exception ex)
            {
                if (ex.Message.Contains("Name not available") || ex.Message.Contains("already taken"))
                {
                    return false; // Username is taken
                }
                
                // Other error, assume unique to not block user
                DebugLog($"⚠️ Uniqueness check failed with unexpected error: {ex.Message}");
                return true;
            }
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ Username uniqueness check failed: {ex.Message}");
            return true; // On error, allow the username
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayFabManager] {message}");
        }
    }
    
    private void OnDestroy()
    {
        if (confirmUsernameButton != null)
        {
            confirmUsernameButton.onClick.RemoveListener(OnConfirmUsername);
        }
        
        if (cancelUsernameButton != null)
        {
            cancelUsernameButton.onClick.RemoveListener(OnCancelUsername);
        }
        
        if (usernameInputField != null)
        {
            usernameInputField.onValueChanged.RemoveListener(OnUsernameInputChanged);
        }
    }
}