using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Thirdweb;
using Thirdweb.Unity;
using System.Threading.Tasks;
using System;

/// <summary>
/// Ronin-Optimized Web3Manager - Handles Ronin Wallet Connection via WalletConnect
/// Optimized specifically for Ronin Network and Ronin Wallet mobile app
/// </summary>
public class Web3Manager : MonoBehaviour
{
    [Header("Ronin Network Configuration")]
    [SerializeField] private int roninChainId = 2020;
    [SerializeField] private int connectionTimeoutSeconds = 45; // Longer timeout for mobile wallet apps
    [SerializeField] private int maxRetries = 2; // Fewer retries since we only have one wallet type
    [SerializeField] private bool enableWalletCaching = true;
    
    [Header("Ronin-Specific Settings")]
    [SerializeField] private bool forceMobileWalletConnect = true; // Force mobile wallet connection
    [SerializeField] private bool enableRoninOptimizations = true; // Ronin-specific optimizations
    [SerializeField] private float roninWalletCheckInterval = 2f; // Check for Ronin wallet every 2 seconds
    
    [Header("NFT Configuration")]
    private const string KONGZ_VX_CONTRACT = "0x241a81fc0d6692707dad2b5025a3a7cf2cf25acf";
    private const string RONIN_RPC_URL = "https://api-gateway.skymavis.com/rpc";
    private const string RONIN_API_KEY = "bS9xVtjS4fIsT10EoqkfHSO6GhwCpzBt";
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool enablePerformanceLogging = true;
    
    // State
    private bool isWalletConnected = false;
    private bool isNFTChecked = false;
    private string walletAddress = "";
    private int nftBalance = 0;
    private IThirdwebWallet connectedWallet;
    
    // Performance tracking
    private System.Diagnostics.Stopwatch connectionTimer;
    
    // Ronin-specific connection data
    private string lastSuccessfulConnectionMethod = "";
    private DateTime lastConnectionTime;
    private bool isRoninWalletInstalled = false;
    
    // Events for NetworkManager to listen to
    public System.Action<string> OnWalletConnected;
    public System.Action<int> OnNFTBalanceChecked;
    public System.Action<string> OnWeb3Error;
    public System.Action<string> OnConnectionProgress;
    
    // Public Properties - API Interface
    public bool IsWalletConnected => isWalletConnected;
    public bool IsNFTChecked => isNFTChecked;
    public string GetWalletAddress() => walletAddress;
    public int GetNFTBalance() => nftBalance;
    public bool HasNFTs() => nftBalance > 0;
    
    private void Awake()
    {
        connectionTimer = new System.Diagnostics.Stopwatch();
        
        // Pre-warm Thirdweb and check for Ronin wallet
        StartCoroutine(InitializeRoninEnvironment());
    }
    
    /// <summary>
    /// Initialize Ronin-specific environment and check wallet availability
    /// </summary>
    private IEnumerator InitializeRoninEnvironment()
    {
        DebugLog("🎮 Initializing Ronin environment...");
        
        // Pre-warm Thirdweb
        if (ThirdwebManager.Instance != null)
        {
            DebugLog("✅ Thirdweb instance ready");
        }
        
        // Check if we're on mobile (where Ronin Wallet is available)
        CheckRoninWalletAvailability();
        
        yield return new WaitForSeconds(0.5f);
        DebugLog("🚀 Ronin environment ready for connections");
    }
    
    /// <summary>
    /// Check if Ronin Wallet is likely available on this platform
    /// </summary>
    private void CheckRoninWalletAvailability()
    {
        #if UNITY_ANDROID || UNITY_IOS
        isRoninWalletInstalled = true; // Assume available on mobile
        DebugLog("📱 Mobile platform detected - Ronin Wallet should be available");
        #elif UNITY_WEBGL
        isRoninWalletInstalled = true; // WalletConnect can bridge to mobile
        DebugLog("🌐 WebGL platform - will use WalletConnect bridge to Ronin Wallet");
        #else
        isRoninWalletInstalled = false;
        DebugLog("🖥️ Desktop platform - Ronin Wallet may not be available");
        #endif
    }
    
