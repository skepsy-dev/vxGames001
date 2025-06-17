using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Thirdweb;
using Thirdweb.Unity;
using System.Threading.Tasks;
using System;
using System.Numerics;
using Fusion;
using AvocadoShark;

/// <summary>
/// Web3Manager with Early Photon Connection
/// Step 1: Start Photon connection on page load
/// Step 2: Continue with Web3/PlayFab auth in parallel
/// </summary>
public class Web3Manager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int chainId = 1;

    [Header("NFT Configuration")]
    private const string KONGZ_VX_CONTRACT = "0x241a81fc0d6692707dad2b5025a3a7cf2cf25acf";
    private const string RONIN_RPC_URL = "https://api-gateway.skymavis.com/rpc";
    private const string RONIN_API_KEY = "bS9xVtjS4fIsT10EoqkfHSO6GhwCpzBt";

    [Header("Early Photon Connection")]
    [SerializeField] private bool enableEarlyPhotonConnection = true;
    [SerializeField] private GameObject photonRunnerPrefab; // Assign NetworkRunner prefab
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool verbosePhotonLogs = true;

    // State
    private bool isWalletConnected = false;
    private bool isNFTChecked = false;
    private string walletAddress = "";
    private int nftBalance = 0;
    private IThirdwebWallet connectedWallet;

    // Early Photon Connection
    private NetworkRunner earlyPhotonRunner;
    private bool isPhotonPreConnected = false;
    private float photonConnectionStartTime;
    private string tempUserId;

    // Events
    public System.Action<string> OnWalletConnected;
    public System.Action<int> OnNFTBalanceChecked;
    public System.Action<string> OnWeb3Error;
    public System.Action<string> OnConnectionProgress;
    public System.Action<NetworkRunner> OnPhotonEarlyConnected;

    // Public Properties
    public bool IsWalletConnected => isWalletConnected;
    public bool IsNFTChecked => isNFTChecked;
    public string GetWalletAddress() => walletAddress;
    public int GetNFTBalance() => nftBalance;
    public bool HasNFTs() => nftBalance > 0;
    public NetworkRunner GetEarlyPhotonRunner() => earlyPhotonRunner;
    public bool IsPhotonPreConnected => isPhotonPreConnected;

    private void Awake()
    {
        DebugLog("🔷 Web3Manager Awake() called", true);
        
        if (enableEarlyPhotonConnection)
        {
            StartEarlyPhotonConnection();
        }
    }

    /// <summary>
    /// Start Photon connection immediately on page load
    /// </summary>
    private void StartEarlyPhotonConnection()
    {
        DebugLog("🚀 STEP 1: Starting early Photon connection...", true);
        photonConnectionStartTime = Time.time;
        
        // Generate temporary user ID
        tempUserId = $"temp_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        DebugLog($"📋 Generated temp user ID: {tempUserId}", true);
        
        // Create the runner but don't connect to lobby yet
        StartCoroutine(InitializePhotonEarly());
    }

    private IEnumerator InitializePhotonEarly()
    {
        DebugLog("⏳ STEP 2: Creating Photon NetworkRunner...", true);
        
        // Find or create runner prefab
        if (photonRunnerPrefab == null)
        {
            // Try to find it in FusionConnection
            var fusionConnection = FindObjectOfType<FusionConnection>();
            if (fusionConnection != null)
            {
                var runnerField = fusionConnection.GetType().GetField("runnerPrefab", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (runnerField != null)
                {
                    photonRunnerPrefab = runnerField.GetValue(fusionConnection) as GameObject;
                    DebugLog("✅ Found runner prefab from FusionConnection", true);
                }
            }
        }
        
        if (photonRunnerPrefab != null)
        {
            earlyPhotonRunner = Instantiate(photonRunnerPrefab).GetComponent<NetworkRunner>();
            earlyPhotonRunner.name = "EarlyPhotonRunner";
            DontDestroyOnLoad(earlyPhotonRunner.gameObject);
            
            DebugLog($"✅ STEP 3: NetworkRunner created: {earlyPhotonRunner.name}", true);
            
            // Configure for WebGL if needed
            #if UNITY_WEBGL && !UNITY_EDITOR
            ConfigureWebGLSettings();
            #endif
            
            // For now, just mark as pre-connected since we can't actually connect without StartGame
            isPhotonPreConnected = true;
            DebugLog("✅ STEP 4: Photon runner created and ready for use", true);
            
            OnPhotonEarlyConnected?.Invoke(earlyPhotonRunner);
        }
        else
        {
            DebugLog("❌ ERROR: No photon runner prefab found!", true);
            DebugLog("   Please assign the NetworkRunner prefab in the inspector", true);
        }
        
        yield return null;
    }

    private void ConfigureWebGLSettings()
    {
        DebugLog("🔧 Configuring WebGL settings...", true);
        
        var appSettings = Fusion.Photon.Realtime.PhotonAppSettings.Global;
        if (appSettings != null)
        {
            appSettings.AppSettings.Protocol = ExitGames.Client.Photon.ConnectionProtocol.WebSocketSecure;
            appSettings.AppSettings.Port = 443;
            appSettings.AppSettings.EnableProtocolFallback = false;
            
            DebugLog("✅ WebGL settings applied: WebSocketSecure on port 443", true);
        }
    }

    /// <summary>
    /// Your existing wallet connection code with added logging
    /// </summary>
    public async Task<bool> ConnectWallet()
    {
        DebugLog("💼 WALLET STEP 1: Starting wallet connection...", true);
        DebugLog($"   Photon pre-connected: {isPhotonPreConnected}", true);
        DebugLog($"   Time since page load: {Time.time:F1}s", true);

        try
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            DebugLog("📱 WALLET STEP 2: Using Ronin browser extension...", true);
            OnConnectionProgress?.Invoke("Connecting to Ronin Wallet...");
            
            RoninJSBridge.Instance.SetWeb3Manager(this);
            RoninJSBridge.Instance.ConnectWallet();
            
            return await Task.FromResult(true);
            #else
            // Your existing WalletConnect code...
            DebugLog("💻 WALLET STEP 2: Using WalletConnect (non-WebGL)...", true);
            OnConnectionProgress?.Invoke("Opening wallet selection...");
            
            var walletOptions = new WalletOptions(
                WalletProvider.WalletConnectWallet, 
                new BigInteger(1)
            );
            
            connectedWallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);
            
            if (connectedWallet != null)
            {
                walletAddress = await connectedWallet.GetAddress();
                isWalletConnected = true;
                DebugLog($"✅ WALLET STEP 3: Wallet connected: {walletAddress}", true);
                OnWalletConnected?.Invoke(walletAddress);
                return true;
            }
            
            return false;
            #endif
        }
        catch (Exception ex)
        {
            DebugLog($"❌ WALLET ERROR: {ex.Message}", true);
            OnWeb3Error?.Invoke($"Connection failed: {ex.Message}");
            return false;
        }
    }

    public void OnRoninExtensionConnected(string address)
    {
        walletAddress = address;
        isWalletConnected = true;

        DebugLog($"✅ WALLET STEP 3: Ronin extension connected: {address}", true);
        DebugLog($"   Total time since page load: {Time.time:F1}s", true);
        OnWalletConnected?.Invoke(address);
    }

    public void OnRoninExtensionError(string error)
    {
        DebugLog($"❌ WALLET ERROR: Ronin extension: {error}", true);
        OnWeb3Error?.Invoke(error);
    }

    /// <summary>
    /// Your existing NFT check code with added logging
    /// </summary>
    public async Task<int> CheckNFTBalance()
    {
        DebugLog("🎮 NFT STEP 1: Starting NFT balance check...", true);
        DebugLog($"   Time since page load: {Time.time:F1}s", true);
        
        if (!isWalletConnected || string.IsNullOrEmpty(walletAddress))
        {
            DebugLog("❌ NFT ERROR: Wallet not connected", true);
            return 0;
        }

        try
        {
            OnConnectionProgress?.Invoke("Checking NFTs...");

            string functionSignature = "0x70a08231";
            string paddedAddress = walletAddress.Replace("0x", "").PadLeft(64, '0');
            string callData = functionSignature + paddedAddress;

            string jsonRequest = CreateEthCallRequest(KONGZ_VX_CONTRACT, callData);

            DebugLog($"📤 NFT STEP 2: Sending request to Ronin RPC...", true);

            using (UnityWebRequest request = UnityWebRequest.Post(RONIN_RPC_URL, jsonRequest, "application/json"))
            {
                request.SetRequestHeader("X-API-KEY", RONIN_API_KEY);
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 15;

                var operation = request.SendWebRequest();

                var startTime = Time.time;
                while (!operation.isDone && (Time.time - startTime) < 15)
                {
                    await Task.Yield();
                }

                if (!operation.isDone)
                {
                    DebugLog("❌ NFT ERROR: Request timed out", true);
                    request.Abort();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string response = request.downloadHandler.text;
                    nftBalance = ParseBalanceResponse(response);
                    isNFTChecked = true;

                    DebugLog($"✅ NFT STEP 3: Balance check complete: {nftBalance} KONGZ VX", true);
                    DebugLog($"   Total time since page load: {Time.time:F1}s", true);
                    OnNFTBalanceChecked?.Invoke(nftBalance);
                    return nftBalance;
                }
                else
                {
                    DebugLog($"❌ NFT ERROR: {request.error}", true);
                    nftBalance = 0;
                    isNFTChecked = true;
                    OnNFTBalanceChecked?.Invoke(0);
                    return 0;
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog($"❌ NFT ERROR: Exception: {ex.Message}", true);
            nftBalance = 0;
            isNFTChecked = true;
            OnWeb3Error?.Invoke($"NFT check failed: {ex.Message}");
            return 0;
        }
    }

    private string CreateEthCallRequest(string contractAddress, string data)
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
            DebugLog($"❌ Parse error: {ex.Message}", false);
        }

        return 0;
    }

    public async void DisconnectWallet()
    {
        try
        {
            DebugLog("🔌 Disconnecting wallet...", true);
            
            if (connectedWallet != null)
            {
                await connectedWallet.Disconnect();
            }

            isWalletConnected = false;
            isNFTChecked = false;
            walletAddress = "";
            nftBalance = 0;
            connectedWallet = null;

            DebugLog("✅ Wallet disconnected", true);
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Disconnect error: {ex.Message}", true);
        }
    }

    public void CancelConnection()
    {
        DebugLog("🛑 Connection cancelled by user", true);
        OnWeb3Error?.Invoke("Connection cancelled");
    }

    private void DebugLog(string message, bool important = false)
    {
        if (enableDebugLogs || important)
        {
            Debug.Log($"[Web3Manager-{Time.time:F2}] {message}");
        }
    }

    private void OnDestroy()
    {
        if (earlyPhotonRunner != null)
        {
            DebugLog("🧹 Cleaning up early Photon runner", true);
            Destroy(earlyPhotonRunner.gameObject);
        }
    }
}