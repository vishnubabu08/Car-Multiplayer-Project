using UnityEngine;

[CreateAssetMenu(fileName = "NewItemData", menuName = "Scriptable Objects/ItemData")]
public class ItemData: ScriptableObject
{
    public string itemName;
    public int goldValue;
    public Sprite icon;
}
