using UnityEngine;

[CreateAssetMenu(fileName = "RoninChainConfig", menuName = "Thirdweb/Ronin Chain Config")]
public class RoninChainConfig : ScriptableObject
{
    [Header("Ronin Network Configuration")]
    public const int CHAIN_ID = 2020; 
    public const string CHAIN_NAME = "Ronin";
    public const string NATIVE_CURRENCY_NAME = "RON";
    public const string NATIVE_CURRENCY_SYMBOL = "RON";
    public const int NATIVE_CURRENCY_DECIMALS = 18;
    
    [Header("RPC Configuration")]
    public string primaryRPC = "https://api.roninchain.com/rpc";
    public string backupRPC = "https://ronin.lgns.net/rpc";
    
    [Header("Contract Addresses")]
    public string kongzVXContract = "0x241a81fc0d6692707dad2b5025a3a7cf2cf25acf";
    
    [Header("API Configuration")]
    public string roninAPIKey = "bS9xVtjS4fIsT10EoqkfHSO6GhwCpzBt";
    public string explorerAPIBase = "https://api-gateway.skymavis.com";
    
    public static RoninChainConfig Instance { get; private set; }
    
    private void OnEnable()
    {
        Instance = this;
    }
    
    /// <summary>
    /// Get the chain ID for Ronin (used in Thirdweb SDK v5)
    /// </summary>
    public static int GetRoninChainId()
    {
        return CHAIN_ID;
    }
    
    /// <summary>
    /// Get RPC URL for Ronin
    /// </summary>
    public static string GetRoninRPC()
    {
        return Instance?.primaryRPC ?? "https://api.roninchain.com/rpc";
    }
    
    /// <summary>
    /// Configure ThirdwebManager for Ronin (SDK v5)
    /// </summary>
    public void ConfigureThirdwebManager()
    {
        Debug.Log("Ronin configuration ready. Chain ID: " + CHAIN_ID);
        Debug.Log("Primary RPC: " + primaryRPC);
        
        // In SDK v5, chain configuration is handled differently
        // The chain will be specified when connecting the wallet
    }
    
    /// <summary>
    /// Validate Ronin wallet address format
    /// </summary>
    public static bool IsValidRoninAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
            return false;
            
        // Check if it's a valid Ethereum-style address (Ronin uses same format)
        if (address.Length != 42)
            return false;
            
        if (!address.StartsWith("0x"))
            return false;
            
        // Check if remaining characters are valid hex
        for (int i = 2; i < address.Length; i++)
        {
            char c = address[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }
        
        return true;
    }
}