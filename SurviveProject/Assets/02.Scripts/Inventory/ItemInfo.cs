using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ItemInfo
{
    public int itemIdx;

    public Sprite itemIcon = null;

    public int curStack = 1;
    public int itemStackMax = 1;

    public string itemName = "";
    public string itemInfo = "";
}
