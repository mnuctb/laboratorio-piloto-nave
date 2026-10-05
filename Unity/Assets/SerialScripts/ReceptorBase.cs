using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Base class shared by the three receivers (CSV, JSON and Binary).
//
// What it does:
//  - Opens the serial port and reads it in a background thread, so the
//    arrival time of each message does not depend on Unity's frame rate.
//  - Stores the last valid control state (4 buttons + potentiometer),
//    which NaveController reads through the IControlSource interface.
//  - Keeps the statistics used by MedidorProtocolo: valid messages,
//    invalid (discarded) messages, received bytes and ping latency.
//
// Each protocol only has to implement ProcesarByte().
public abstract class ReceptorBase : MonoBehaviour, IControlSource
{
    [Tooltip("Arduino serial port (check it in the Arduino IDE)")]
    public string puerto = "COM6";

    [Tooltip("Must be the same value used in Serial.begin() on the Arduino")]
    public int baudios = 9600;

    // Protocol name shown on screen and used in the result files
    public abstract string NombreProtocolo { get; }

    // ---------------- Control state (read by NaveController) ----------------
    readonly bool[] botones = new bool[4];
    volatile int potenciometro = 0;

    public bool[] Botones => botones;
    public int Potenciometro => potenciometro;

    // ---------------- Statistics (read by MedidorProtocolo) ----------------
    int mensajesValidos;
    int mensajesInvalidos;
    long bytesRecibidos;

    public int MensajesValidos => Volatile.Read(ref mensajesValidos);
    public int MensajesInvalidos => Volatile.Read(ref mensajesInvalidos);
    public long BytesRecibidos => Interlocked.Read(ref bytesRecibidos);
    public bool PuertoAbierto => serialPort != null && serialPort.IsOpen;

    // ---------------- Ping (round-trip latency) ----------------
    readonly Stopwatch reloj = Stopwatch.StartNew();   // high resolution clock
    long tickEnvioPing;
    volatile bool esperandoPing = false;
    double ultimaLatenciaMs = -1;

    public bool EsperandoPing => esperandoPing;
    public double UltimaLatenciaMs => Volatile.Read(ref ultimaLatenciaMs);

    // ---------------- Serial port ----------------
    SerialPort serialPort;
    Thread hiloLectura;
    volatile bool leyendo = false;

    protected virtual void Start()
    {
        try
        {
            serialPort = new SerialPort(puerto, baudios);
            serialPort.ReadTimeout = 100;   // lets the thread check 'leyendo' regularly
            serialPort.DtrEnable = true;    // resets the Arduino when the port opens
            serialPort.Open();
        }
        catch (Exception ex)
        {
            Debug.LogError("Could not open " + puerto + ". Is the Arduino Serial Monitor closed? " + ex.Message);
            return;
        }

        leyendo = true;
        hiloLectura = new Thread(LeerPuerto) { IsBackground = true };
        hiloLectura.Start();
    }

    // Runs in the background thread: reads byte by byte and passes each
    // byte to the protocol decoder.
    void LeerPuerto()
    {
        while (leyendo)
        {
            try
            {
                int dato = serialPort.ReadByte();
                if (dato < 0) continue;
                Interlocked.Increment(ref bytesRecibidos);
                ProcesarByte((byte)dato);
            }
            catch (TimeoutException)
            {
                // No data in the last 100 ms: keep waiting
            }
            catch (Exception ex)
            {
                if (leyendo) Debug.LogWarning("Serial error (" + NombreProtocolo + "): " + ex.Message);
            }
        }
    }

    // Each protocol decodes the incoming bytes in its own way
    protected abstract void ProcesarByte(byte dato);

    // Called by the protocol when a complete and correct message arrives
    protected void RegistrarValido(bool[] nuevosBotones, int nuevoPot)
    {
        for (int i = 0; i < 4; i++) botones[i] = nuevosBotones[i];
        potenciometro = nuevoPot;
        Interlocked.Increment(ref mensajesValidos);

        // If a ping is pending, this message is its answer: save the time
        if (esperandoPing)
        {
            long ticks = reloj.ElapsedTicks - Volatile.Read(ref tickEnvioPing);
            Volatile.Write(ref ultimaLatenciaMs, ticks * 1000.0 / Stopwatch.Frequency);
            esperandoPing = false;
        }
    }

    // Called by the protocol when a message is incomplete or corrupted
    protected void RegistrarInvalido(string motivo)
    {
        Interlocked.Increment(ref mensajesInvalidos);
        Debug.LogWarning(NombreProtocolo + ": message discarded (" + motivo + ")");
    }

    // Sends a one-character command to the Arduino ('N', 'M', 'P' or 'V')
    public void EnviarComando(char comando)
    {
        if (!PuertoAbierto) return;
        try { serialPort.Write(comando.ToString()); }
        catch (Exception ex) { Debug.LogWarning("Could not send command: " + ex.Message); }
    }

    // Starts a ping: saves the current time and asks the Arduino for one message
    public void EnviarPing()
    {
        Volatile.Write(ref tickEnvioPing, reloj.ElapsedTicks);
        esperandoPing = true;
        EnviarComando('P');
    }

    // Used when a ping gets no answer before the time limit
    public void CancelarPing()
    {
        esperandoPing = false;
    }

    protected virtual void OnDestroy()
    {
        leyendo = false;
        hiloLectura?.Join(300);
        if (PuertoAbierto) serialPort.Close();
    }
}
