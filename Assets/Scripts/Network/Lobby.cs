using DG.Tweening;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Lobby : MonoBehaviour
{
    [Header("Screens")]
    [SerializeField] GameObject title;
    [SerializeField] RectTransform titleImage;
    [SerializeField] GameObject firstScreen;
    [SerializeField] RectTransform termsButton;
    [SerializeField] RectTransform privacyButton;
    [SerializeField] RectTransform startLabel;
    [SerializeField] Button pressToStart;
    [SerializeField] GameObject signupPanel;
    [SerializeField] GameObject loginPanel;
    [SerializeField] GameObject lobbyScreen;
    [SerializeField] Slider loadingBar;
    [SerializeField] TMP_Text loadingText;

    [Header("Signup")]
    [SerializeField] TMP_InputField signupEmail;
    [SerializeField] TMP_InputField signupUsername;
    [SerializeField] TMP_InputField signupPassword;
    [SerializeField] Button signupButton;
    [SerializeField] Button signupClose;
    [SerializeField] Button showLoginButton;

    [Header("Login")]
    [SerializeField] TMP_InputField loginEmail;
    [SerializeField] TMP_InputField loginPassword;
    [SerializeField] Toggle rememberMe;
    [SerializeField] Button loginButton;
    [SerializeField] Button loginClose;
    [SerializeField] Button showSignupButton;

    [Header("Notification")]
    [SerializeField] GameObject notification;
    [SerializeField] TMP_Text notificationTitle;
    [SerializeField] TMP_Text notificationInfo;
    [SerializeField] Button notificationClose;
    [SerializeField] Button notificationOk;

    [Header("Animation")]
    [SerializeField] float introTitleDuration = 1.2f;
    [SerializeField] float introButtonDuration = 1f;
    [SerializeField] float introPause = 0.5f;
    [SerializeField] float buttonDuration = 0.5f;
    [SerializeField] float loadingDuration = 0.6f;

    Sequence sequence;
    Tween startPulse;
    bool busy;
    enum SavedLoginState { NoCredentials, SigningIn, SignedIn, Failed }
    SavedLoginState savedLoginState;
    string savedLoginError;
    bool tapPending;
    bool introTransition;

    void Start()
    {
        signupClose.onClick.AddListener(ReturnToStart);
        loginClose.onClick.AddListener(ReturnToStart);
        signupButton.onClick.AddListener(SignUp);
        loginButton.onClick.AddListener(LogIn);
        showLoginButton.onClick.AddListener(ShowLogin);
        showSignupButton.onClick.AddListener(ShowSignup);
        notificationClose.onClick.AddListener(CloseNotification);
        notificationOk.onClick.AddListener(CloseNotification);
        signupPassword.contentType = TMP_InputField.ContentType.Password;
        loginPassword.contentType = TMP_InputField.ContentType.Password;
        // The player opts in by checking Remember Me on the login screen.
        rememberMe.isOn = false;
        title.SetActive(true);
        foreach (Transform child in titleImage.parent)
        {
            if (child.name.StartsWith("Flying Document") && child.GetComponent<FloatingDocument>() == null)
                child.gameObject.AddComponent<FloatingDocument>();
        }
        lobbyScreen.SetActive(false);
        signupPanel.SetActive(false);
        loginPanel.SetActive(false);
        // The package's demo panel controller expects demo-only scene references.
        var demoPanel = notification.GetComponent("PanelView") as Behaviour;
        if (demoPanel != null) demoPanel.enabled = false;
        notification.SetActive(false);
        loadingBar.gameObject.SetActive(false);
        firstScreen.SetActive(false);
        titleImage.localScale = Vector3.zero;
        SetX(termsButton, -720f);
        SetX(privacyButton, 500f);
        SetY(startLabel, -340f);
        pressToStart.gameObject.SetActive(false);
        sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(titleImage.DOScale(Vector3.one, introTitleDuration).SetEase(Ease.OutBack))
            .AppendInterval(introPause)
            .AppendCallback(() => firstScreen.SetActive(true))
            .Append(termsButton.DOAnchorPosX(220f, introButtonDuration).SetEase(Ease.OutCubic))
            .AppendInterval(introPause)
            .Append(privacyButton.DOAnchorPosX(-212f, introButtonDuration).SetEase(Ease.OutCubic))
            .AppendInterval(introPause)
            .Append(startLabel.DOAnchorPosY(170f, introButtonDuration).SetEase(Ease.OutCubic))
            .AppendInterval(introPause)
            .AppendCallback(EnablePressToStart);
        StartSavedLogin();
    }

    void OnDestroy()
    {
        sequence?.Kill();
        startPulse?.Kill();
    }

    static void SetX(RectTransform rect, float x)
    {
        Vector2 position = rect.anchoredPosition;
        position.x = x;
        rect.anchoredPosition = position;
    }

    static void SetY(RectTransform rect, float y)
    {
        Vector2 position = rect.anchoredPosition;
        position.y = y;
        rect.anchoredPosition = position;
    }

    // Kept for existing scene bindings. New buttons may select TapToStart directly.
    public void OpenSignup() { TapToStart(); }

    public void TapToStart()
    {
        if (busy || introTransition || tapPending) return;
        if (savedLoginState == SavedLoginState.SigningIn)
        {
            tapPending = true;
            pressToStart.interactable = false;
            loadingBar.gameObject.SetActive(true);
            loadingBar.value = loadingBar.minValue;
            loadingText.text = "SIGNING IN...";
            return;
        }
        ProceedFromStart();
    }

    void ProceedFromStart()
    {
        tapPending = false;
        introTransition = true;
        pressToStart.interactable = true;
        loadingBar.gameObject.SetActive(false);
        startPulse?.Kill();
        startLabel.localScale = Vector3.one;
        pressToStart.gameObject.SetActive(false);
        sequence?.Kill();
        sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(startLabel.DOAnchorPosY(-340f, buttonDuration).SetEase(Ease.InCubic))
            .Join(termsButton.DOAnchorPosX(-720f, buttonDuration).SetEase(Ease.InCubic))
            .Join(privacyButton.DOAnchorPosX(500f, buttonDuration).SetEase(Ease.InCubic))
            .AppendCallback(() =>
            {
                firstScreen.SetActive(false);
                introTransition = false;
                if (savedLoginState == SavedLoginState.SignedIn)
                    AuthenticationSucceeded();
                else if (savedLoginState == SavedLoginState.Failed)
                {
                    loginPanel.SetActive(true);
                    Notify("Sign-in failed", savedLoginError);
                }
                else signupPanel.SetActive(true);
            });
    }

    void StartSavedLogin()
    {
        string email, password;
        var load = SecureCredentials.Load(out email, out password);
        if (load == SecureCredentials.LoadResult.Missing) { savedLoginState = SavedLoginState.NoCredentials; return; }
        if (load == SecureCredentials.LoadResult.Unavailable)
        {
            savedLoginState = SavedLoginState.Failed;
            savedLoginError = "Saved sign-in details could not be read. Please sign in again.";
            return;
        }
        if (string.IsNullOrWhiteSpace(PlayFabSettings.TitleId))
        {
            savedLoginState = SavedLoginState.Failed;
            savedLoginError = "PlayFab is not configured yet.";
            return;
        }
        savedLoginState = SavedLoginState.SigningIn;
        PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest { Email = email, Password = password }, result =>
        {
            if (this == null) return;
            savedLoginState = SavedLoginState.SignedIn;
            if (tapPending) ProceedFromStart();
        }, error =>
        {
            if (this == null) return;
            savedLoginState = SavedLoginState.Failed;
            savedLoginError = string.IsNullOrEmpty(error.ErrorMessage) ? "Please sign in again." : error.ErrorMessage;
            if (error.Error == PlayFabErrorCode.InvalidEmailOrPassword || error.Error == PlayFabErrorCode.InvalidUsernameOrPassword || error.Error == PlayFabErrorCode.AccountNotFound)
                SecureCredentials.Clear();
            if (tapPending) ProceedFromStart();
        });
    }

    void ReturnToStart()
    {
        if (busy) return;
        signupPanel.SetActive(false);
        loginPanel.SetActive(false);
        signupPassword.text = "";
        loginPassword.text = "";
        firstScreen.SetActive(true);
        pressToStart.gameObject.SetActive(false);
        sequence?.Kill();
        sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(termsButton.DOAnchorPosX(220f, buttonDuration).SetEase(Ease.OutCubic))
            .Join(privacyButton.DOAnchorPosX(-212f, buttonDuration).SetEase(Ease.OutCubic))
            .Append(startLabel.DOAnchorPosY(170f, buttonDuration).SetEase(Ease.OutCubic))
            .AppendCallback(EnablePressToStart);
    }

    void EnablePressToStart()
    {
        pressToStart.gameObject.SetActive(true);
        startPulse?.Kill();
        startLabel.localScale = Vector3.one;
        startPulse = startLabel.DOScale(1.2f, 0.65f)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
    }

    void ShowLogin() { if (!busy) { signupPanel.SetActive(false); loginPanel.SetActive(true); } }
    void ShowSignup() { if (!busy) { loginPanel.SetActive(false); signupPanel.SetActive(true); } }
    void CloseNotification() { notification.SetActive(false); }
    void Notify(string heading, string message) { notificationTitle.text = heading; notificationInfo.text = message; notification.SetActive(true); notification.transform.SetAsLastSibling(); }
    public void ShowStatsError() { Notify("Stats unavailable", "Could not load your player information. Please try again later."); }
    public void ShowAvatarError(string message) { Notify("Profile picture", message); }

    bool Ready(string email, string password)
    {
        if (busy) return false;
        if (string.IsNullOrWhiteSpace(PlayFabSettings.TitleId)) { Notify("Setup needed", "PlayFab is not configured yet."); return false; }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) { Notify("Missing information", "Enter your email and password."); return false; }
        return true;
    }

    void SignUp()
    {
        string email = signupEmail.text.Trim();
        string username = signupUsername.text.Trim();
        string password = signupPassword.text;
        if (!Ready(email, password)) return;
        if (username.Length < 3 || username.Length > 20 || password.Length < 6 || password.Length > 100)
        { Notify("Check your details", "Username must be 3–20 characters and password must be 6–100 characters."); return; }
        busy = true;
        signupButton.interactable = false;
        PlayFabClientAPI.RegisterPlayFabUser(new RegisterPlayFabUserRequest
        { Email = email, Username = username, Password = password, RequireBothUsernameAndEmail = true },
            result => FinishManualLogin(email, password, true), AuthenticationFailed);
    }

    void LogIn()
    {
        string email = loginEmail.text.Trim();
        string password = loginPassword.text;
        if (!Ready(email, password)) return;
        bool remember = rememberMe.isOn;
        busy = true;
        loginButton.interactable = false;
        PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest { Email = email, Password = password },
            result => FinishManualLogin(email, password, remember), AuthenticationFailed);
    }

    void FinishManualLogin(string email, string password, bool remember)
    {
        if (this == null) return;
        // Clear an older account first so a failed replacement cannot sign in as the wrong user later.
        bool cleared = SecureCredentials.Clear();
        bool saved = cleared && (!remember || SecureCredentials.Save(email, password));
        savedLoginState = SavedLoginState.SignedIn;
        AuthenticationSucceeded();
        if (!saved) Notify("Sign-in", "You are signed in, but this device could not save your sign-in details. Please sign in again next time.");
    }

    void AuthenticationFailed(PlayFabError error)
    {
        if (this == null) return;
        busy = false;
        signupButton.interactable = true;
        loginButton.interactable = true;
        Notify("Could not sign in", string.IsNullOrEmpty(error.ErrorMessage) ? "Please try again." : error.ErrorMessage);
    }

    void AuthenticationSucceeded()
    {
        if (this == null) return;
        signupPassword.text = "";
        loginPassword.text = "";
        signupPanel.SetActive(false);
        loginPanel.SetActive(false);
        loadingBar.gameObject.SetActive(true);
        loadingBar.minValue = 0f;
        loadingBar.maxValue = 1f;
        loadingBar.value = 0f;
        loadingText.text = "0%";
        // PlayFab has already authenticated; this is a visual transition, not network progress.
        sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(DOTween.To(() => loadingBar.value, value => { loadingBar.value = value; loadingText.text = Mathf.RoundToInt(value * 100f) + "%"; }, 1f, loadingDuration).SetEase(Ease.Linear))
            .AppendCallback(() =>
            {
                lobbyScreen.SetActive(true);
                title.SetActive(false);
                busy = false;
                signupButton.interactable = true;
                loginButton.interactable = true;
                var dailyPopup = GetComponent<DailyLoginPopup>();
                if (dailyPopup != null) dailyPopup.CheckOnLogin();
            });
    }
}
