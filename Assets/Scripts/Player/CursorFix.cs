using UnityEngine;

public class CursorFix : MonoBehaviour
{
    void Update()
    {
        // Force cursor to always be visible and unlocked
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}