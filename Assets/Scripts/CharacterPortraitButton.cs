
using UnityEngine;

public class CharacterPortraitButton : MonoBehaviour
{
    // This method will be called by the Button's onClick event
    public void OnClick()
    {
        Debug.Log("🖱️ CharacterPortraitButton clicked!");
        CharacterSelector.OnPortraitClickedStatic();
    }
}