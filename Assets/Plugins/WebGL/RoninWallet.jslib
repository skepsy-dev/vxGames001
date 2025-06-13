mergeInto(LibraryManager.library, {
    DetectRoninWallet: function() {
        // Check if Ronin wallet is available
        if (typeof window.ronin !== 'undefined' && window.ronin.provider) {
            return true;
        } else if (typeof window.ethereum !== 'undefined' && window.ethereum.isRonin) {
            return true;
        }
        return false;
    },

    ConnectRoninWallet: function() {
        // Get the provider
        var provider = window.ronin ? window.ronin.provider : 
                      (window.ethereum && window.ethereum.isRonin ? window.ethereum : null);
        
        if (!provider) {
            console.error("Ronin wallet not detected!");
            SendMessage('RoninJSBridge', 'OnWalletConnectionFailed', 'No Ronin wallet detected');
            return;
        }

        // Request accounts
        provider.request({ method: 'eth_requestAccounts' })
            .then(function(accounts) {
                if (accounts.length > 0) {
                    var address = accounts[0];
                    console.log("Connected to Ronin wallet:", address);
                    SendMessage('RoninJSBridge', 'OnWalletConnectionSuccess', address);
                } else {
                    SendMessage('RoninJSBridge', 'OnWalletConnectionFailed', 'No accounts returned');
                }
            })
            .catch(function(error) {
                console.error("Connection error:", error);
                if (error.code === 4001) {
                    SendMessage('RoninJSBridge', 'OnWalletConnectionRejected');
                } else {
                    SendMessage('RoninJSBridge', 'OnWalletConnectionFailed', error.message);
                }
            });
    },

    GetRoninAddress: function() {
        var provider = window.ronin ? window.ronin.provider : 
                      (window.ethereum && window.ethereum.isRonin ? window.ethereum : null);
        
        if (!provider) return null;
        
        // This would need to be async in real implementation
        return provider.selectedAddress || '';
    }
});