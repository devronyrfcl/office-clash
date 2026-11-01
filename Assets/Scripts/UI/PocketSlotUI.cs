using Fusion;
using UnityEngine;
using UnityEngine.UI;

//Shows what the local player carries in one pocket. The button itself is a TouchButton that swaps the pocket
public class PocketSlotUI : MonoBehaviour
{
    [SerializeField] int pocketIndex = 0;
    [SerializeField] Image icon;

    NetworkId shownId;

    void Update()
    {
        NetworkPlayer local = NetworkPlayer.Local;

        if (local == null || local.Runner == null || !local.Object.IsValid)
        {
            Show(default, null);
            return;
        }

        PlayerInventory inventory = local.GetComponent<PlayerInventory>();

        if (inventory == null)
            return;

        NetworkId id = inventory.GetPocketItem(pocketIndex);

        //Nothing changed since the last frame
        if (id == shownId && (id.IsValid == (icon.sprite != null)))
            return;

        Show(id, ItemIcons.Get(local.Runner, id));
    }

    void Show(NetworkId id, Sprite sprite)
    {
        shownId = id;

        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.preserveAspect = true;
    }
}
