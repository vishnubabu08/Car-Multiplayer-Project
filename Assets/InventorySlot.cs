using UnityEngine;

public class InventorySlot : MonoBehaviour
{

    public ItemData data;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log(data.itemName);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
