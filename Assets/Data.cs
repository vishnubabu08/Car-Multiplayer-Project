using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "Scriptable Objects/Data")]
public class Data : ScriptableObject
{
   
        public Details details = new Details();


}

public class Details
{
    public string name;
    public int Age;
    public string place;
}