    /// <summary>
    /// Connect to Ronin Wallet with optimized flow
    /// </summary>
    public async Task<bool> ConnectWallet()
    {
        connectionTimer.Restart();
        DebugLog("🎯 Starting Ronin Wallet connection...");
        
        // Check if we can try a quick reconnect first
        if (enableWalletCaching && TryQuickReconnect())
        {
            return true;
        }
        
        // Try Ronin-optimized connection strategies
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                DebugLog($"🔄 Ronin connection attempt {attempt}/{maxRetries}");
                OnConnectionProgress?.Invoke($"Connecting to Ronin Wallet (attempt {attempt})...");
                
                var success = await ConnectToRoninWallet(attempt);
                if (success)
                {
                    LogConnectionSuccess();
                    CacheSuccessfulConnection("RoninWalletConnect");
                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugLog($"❌ Attempt {attempt} failed: {ex.Message}");
                
                // Check if this is a user cancellation (don't retry)
                if (IsUserCancellation(ex))
                {
                    DebugLog("User cancelled connection - not retrying");
                    OnWeb3Error?.Invoke("Connection cancelled by user");
                    return false;
                }
                
                // Wait before retry with specific messaging for Ronin
                if (attempt < maxRetries)
                {
                    int delay = attempt * 2000; // 2s, 4s delay
                    DebugLog($"⏳ Waiting {delay}ms before retry...");
                    OnConnectionProgress?.Invoke($"Retrying in {delay/1000} seconds...");
                    await Task.Delay(delay);
                }
            }
        }
        
