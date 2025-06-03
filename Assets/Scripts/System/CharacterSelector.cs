using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using AvocadoShark;
using Fusion;
using StarterAssets;

public class CharacterSelector : MonoBehaviour
{
    [Header("References")]
    public CharacterSO characterScriptableObject;
    public Transform contentContainer;
    public GameObject characterPortraitPrefab;
    public GameObject selectionPanel;

    [Header("Settings")]
    public bool closeAfterSelection = true;

    public static CharacterSelector Instance;
    private InGame_Manager inGameManager;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        inGameManager = FindFirstObjectByType<InGame_Manager>();
        selectionPanel.SetActive(false);
        PopulateCharacterList();
    }

    public void OpenSelectionPanel()
    {
        selectionPanel.SetActive(true);
    }

    public void CloseSelectionPanel()
    {
        selectionPanel.SetActive(false);
    }

    private void PopulateCharacterList()
    {
        Debug.Log($"🎮 Starting PopulateCharacterList...");
        Debug.Log($"📋 Characters count: {characterScriptableObject.characters.Count}");

        // Clear existing entries
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }

        // Create character portraits
        for (int i = 0; i < characterScriptableObject.characters.Count; i++)
        {
            var character = characterScriptableObject.characters[i];
            GameObject portraitObject = Instantiate(characterPortraitPrefab, contentContainer);

            Debug.Log($"📦 Created portrait for: {character.characterName}");

            // Find Image component (checking multiple locations)
            Image portraitImage = null;

            // Method 1: Direct on root
            portraitImage = portraitObject.GetComponent<Image>();
            if (portraitImage != null)
            {
                Debug.Log("✅ Found Image on root GameObject");
            }
            else
            {
                // Method 2: Look for child named "Image"
                Transform imageChild = portraitObject.transform.Find("Image");
                if (imageChild != null)
                {
                    portraitImage = imageChild.GetComponent<Image>();
                    if (portraitImage != null)
                    {
                        Debug.Log("✅ Found Image on child 'Image'");
                    }
                }

                // Method 3: Search all children
                if (portraitImage == null)
                {
                    portraitImage = portraitObject.GetComponentInChildren<Image>();
                    if (portraitImage != null)
                    {
                        Debug.Log("✅ Found Image in children");
                    }
                }
            }

            // Assign the sprite
            if (portraitImage != null && character.characterSprite != null)
            {
                portraitImage.sprite = character.characterSprite;
                portraitImage.color = Color.white;
                portraitImage.preserveAspect = true;
                Debug.Log($"✅ SPRITE ASSIGNED: {character.characterName} -> {character.characterSprite.name}");
            }

            // Set character name
            var nameText = portraitObject.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = character.characterName;
            }

            // Setup button click
            var button = portraitObject.GetComponent<Button>();
            if (button != null)
            {
                int characterIndex = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectCharacter(characterIndex));
            }
        }
    }

    public void SelectCharacter(int characterIndex)
    {
        var selectedCharacter = characterScriptableObject.characters[characterIndex];
        Debug.Log($"🎭 Selected: {selectedCharacter.characterName} (index: {characterIndex})");

        // Save selection for UI (this updates the PFP immediately)
        PlayerPrefs.SetString("SpawnedCharacterName", selectedCharacter.characterName);
        PlayerPrefs.SetInt("SpawnedCharacterIndex", characterIndex);
        PlayerPrefs.Save();

        // Update PFP immediately
        if (inGameManager != null)
        {
            inGameManager.RefreshCharacterPFP();
        }

        // Request character change
        RequestCharacterChange(characterIndex);

        if (closeAfterSelection)
        {
            CloseSelectionPanel();
        }
    }

    /// <summary>
    /// Request a character change by updating CharacterSO and requesting respawn
    /// </summary>
    private void RequestCharacterChange(int characterIndex)
    {
        // Find the NetworkRunner
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner == null || !runner.IsConnectedToServer)
        {
            Debug.LogError("❌ No active NetworkRunner found!");
            return;
        }

        // Find FusionConnection (try multiple ways)
        var fusionConnection = FusionConnection.Instance;
        if (fusionConnection == null)
        {
            // Try finding it by type
            fusionConnection = FindFirstObjectByType<FusionConnection>();
            if (fusionConnection == null)
            {
                Debug.LogError("❌ FusionConnection not found! Cannot change character.");
                return;
            }
            Debug.Log("⚠️ FusionConnection.Instance was null, found it via FindFirstObjectByType");
        }

        var localPlayerObject = runner.GetPlayerObject(runner.LocalPlayer);
        if (localPlayerObject == null)
        {
            Debug.LogError("❌ No local player object found!");
            return;
        }

        Debug.Log($"🔄 Requesting character change to index: {characterIndex}");
        
        // Update the character selection in CharacterSO
        if (characterIndex >= 0 && characterIndex < characterScriptableObject.characters.Count)
        {
            var selectedCharacter = characterScriptableObject.characters[characterIndex];
            
            // Save the selection using CharacterSO's method
            characterScriptableObject.SaveSelectedCharacter(selectedCharacter);
            
            // Also update the PlayerPrefs that FusionConnection uses
            PlayerPrefs.SetInt("Character Selection", characterIndex);
            PlayerPrefs.Save();
            
            Debug.Log($"✅ Character selection saved: {selectedCharacter.characterName} at index {characterIndex}");
            
            // Request respawn with new character
            RequestCharacterRespawn(runner, localPlayerObject);
        }
        else
        {
            Debug.LogError($"❌ Invalid character index: {characterIndex}");
        }
    }
    
    /// <summary>
    /// Request a respawn with the newly selected character
    /// </summary>
    private void RequestCharacterRespawn(NetworkRunner runner, NetworkObject localPlayerObject)
    {
        Debug.Log("🔄 Requesting character respawn...");
        
        // Use the new public method in FusionConnection
        if (FusionConnection.Instance != null)
        {
            // Call the new respawn method
            FusionConnection.Instance.RespawnLocalPlayerWithNewCharacter();
            Debug.Log("✅ Character respawn requested!");
        }
        else
        {
            Debug.LogError("❌ FusionConnection.Instance is null!");
        }
    }

    // Keep for compatibility
    public static void OnPortraitClickedStatic()
    {
        Debug.LogWarning("Legacy method called");
    }
}