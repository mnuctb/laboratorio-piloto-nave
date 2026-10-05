using UnityEngine;

// Controller for the S2 simulator (ship/drone).
// Works with ANY of the 3 receivers, because it only asks for
// "whichever one implements IControlSource" on this same object.
//
// Mapping required by the Annex statement:
// - Potentiometer: controls altitude/acceleration CONTINUOUSLY
// - Button 1 (b1): shoot
// - Button 2 (b2): boost (while held down)
// - Button 3 (b3): mode switch
// - Button 4 (b4): used here as brake/reset (you can change it)

public class NaveController : MonoBehaviour
{
    IControlSource control;

    public float velocidadNormal = 5f;
    public float velocidadImpulso = 12f;

    [Header("Disparo")]
    public GameObject balaPrefab;       // drag your bullet prefab here
    public Transform puntoDisparo;       // empty object at the tip of the ship (optional)
    public float velocidadBala = 15f;

    bool modoImpulso = false;
    bool botonModoAnterior = false;
    bool botonDisparoAnterior = false;
    int modoActual = 0;

    void Start()
    {
        // Looks, on this same GameObject, for any script that implements
        // IControlSource (CSVReceiver, JSONReceiver or BinaryReceiver).
        control = GetComponent<IControlSource>();
        if (control == null)
        {
            Debug.LogError("NaveController no encontro ningun receptor " +
                            "(agrega CSVReceiver, JSONReceiver o BinaryReceiver a este objeto)");
        }
    }

    void Update()
    {
        if (control == null) return;

        bool[] b = control.Botones;
        int pot = control.Potenciometro;

        // --- Potentiometer: continuous altitude/acceleration ---
        float altitudNormalizada = pot / 1023f;      // 0.0 a 1.0
        float velocidadActual = modoImpulso ? velocidadImpulso : velocidadNormal;
        float delta = (altitudNormalizada - 0.5f) * velocidadActual * Time.deltaTime;
        transform.position += Vector3.up * delta;

        // --- Potentiometer: continuous altitude/acceleration ---
        if (b[0] && !botonDisparoAnterior)
        {
            Disparar();
        }
        botonDisparoAnterior = b[0];

        // --- Button 2: boost while held down ---
        modoImpulso = b[1];

        // --- Button 3: mode switch (only on press, not while held) ---
        if (b[2] && !botonModoAnterior)
        {
            modoActual = (modoActual + 1) % 3;
            Debug.Log("Cambio de modo -> " + modoActual);
        }
        botonModoAnterior = b[2];

        // --- Button 4: brake / position reset ---
        if (b[3]) transform.position = Vector3.zero;
    }

    void Disparar()
    {
        if (balaPrefab == null)
        {
            Debug.LogWarning("Falta asignar 'Bala Prefab' en el Inspector del NaveController");
            return;
        }

        Vector3 posicion = puntoDisparo != null ? puntoDisparo.position : transform.position;
        Quaternion rotacion = puntoDisparo != null ? puntoDisparo.rotation : transform.rotation;

        GameObject bala = Instantiate(balaPrefab, posicion, rotacion);
        Bullet script = bala.GetComponent<Bullet>();
        if (script != null) script.velocidad = velocidadBala;
    }
}