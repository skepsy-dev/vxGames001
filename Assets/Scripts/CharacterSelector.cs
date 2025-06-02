// FILE 1: CharacterSelector.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using AvocadoShark;
using Fusion;
using UnityEngine.Events;

public class CharacterSelector : MonoBehaviour
{
    [Header("References")]
    public CharacterSO characterScriptableObject; // Drag your CharacterSO here
    public Transform contentContainer; // Drag the content container of your scroll view
    public GameObject characterPortraitPrefab; // Drag your CharacterPortraitPanel prefab
    public GameObject selectionPanel; // The entire character selection panel

    [Header("Settings")]
    public bool closeAfterSelection = true;

    // Static reference for prefabs to access
    public static CharacterSelector Instance;

    // Reference to InGame_Manager to update PFP
    private InGame_Manager inGameManager;

    private void Awake()
    {
        // Set the static instance so prefabs can access this
        Instance = this;
    }

    private void Start()
    {
        // Find InGame_Manager
        inGameManager = FindFirstObjectByType<InGame_Manager>();

        // Initially hide the selection panel
        selectionPanel.SetActive(false);

        // Populate the character list
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
        Debug.Log($"🔍 PopulateCharacterList called. CharacterSO is null: {characterScriptableObject == null}");

        if (characterScriptableObject != null)
        {
            Debug.Log($"📋 Characters count: {characterScriptableObject.characters.Count}");
        }

        // Clear existing entries
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }

        // Create entries for each character
        for (int i = 0; i < characterScriptableObject.characters.Count; i++)
        {
            var character = characterScriptableObject.characters[i];
            GameObject portraitObject = Instantiate(characterPortraitPrefab, contentContainer);

            // Set up the portrait
            var portraitImage = portraitObject.GetComponent<Image>();
            var nameText = portraitObject.GetComponentInChildren<TextMeshProUGUI>();

            // Assign character data
            if (portraitImage != null && character.characterSprite != null)
            {
                portraitImage.sprite = character.characterSprite;
            }

            if (nameText != null)
            {
                nameText.text = character.characterName;
            }
        }
    }

    // This method will be called from the button's onClick event
    public void OnPortraitClicked()
    {
        // Get which button was clicked
        var clickedButton = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        if (clickedButton != null)
        {
            // Get the index by finding the button's position in the content container
            int index = clickedButton.transform.GetSiblingIndex();
            Debug.Log($"🖱️ Portrait clicked at index: {index}");
            SelectCharacter(index);
        }
    }

    // Static method that prefabs can call
    public static void OnPortraitClickedStatic()
    {
        if (Instance != null)
        {
            Instance.OnPortraitClicked();
        }
        else
        {
            Debug.LogError("❌ CharacterSelector Instance is null! Make sure CharacterSelector is in the scene.");
        }
    }

    public void SelectCharacter(int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= characterScriptableObject.characters.Count)
            return;

        Debug.Log($"Selected character: {characterScriptableObject.characters[characterIndex].characterName}");

        // Save selection to PlayerPrefs
        PlayerPrefs.SetString("SpawnedCharacterName", characterScriptableObject.characters[characterIndex].characterName);
        PlayerPrefs.SetInt("SpawnedCharacterIndex", characterIndex);

        // Update PFP in InGame_Manager
        if (inGameManager != null)
        {
            inGameManager.RefreshCharacterPFP();
        }

        // Request character change via network
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null)
        {
            var playerObj = runner.GetPlayerObject(runner.LocalPlayer);
            if (playerObj != null)
            {
                var playerStats = playerObj.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    // Call RPC to change character
                    playerStats.RPC_ChangeCharacter(characterIndex);
                }
            }
        }

        // Close panel if configured to do so
        if (closeAfterSelection)
        {
            CloseSelectionPanel();
        }
    }
}
