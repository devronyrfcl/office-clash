using UnityEngine;

public class ComicSystem : MonoBehaviour
{
    public GameObject comicParent; // The parent GameObject of the comic
    public GameObject[] pages; // Array of comic pages
    public GameObject finishButton; // Finish button that appears on the last page
    private int currentPage = 0; // Track the current page
    private const string ComicShownKey = "ComicShown";

    // Start is called before the first frame update
    void Start()
    {
        // Check if the comic has been shown before
        if (PlayerPrefs.GetInt(ComicShownKey, 0) == 0)
        {
            // Activate the Comic_Parent GameObject
            comicParent.SetActive(true);

            // Initialize the comic
            InitializeComic();

            // Set the flag to indicate the comic has been shown
            PlayerPrefs.SetInt(ComicShownKey, 1);
            PlayerPrefs.Save();
        }
        else
        {
            // Deactivate the Comic_Parent GameObject if it should not be shown again
            comicParent.SetActive(false);
        }
    }

    // Function to show the next page
    public void NextPage()
    {
        if (currentPage < pages.Length - 1)
        {
            pages[currentPage].SetActive(false);
            currentPage++;
            ShowPage(currentPage);
        }
        UpdateFinishButton();
    }

    // Function to show the previous page
    public void PreviousPage()
    {
        if (currentPage > 0)
        {
            pages[currentPage].SetActive(false);
            currentPage--;
            ShowPage(currentPage);
        }
        UpdateFinishButton();
    }

    // Function to reset to the first page
    public void ResetToFirstPage()
    {
        if (pages != null && pages.Length > 0)
        {
            pages[currentPage].SetActive(false);
            currentPage = 0;
            ShowPage(currentPage);
            UpdateFinishButton();
        }
    }

    // Function to show the current page
    private void ShowPage(int pageIndex)
    {
        if (pages != null && pages.Length > 0)
        {
            pages[pageIndex].SetActive(true);
        }
    }

    // Function to hide all pages
    private void HideAllPages()
    {
        foreach (var page in pages)
        {
            page.SetActive(false);
        }
    }

    // Function to initialize the comic
    public void InitializeComic()
    {
        HideAllPages();
        currentPage = 0;
        ShowPage(currentPage);
        UpdateFinishButton();
    }

    // Function to update the finish button visibility
    private void UpdateFinishButton()
    {
        if (finishButton != null)
        {
            finishButton.SetActive(currentPage == pages.Length - 1);
        }
    }

    public void ExitApplication()
    {
        // Quit the application
        Application.Quit();

        // If running in the Unity editor, log a message
        #if UNITY_EDITOR
        Debug.Log("Application.Quit() called in the editor");
        #endif
    }
}
