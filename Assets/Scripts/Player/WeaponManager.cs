using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapon GameObjects (Vapnen i spelarens hand)")]
    // Dra ditt GRÖNA PushGun_Weapon-objekt hit
    public GameObject pushGunObject;
    // Dra ditt LILA PullGun_Weapon-objekt hit
    public GameObject pullGunObject;

    [Header("Weapon Scripts")]
    // Dra ditt GRÖNA PushGun_Weapon-objekt hit IGEN (Unity hittar skriptet)
    public MonoBehaviour pushGunScript;
    // Dra ditt LILA PullGun_Weapon-objekt hit IGEN
    public MonoBehaviour pullGunScript;

    // Detta är en enkel variabel för att hålla koll på vilket vapen som ska vara aktivt
    private int activeWeaponIndex = 1; // 0 = Push, 1 = Pull (Startar på Pull!)

    void Start()
    {
        // Starta spelet med att aktivera PullGun (index 1) eftersom du vill det!
        SelectWeapon(activeWeaponIndex);
    }

    void Update()
    {
        // Säkerhetskoll så att tangentbordet är anslutet
        if (Keyboard.current == null) return;

        // Växla med sifferknapparna 1 och 2 (Det NYA systemets sätt)
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (activeWeaponIndex != 0) // Bara växla om vi inte redan håller i det
            {
                SelectWeapon(0); // Gå till Push (Grön)
            }
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (activeWeaponIndex != 1)
            {
                SelectWeapon(1); // Gå till Pull (Lila)
            }
        }

        // EXTRA: Växla med scrollhjulet på musen (Superenkelt med nya systemet!)
        if (Mouse.current != null)
        {
            Vector2 scrollDelta = Mouse.current.scroll.ReadValue();
            if (scrollDelta.y > 0f) // Scrollar upp
            {
                SelectWeapon(activeWeaponIndex == 0 ? 1 : 0);
            }
            else if (scrollDelta.y < 0f) // Scrollar ner
            {
                SelectWeapon(activeWeaponIndex == 1 ? 0 : 1);
            }
        }
    }

    // Denna metod hanterar hela växlingen och dörrvakten
    void SelectWeapon(int index)
    {
        activeWeaponIndex = index;
        
        if (activeWeaponIndex == 0)
        {
            // --- AKTIVERA GRÖN PUSHGUN ---
            if (pushGunObject != null) pushGunObject.SetActive(true);
            if (pushGunScript != null) pushGunScript.enabled = true;

            // --- INAKTIVERA LILA PULLGUN ---
            if (pullGunObject != null) pullGunObject.SetActive(false);
            if (pullGunScript != null) pullGunScript.enabled = false;
            
            Debug.Log("✅ Vapen växlat till: PUSH GUN (GRÖN)");
        }
        else if (activeWeaponIndex == 1)
        {
            // --- AKTIVERA LILA PULLGUN ---
            if (pullGunObject != null) pullGunObject.SetActive(true);
            if (pullGunScript != null) pullGunScript.enabled = true;

            // --- INAKTIVERA GRÖN PUSHGUN ---
            if (pushGunObject != null) pushGunObject.SetActive(false);
            if (pushGunScript != null) pushGunScript.enabled = false;

            Debug.Log("✅ Vapen växlat till: PULL GUN (LILA)");
        }
    }
}