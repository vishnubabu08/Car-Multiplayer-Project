using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Documents (Drag GameObjects here)")]
    public UIDocument authScreen;
    public UIDocument registerScreen;
    public UIDocument garageScreen;
    public UIDocument trackScreen;
    public UIDocument raceScreen;
    public UIDocument shopScreen;
    public UIDocument rankScreen;

    private void OnEnable()
    {
        Debug.Log("<b>[UI Manager]</b> Starting UI Wiring...");

        // 1. Wire up all the buttons on every screen
        WireUpNavButtons(authScreen, "Auth Screen");
        WireUpNavButtons(registerScreen, "Register Screen");
        WireUpNavButtons(garageScreen, "Garage Screen");
        WireUpNavButtons(trackScreen, "Track Screen");
        WireUpNavButtons(raceScreen, "Race Screen");
        WireUpNavButtons(shopScreen, "Shop Screen");
        WireUpNavButtons(rankScreen, "Rank Screen");

        // 2. Open the Garage first 
        OpenScreen(garageScreen);
    }

    private void WireUpNavButtons(UIDocument uiDoc, string screenName)
    {
        if (uiDoc == null || uiDoc.rootVisualElement == null) return;

        VisualElement root = uiDoc.rootVisualElement;
        int foundButtons = 0;

        // --- HELPER FUNCTION TO WIRE CLICKS ---
        void ConnectClick(VisualElement element, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string btnText = text.ToUpper();

            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                Debug.Log($"<b>[Button Clicked]</b> Navigating to: {btnText}");

                if (btnText.Contains("GARAGE")) OpenScreen(garageScreen);
                else if (btnText.Contains("TRACK")) OpenScreen(trackScreen);
                else if (btnText.Contains("RACE")) OpenScreen(raceScreen);
                else if (btnText.Contains("SHOP")) OpenScreen(shopScreen);
                // Catch both "RANK" and "LEADERBOARD" text
                else if (btnText.Contains("RANK") || btnText.Contains("LEADERBOARD")) OpenScreen(rankScreen);
            });
            foundButtons++;
        }

        // ==========================================
        // 1. BOTTOM NAVIGATION BAR (nav-item)
        // ==========================================
        root.Query<VisualElement>(className: "nav-item").ForEach(navItem =>
        {
            Label label = navItem.Q<Label>();
            if (label != null) ConnectClick(navItem, label.text);
        });

        // ==========================================
        // 2. TOP NAVIGATION BAR (nav-link)
        // ==========================================
        root.Query<Label>(className: "nav-link").ForEach(navLink =>
        {
            ConnectClick(navLink, navLink.text);
        });

        Debug.Log($"<b>[UI Manager]</b> Found and wired {foundButtons} Nav Buttons on {screenName}");

        // ==========================================
        // 3. SPECIFIC BUTTON LOGIC
        // ==========================================
        Button btnAuth = root.Q<Button>("btn-authenticate");
        if (btnAuth != null) btnAuth.RegisterCallback<PointerUpEvent>(evt => OpenScreen(garageScreen));

        Button btnRegister = root.Q<Button>("btn-register");
        if (btnRegister != null) btnRegister.RegisterCallback<PointerUpEvent>(evt => OpenScreen(garageScreen));

        Button btnStartRace = root.Q<Button>(className: "fab-start-race");
        if (btnStartRace != null) btnStartRace.RegisterCallback<PointerUpEvent>(evt => OpenScreen(raceScreen));
    }

    public void OpenScreen(UIDocument targetScreen)
    {
        HideScreen(authScreen);
        HideScreen(registerScreen);
        HideScreen(garageScreen);
        HideScreen(trackScreen);
        HideScreen(raceScreen);
        HideScreen(shopScreen);
        HideScreen(rankScreen);

        if (targetScreen != null && targetScreen.rootVisualElement != null)
        {
            targetScreen.rootVisualElement.style.display = DisplayStyle.Flex;
            targetScreen.rootVisualElement.BringToFront();
        }
    }

    private void HideScreen(UIDocument uiDoc)
    {
        if (uiDoc != null && uiDoc.rootVisualElement != null)
        {
            uiDoc.rootVisualElement.style.display = DisplayStyle.None;
        }
    }
}