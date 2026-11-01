using Fusion;
using UnityEngine;

public static class ItemIcons
{
    //The icon of the item with this network id, null when there is none or it has no icon
    public static Sprite Get(NetworkRunner runner, NetworkId id)
    {
        if (runner == null || !id.IsValid)
            return null;

        if (runner.TryFindObject(id, out NetworkObject networkObject) && networkObject.TryGetComponent(out ThrowableItem item) && item.Data != null)
            return item.Data.icon;

        return null;
    }
}
