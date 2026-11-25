using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfileAvatarPicker : MonoBehaviour
{
    const string AvatarKey = "ProfileAvatar";
    [SerializeField] Sprite[] avatars;
    [SerializeField] Image profileImage;
    [SerializeField] GameObject popup;
    [SerializeField] Button openButton;
    [SerializeField] Button confirmButton;
    [SerializeField] Button cancelButton;
    [SerializeField] Button[] avatarButtons;
    [SerializeField] Image[] selectionBorders;
    [SerializeField] TMP_Text statusText;

    int current = -1;
    int selected = -1;
    int generation;
    bool busy;
    bool loaded;

    public Sprite CurrentAvatar => current >= 0 && current < avatars.Length ? avatars[current] : null;

    void Awake()
    {
        openButton.onClick.AddListener(Open);
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Close);
        for (int i = 0; i < avatarButtons.Length; i++)
        {
            int index = i;
            avatarButtons[i].onClick.AddListener(() => Select(index));
        }
        popup.SetActive(false);
        openButton.interactable = false;
    }

    void OnEnable()
    {
        int request = ++generation;
        loaded = false;
        busy = false;
        openButton.interactable = false;
        if (!PlayFabClientAPI.IsClientLoggedIn() || avatars == null || avatars.Length == 0) return;
        PlayFabClientAPI.GetUserData(new GetUserDataRequest { Keys = new List<string> { AvatarKey } }, result =>
        {
            if (!Valid(request)) return;
            UserDataRecord record;
            if (result.Data != null && result.Data.TryGetValue(AvatarKey, out record) && record != null && !string.IsNullOrEmpty(record.Value))
            {
                int index = System.Array.FindIndex(avatars, avatar => avatar != null && avatar.name == record.Value);
                if (index < 0) { Error("Your saved avatar could not be found. Please contact support."); return; }
                Apply(index);
                loaded = true;
                openButton.interactable = true;
                return;
            }
            // Persist a first avatar only after a successful read shows no saved choice.
            Save(Random.Range(0, avatars.Length), request, false);
        }, error => { if (Valid(request)) Error("Could not load your profile picture. Please try again later."); });
    }

    void OnDisable()
    {
        generation++;
        if (popup != null) popup.SetActive(false);
    }

    bool Valid(int request) => this != null && isActiveAndEnabled && request == generation;

    void Apply(int index)
    {
        current = index;
        profileImage.sprite = avatars[index];
        profileImage.color = Color.white;
        profileImage.preserveAspect = true;
    }

    public void Open()
    {
        if (!loaded || busy) return;
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();
        statusText.text = "SELECT YOUR AVATAR";
        Select(current);
    }

    public void Select(int index)
    {
        if (busy || index < 0 || index >= avatars.Length) return;
        selected = index;
        for (int i = 0; i < selectionBorders.Length; i++)
            selectionBorders[i].color = i == selected ? new Color32(255, 204, 44, 255) : new Color32(119, 155, 182, 255);
    }

    public void Confirm()
    {
        if (!loaded || busy || selected < 0) return;
        if (selected == current) { Close(); return; }
        Save(selected, generation, true);
    }

    public void Close()
    {
        if (!busy) popup.SetActive(false);
    }

    void Save(int index, int request, bool closeAfter)
    {
        busy = true;
        confirmButton.interactable = false;
        cancelButton.interactable = false;
        statusText.text = "SAVING...";
        PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string> { { AvatarKey, avatars[index].name } }
        }, result =>
        {
            if (!Valid(request)) return;
            busy = false;
            loaded = true;
            confirmButton.interactable = true;
            cancelButton.interactable = true;
            openButton.interactable = true;
            Apply(index);
            if (closeAfter) popup.SetActive(false);
        }, error =>
        {
            if (!Valid(request)) return;
            busy = false;
            confirmButton.interactable = true;
            cancelButton.interactable = true;
            statusText.text = "COULD NOT SAVE. PLEASE TRY AGAIN.";
            if (!closeAfter) Error("Could not save your profile picture. Please try again later.");
        });
    }

    void Error(string message)
    {
        Debug.LogWarning(message);
        var lobby = FindObjectOfType<Lobby>();
        if (lobby != null) lobby.ShowAvatarError(message);
    }
}
