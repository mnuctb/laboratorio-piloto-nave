using UnityEngine;


// Projectile fired by the ship. It moves forward in a straight line
// and destroys itself after a set amount of time.
public class Bullet : MonoBehaviour
{
    public float velocidad = 15f; // Bullet speed (NaveController overwrites this value when shooting)
    public float tiempoDeVida = 3f;   // Seconds before the bullet is destroyed automatically


    void Start()
    {
        // Schedule the destruction of this bullet so it doesn't live forever
        // and pile up in the scene if it never hits anything.
        Destroy(gameObject, tiempoDeVida);
    }
    void Update()
    {

        // Move the bullet along its local forward direction.
        // Multiplying by Time.deltaTime keeps the speed independent of the frame rate.

        transform.position += transform.forward * velocidad * Time.deltaTime;
    }
}