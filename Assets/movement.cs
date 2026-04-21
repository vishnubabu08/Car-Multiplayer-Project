using UnityEngine;

public class movement : MonoBehaviour
{

    public float X;
    public float z;
    public float Speed = 20f;
    public GameObject win;


    private void Update()
    {
        X = Input.GetAxis("Horizontal");
        z = Input.GetAxis("Vertical");

        transform.Translate(new Vector3(X, 0, z) * Speed * Time.deltaTime);

       if( Input.GetKeyDown(KeyCode.Space)){

            transform.Translate(new Vector3(0, 1, 0) * Speed * Time.deltaTime);

        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "cube2")
        {

            collision.gameObject.GetComponent<Renderer>().material.color = Color.green;

            win.SetActive(true);
        }

    }
}
