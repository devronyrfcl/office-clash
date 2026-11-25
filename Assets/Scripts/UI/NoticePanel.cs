using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;

public class NoticePanel : MonoBehaviour
{
    [SerializeField] GameObject popup;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text body;
    int requestVersion;

    public void Open()
    {
        if (popup == null || title == null || body == null) return;
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();
        title.text = "NOTICE";
        body.text = "Loading notice...";
        int version = ++requestVersion;
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            body.text = "Please sign in to view notices.";
            return;
        }
        PlayFabClientAPI.GetTitleData(new GetTitleDataRequest
        {
            Keys = new List<string> { "NoticeTitle", "NoticeBody" }
        }, result =>
        {
            if (this == null || version != requestVersion || !popup.activeInHierarchy) return;
            string noticeTitle, noticeBody;
            if (result.Data == null || !result.Data.TryGetValue("NoticeBody", out noticeBody) || string.IsNullOrWhiteSpace(noticeBody))
            {
                body.text = "There are no notices right now. Check back later!";
                return;
            }
            if (result.Data.TryGetValue("NoticeTitle", out noticeTitle) && !string.IsNullOrWhiteSpace(noticeTitle))
                title.text = noticeTitle;
            body.text = noticeBody;
        }, error =>
        {
            if (this == null || version != requestVersion || !popup.activeInHierarchy) return;
            body.text = "Could not load notices. Please close and try again.";
            Debug.LogWarning("Notice load failed: " + error.Error);
        });
    }

    public void Close()
    {
        requestVersion++;
        if (popup != null) popup.SetActive(false);
    }
}