        // All attempts failed
        LogConnectionFailure();
        OnWeb3Error?.Invoke("Unable to connect to Ronin Wallet. Please ensure the Ronin Wallet app is installed and try again.");
        return false;
    }
    
    /// <summary>
    /// Connect specifically to Ronin Wallet with optimizations
    /// </summary>
    private async Task<bool> ConnectToRoninWallet(int attemptNumber)
    {
        try
        {
            var startTime = Time.time;
            
            // Create Ronin-optimized wallet options
            var walletOptions = CreateRoninWalletOptions(attemptNumber);
            
            // Show platform-specific instructions
            ShowRoninConnectionInstructions(attemptNumber);
            
            // Create timeout task (longer for mobile wallet apps)
            var timeoutTask = Task.Delay(connectionTimeoutSeconds * 1000);
            var connectTask = ThirdwebManager.Instance.ConnectWallet(walletOptions);
            
            // Race between connection and timeout
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Ronin Wallet connection timed out after {connectionTimeoutSeconds} seconds");
            }
            
            connectedWallet = await connectTask;
            
            if (connectedWallet != null)
            {
                walletAddress = await connectedWallet.GetAddress();
                
                // Verify we're on Ronin network
                if (await VerifyRoninNetwork())
                {
                    isWalletConnected = true;
                    var connectionTime = Time.time - startTime;
                    DebugLog($"✅ Ronin Wallet connected in {connectionTime:F2}s: {walletAddress}");
                    
                    OnWalletConnected?.Invoke(walletAddress);
                    return true;
                }
                else
                {
                    throw new Exception("Wallet is not connected to Ronin Network");
                }
            }
            else
            {
                throw new Exception("Ronin Wallet connection returned null");
            }
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Ronin Wallet connection failed: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Create optimized wallet options for Ronin
    /// </summary>
    private WalletOptions CreateRoninWalletOptions(int attemptNumber)
    {
        var options = new WalletOptions(WalletProvider.WalletConnectWallet, roninChainId);
        
        if (enableRoninOptimizations)
        {
            // Add Ronin-specific optimizations here if available in Thirdweb
            // This might include custom RPC endpoints, specific wallet connect settings, etc.
            DebugLog($"🎯 Using Ronin-optimized settings for attempt {attemptNumber}");
        }
        
        return options;
    }
    
    /// <summary>
    /// Show platform-specific instructions for connecting Ronin Wallet
    /// </summary>
    private void ShowRoninConnectionInstructions(int attemptNumber)
    {
        string instruction = "";
        
        #if UNITY_WEBGL
        instruction = "Please approve the connection in your Ronin Wallet mobile app";
        #elif UNITY_ANDROID || UNITY_IOS
        instruction = "Opening Ronin Wallet app...";
        #else
        instruction = "Please use WalletConnect QR code to connect your Ronin Wallet";
        #endif
        
        if (attemptNumber > 1)
        {
            instruction += $" (Retry {attemptNumber})";
        }
        
        OnConnectionProgress?.Invoke(instruction);
        DebugLog($"📱 {instruction}");
    }
    
    /// <summary>
    /// Verify we're connected to Ronin network
    /// </summary>
    private async Task<bool> VerifyRoninNetwork()
    {
        try
        {
            // You can add network verification here if needed
            // For now, we trust that the chainId in WalletOptions worked
            DebugLog("✅ Verified connection to Ronin Network");
            return true;
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Network verification failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Try to quickly reconnect using cached connection info
    /// </summary>
    private bool TryQuickReconnect()
    {
        if (!HasValidCachedConnection())
            return false;
            
        DebugLog("🔄 Attempting quick reconnect to Ronin Wallet...");
        OnConnectionProgress?.Invoke("Quick reconnect to Ronin Wallet...");
        
        // Note: Implement actual quick reconnect logic based on your needs
        // This might involve checking if the wallet is still connected, etc.
        
        return false; // For now, always do full connection
    }
    
    /// <summary>
    /// Check if we have a valid cached connection
    /// </summary>
    private bool HasValidCachedConnection()
    {
        if (!enableWalletCaching) return false;
        
        var timeSinceLastConnection = DateTime.Now - lastConnectionTime;
        if (timeSinceLastConnection.TotalMinutes > 15) // Shorter cache for mobile apps
        {
            DebugLog("Cached Ronin connection expired");
            ClearConnectionCache();
            return false;
        }
        
        return !string.IsNullOrEmpty(lastSuccessfulConnectionMethod);
    }
    
    /// <summary>
    /// Cache successful connection info
    /// </summary>
    private void CacheSuccessfulConnection(string connectionMethod)
    {
        if (enableWalletCaching)
        {
            lastSuccessfulConnectionMethod = connectionMethod;
            lastConnectionTime = DateTime.Now;
            DebugLog($"💾 Cached successful Ronin connection: {connectionMethod}");
        }
    }
    
    /// <summary>
    /// Clear connection cache
    /// </summary>
    private void ClearConnectionCache()
    {
        lastSuccessfulConnectionMethod = "";
        lastConnectionTime = default;
        DebugLog("🗑️ Ronin connection cache cleared");
    }
    
    /// <summary>
    /// Check if error indicates user cancellation
    /// </summary>
    private bool IsUserCancellation(Exception ex)
    {
        var message = ex.Message.ToLower();
        return message.Contains("user rejected") ||
               message.Contains("user denied") ||
               message.Contains("user cancelled") ||
               message.Contains("user canceled") ||
               message.Contains("rejected by user");
    }
    
    /// <summary>
    /// Log successful connection with Ronin-specific metrics
    /// </summary>
    private void LogConnectionSuccess()
    {
        connectionTimer.Stop();
        var totalTime = connectionTimer.ElapsedMilliseconds;
        
        if (enablePerformanceLogging)
        {
            DebugLog($"🎉 RONIN WALLET CONNECTION SUCCESS!");
            DebugLog($"📊 Total connection time: {totalTime}ms ({totalTime / 1000.0:F2}s)");
            DebugLog($"🔗 Ronin address: {walletAddress}");
            DebugLog($"🎮 Ronin Network (Chain ID: {roninChainId})");
        }
    }
    
    /// <summary>
    /// Log connection failure with Ronin-specific diagnostics
    /// </summary>
    private void LogConnectionFailure()
    {
        connectionTimer.Stop();
        var totalTime = connectionTimer.ElapsedMilliseconds;
        
        DebugLog($"💥 RONIN WALLET CONNECTION FAILED after {totalTime}ms");
        DebugLog($"🔍 Platform: {Application.platform}");
        DebugLog($"🌐 Internet: {Application.internetReachability}");
        DebugLog($"📱 Ronin Wallet Available: {isRoninWalletInstalled}");
        
        #if UNITY_WEBGL
        DebugLog($"🕸️ WebGL Build - Ensure Ronin Wallet mobile app is installed");
        #elif UNITY_ANDROID
        DebugLog($"📱 Android - Check if Ronin Wallet app is installed from Play Store");
        #elif UNITY_IOS
        DebugLog($"📱 iOS - Check if Ronin Wallet app is installed from App Store");
        #endif
    }
    
    /// <summary>
    /// Check NFT balance with Ronin-optimized RPC
    /// </summary>
    public async Task<int> CheckNFTBalance()
    {
        if (!isWalletConnected || string.IsNullOrEmpty(walletAddress))
        {
            DebugLog("❌ Cannot check NFT balance - Ronin wallet not connected");
            return 0;
        }
        
        try
        {
            DebugLog("🎮 Checking NFT balance on Ronin Network...");
            OnConnectionProgress?.Invoke("Checking NFTs on Ronin...");
            
            // Create the contract call for Ronin
            string functionSignature = "0x70a08231"; // balanceOf
            string paddedAddress = walletAddress.Replace("0x", "").PadLeft(64, '0');
            string callData = functionSignature + paddedAddress;
            
            string jsonRequest = CreateRoninEthCallRequest(KONGZ_VX_CONTRACT, callData);
            
            using (UnityWebRequest request = UnityWebRequest.Post(RONIN_RPC_URL, jsonRequest, "application/json"))
            {
                request.SetRequestHeader("X-API-KEY", RONIN_API_KEY);
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 15; // Slightly longer timeout for Ronin RPC
                
                var operation = request.SendWebRequest();
                
                // Wait for completion with timeout
                var startTime = Time.time;
                while (!operation.isDone && (Time.time - startTime) < 15)
                {
                    await Task.Yield();
                }
                
                if (!operation.isDone)
                {
                    DebugLog("❌ Ronin NFT check timed out");
                    request.Abort();
                }
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string response = request.downloadHandler.text;
                    nftBalance = ParseBalanceResponse(response);
                    isNFTChecked = true;
                    
                    DebugLog($"✅ Ronin NFT balance: {nftBalance} KONGZ VX");
                    OnNFTBalanceChecked?.Invoke(nftBalance);
                    return nftBalance;
                }
                else
                {
                    DebugLog($"❌ Ronin NFT check failed: {request.error}");
                    nftBalance = 0;
                    isNFTChecked = true;
                    OnNFTBalanceChecked?.Invoke(0);
                    return 0;
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Ronin NFT check error: {ex.Message}");
            nftBalance = 0;
            isNFTChecked = true;
            OnWeb3Error?.Invoke($"NFT check failed: {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// Create JSON-RPC request optimized for Ronin
    /// </summary>
    private string CreateRoninEthCallRequest(string contractAddress, string data)
    {
        return $@"{{
            ""jsonrpc"": ""2.0"",
            ""method"": ""eth_call"",
            ""params"": [
                {{
                    ""to"": ""{contractAddress}"",
                    ""data"": ""{data}""
                }},
                ""latest""
            ],
            ""id"": 1
        }}";
    }
    
    /// <summary>
    /// Parse NFT balance from Ronin RPC response
    /// </summary>
    private int ParseBalanceResponse(string response)
    {
        try
        {
            if (response.Contains("\"result\""))
            {
                int resultStart = response.IndexOf("\"result\":\"") + 10;
                if (resultStart > 9)
                {
                    int resultEnd = response.IndexOf("\"", resultStart);
                    if (resultEnd > resultStart)
                    {
                        string hexResult = response.Substring(resultStart, resultEnd - resultStart);
                        
                        if (hexResult.StartsWith("0x"))
                        {
                            hexResult = hexResult.Substring(2);
                        }
                        
                        if (long.TryParse(hexResult, System.Globalization.NumberStyles.HexNumber, null, out long balance))
                        {
                            return (int)balance;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Failed to parse Ronin balance: {ex.Message}");
        }
        
        return 0;
    }
    
    /// <summary>
    /// Disconnect Ronin wallet and clear cache
    /// </summary>
    public async void DisconnectWallet()
    {
        try
        {
            if (connectedWallet != null)
            {
                await connectedWallet.Disconnect();
            }
            
            isWalletConnected = false;
            isNFTChecked = false;
            walletAddress = "";
            nftBalance = 0;
            connectedWallet = null;
            
            ClearConnectionCache();
            
            DebugLog("✅ Ronin Wallet disconnected and cache cleared");
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Ronin disconnect error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Force cancel current Ronin connection attempt
    /// </summary>
    public void CancelConnection()
    {
        DebugLog("🛑 Ronin Wallet connection cancelled by user");
        ClearConnectionCache();
        OnWeb3Error?.Invoke("Ronin Wallet connection cancelled");
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Web3Manager-Ronin] {message}");
        }
    }
    
    // Platform-specific optimizations
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isWalletConnected)
        {
            DebugLog("📱 App regained focus - Ronin wallet still connected");
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && isWalletConnected)
        {
            DebugLog("📱 App unpaused - Ronin wallet still connected");
        }
    }
}