#if CMPSETUP_COMPLETE
using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/Character Selection")]
public class CharacterSO : ScriptableObject
{
    public List<PlayerSelectionDetails> characters = new List<PlayerSelectionDetails>();
    private const string Key = "Character Selection";

    public int GetSelectedCharacterIndex => PlayerPrefs.GetInt(Key, 0);
    public void SaveSelectedCharacter(PlayerSelectionDetails character)
    {
        var result = characters.IndexOf(character);
        if (result < 0)
            return;
        PlayerPrefs.SetInt(Key, result);
    }

    public PlayerSelectionDetails GetSelectedCharacter()
    {
        return characters[PlayerPrefs.GetInt(Key, 0)];
    }

    /// <summary>
    /// Get a random character from all available characters
    /// </summary>
    public PlayerSelectionDetails GetRandomCharacter()
    {
        if (characters == null || characters.Count == 0)
        {
            Debug.LogError("No characters available for random selection!");
            return characters[0]; // Fallback to first character
        }

        // Debug the characters list
        Debug.Log($"Characters list has {characters.Count} entries");
        for (int i = 0; i < characters.Count; i++)
        {
            Debug.Log($"Character {i}: {characters[i].characterName}");
        }

        int randomIndex = UnityEngine.Random.Range(0, characters.Count);
        Debug.Log($"🎲 RANDOM SELECTION: index={randomIndex}, name={characters[randomIndex].characterName}");
        return characters[randomIndex];
    }
}

[Serializable]
public class PlayerSelectionDetails
{
    public string characterName;
    public ThirdPersonController character;
    public GameObject displayModel;

    [Header("Character UI")]
    public Sprite characterSprite; // ADD this line - for PFP images
}
#endif