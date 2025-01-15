using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CatController : MonoBehaviour
{
    public float speed = 5f;
    public float rotationSpeed = 360f;

    void Update()
    {
        // Movimento avanti/indietro
        float move = Input.GetAxis("Vertical") * speed * Time.deltaTime;

        // Rotazione
        float rotation = Input.GetAxis("Horizontal") * rotationSpeed * Time.deltaTime;

        // Applica trasformazioni
        transform.Translate(0, 0, move);
        transform.Rotate(0, rotation, 0);
    }
}

