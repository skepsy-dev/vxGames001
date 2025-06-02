using UnityEngine;

/// <summary>
/// Simple button script for character portrait prefabs
/// Calls the static CharacterSelector method when clicked
/// </summary>
public class CharacterPortraitButton : MonoBehaviour
{
    /// <summary>
    /// Called by the Button component's onClick event
    /// This method bridges the prefab to the CharacterSelector
    /// </summary>
    public void OnClick()
    {
        // Call the static method in CharacterSelector
        CharacterSelector.OnPortraitClickedStatic();
    }
}