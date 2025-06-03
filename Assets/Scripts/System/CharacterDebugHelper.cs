using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Debug helper to check character setup
/// Add this to your character portrait prefab temporarily to debug issues
/// </summary>
public class CharacterDebugHelper : MonoBehaviour
{
    private void Start()
    {
        DebugCharacterPortrait();
    }

    private void DebugCharacterPortrait()
    {
        Debug.Log($"🔍 Debugging Character Portrait: {gameObject.name}");
        
        // Check for Image component
        var image = GetComponent<Image>();
        if (image != null)
        {
            Debug.Log($"✅ Image component found. Sprite: {(image.sprite != null ? image.sprite.name : "NULL")}");
            Debug.Log($"   Color: {image.color}, Raycast Target: {image.raycastTarget}");
        }
        else
        {
            Debug.LogError($"❌ No Image component found on {gameObject.name}");
        }
        
        // Check for Button component
        var button = GetComponent<Button>();
        if (button != null)
        {
            Debug.Log($"✅ Button component found. Interactable: {button.interactable}");
            Debug.Log($"   Listeners: {button.onClick.GetPersistentEventCount()}");
        }
        else
        {
            Debug.LogWarning($"⚠️ No Button component found on {gameObject.name}");
        }
        
        // Check for Text component
        var text = GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            Debug.Log($"✅ Text component found: '{text.text}'");
        }
        else
        {
            Debug.LogWarning($"⚠️ No TextMeshProUGUI component found in children");
        }
        
        // Check hierarchy
        Debug.Log($"📋 Children count: {transform.childCount}");
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            Debug.Log($"   Child {i}: {child.name} (Components: {child.GetComponents<Component>().Length})");
        }
    }
}