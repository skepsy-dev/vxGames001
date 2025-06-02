using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Thirdweb;
using Thirdweb.Unity;

/// <summary>
/// Web3Manager - Handles Wallet Connection and NFT Verification
/// Extracted from NetworkManager for better code organization
/// </summary>
public class Web3Manager : MonoBehaviour
{
    [Header("Wallet Configuration")]
    [SerializeField] private int roninChainId = 2020;
    
    [Header("NFT Configuration")]
    private const string KONGZ_VX_CONTRACT = "0x241a81fc0d6692707dad2b5025a3a7cf2cf25acf";
    private const string RONIN_RPC_URL = "https://api-gateway.skymavis.com/rpc";
    private const string RONIN_API_KEY = "bS9xVtjS4fIsT10EoqkfHSO6GhwCpzBt";
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // State
    private bool isWalletConnected = false;
    private bool isNFTChecked = false;
    private string walletAddress = "";
    private int nftBalance = 0;
    private IThirdwebWallet connectedWallet;
    
    // Events for NetworkManager to listen to
    public System.Action<string> OnWalletConnected;
    public System.Action<int> OnNFTBalanceChecked;
    public System.Action<string> OnWeb3Error;
    
    // Public Properties - API Interface
    public bool IsWalletConnected => isWalletConnected;
    public bool IsNFTChecked => isNFTChecked;
    public string GetWalletAddress() => walletAddress;
    public int GetNFTBalance() => nftBalance;
    public bool HasNFTs() => nftBalance > 0;
    
    /// <summary>
    /// Connect wallet using WalletConnect
    /// </summary>
    public async System.Threading.Tasks.Task<bool> ConnectWallet()
    {
        try
        {
            DebugLog("Attempting wallet connection...");
            
            // Try WalletConnect first (works best for mobile wallets)
            var walletOptions = new WalletOptions(WalletProvider.WalletConnectWallet, roninChainId);
            connectedWallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);
            
            if (connectedWallet != null)
            {
                walletAddress = await connectedWallet.GetAddress();
                isWalletConnected = true;
                
                DebugLog($"✅ Wallet connected: {walletAddress}");
                OnWalletConnected?.Invoke(walletAddress);
                return true;
            }
            else
            {
                throw new System.Exception("Wallet connection returned null");
            }
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ Wallet connection failed: {ex.Message}");
            OnWeb3Error?.Invoke($"Wallet connection failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Check NFT balance using direct contract call
    /// </summary>
    public async System.Threading.Tasks.Task<int> CheckNFTBalance()
    {
        if (!isWalletConnected || string.IsNullOrEmpty(walletAddress))
        {
            DebugLog("❌ Cannot check NFT balance - wallet not connected");
            return 0;
        }
        
        try
        {
            DebugLog("Checking NFT balance...");
            
            // Create the contract call
            string functionSignature = "0x70a08231"; // balanceOf
            string paddedAddress = walletAddress.Replace("0x", "").PadLeft(64, '0');
            string callData = functionSignature + paddedAddress;
            
            string jsonRequest = CreateEthCallRequest(KONGZ_VX_CONTRACT, callData);
            
            using (UnityWebRequest request = UnityWebRequest.Post(RONIN_RPC_URL, jsonRequest, "application/json"))
            {
                request.SetRequestHeader("X-API-KEY", RONIN_API_KEY);
                request.SetRequestHeader("Content-Type", "application/json");
                
                var operation = request.SendWebRequest();
                
                // Wait for completion
                while (!operation.isDone)
                {
                    await System.Threading.Tasks.Task.Yield();
                }
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string response = request.downloadHandler.text;
                    nftBalance = ParseBalanceResponse(response);
                    isNFTChecked = true;
                    
                    DebugLog($"✅ NFT balance: {nftBalance} KONGZ VX");
                    OnNFTBalanceChecked?.Invoke(nftBalance);
                    return nftBalance;
                }
                else
                {
                    DebugLog($"❌ NFT check failed: {request.error}");
                    nftBalance = 0;
                    isNFTChecked = true;
                    OnNFTBalanceChecked?.Invoke(0);
                    return 0;
                }
            }
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ NFT check error: {ex.Message}");
            nftBalance = 0;
            isNFTChecked = true;
            OnWeb3Error?.Invoke($"NFT check failed: {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// Create JSON-RPC request for NFT balance check
    /// </summary>
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
    
    /// <summary>
    /// Parse NFT balance from RPC response
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
        catch (System.Exception ex)
        {
            DebugLog($"❌ Failed to parse balance: {ex.Message}");
        }
        
        return 0;
    }
    
    /// <summary>
    /// Disconnect wallet
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
            
            DebugLog("✅ Wallet disconnected");
        }
        catch (System.Exception ex)
        {
            DebugLog($"❌ Disconnect error: {ex.Message}");
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Web3Manager] {message}");
        }
    }
}