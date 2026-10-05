using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

// On-screen measurement tool for the lab.
// Add it to the SAME GameObject as the active receiver (CSV, JSON or Binary).
//
// It shows live data and runs three tests:
//   1) Test value: asks the Arduino for the fixed value 1,0,1,0,512 and
//      checks that the receiver decodes exactly that value.
//   2) Latency: sends N pings and measures the round-trip time
//      (Unity -> Arduino -> Unity) with a high resolution clock.
//   3) Frequency: during a fixed time counts valid and invalid messages
//      and received bytes, to get messages per second and bytes per message.
//
// Results are saved as CSV files in the "Mediciones" folder, next to the
// Assets folder of the project.
public class MedidorProtocolo : MonoBehaviour
{
    [Header("Latency test")]
    public int cantidadPings = 100;
    public float esperaEntrePings = 0.1f;   // seconds between pings
    public float tiempoLimitePing = 1f;     // a ping without answer after this time is lost

    [Header("Frequency test")]
    public float duracionFrecuencia = 30f;  // seconds

    ReceptorBase receptor;
    string carpeta;
    bool pruebaEnCurso = false;

    string estado = "Esperando que el Arduino reinicie...";
    string resultadoValor = "-";
    string resultadoLatencia = "-";
    string resultadoFrecuencia = "-";

    // Live messages per second
    float tiempoConteo;
    int validosPrevios;
    float mensajesPorSegundo;

    void Start()
    {
        receptor = GetComponent<ReceptorBase>();
        if (receptor == null)
        {
            Debug.LogError("MedidorProtocolo needs a receiver (CSV, JSON or Binary) on the same GameObject");
            enabled = false;
            return;
        }
        carpeta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mediciones"));
        Directory.CreateDirectory(carpeta);
    }

    void Update()
    {
        // Messages received during the last second
        tiempoConteo += Time.unscaledDeltaTime;
        if (tiempoConteo >= 1f)
        {
            int validos = receptor.MensajesValidos;
            mensajesPorSegundo = (validos - validosPrevios) / tiempoConteo;
            validosPrevios = validos;
            tiempoConteo = 0f;
        }

        // The Arduino resets when the port opens: wait 3 s before testing
        if (!pruebaEnCurso && Time.timeSinceLevelLoad > 3f && estado.StartsWith("Esperando"))
            estado = "Listo";
    }

    // ------------------------------------------------------------------
    // Test 1: same test value for the three protocols
    // ------------------------------------------------------------------
    IEnumerator PruebaValor()
    {
        pruebaEnCurso = true;
        estado = "Enviando valor de prueba...";

        receptor.EnviarComando('M');                    // stop periodic messages
        yield return new WaitForSecondsRealtime(0.5f);

        int antes = receptor.MensajesValidos;
        receptor.EnviarComando('V');                    // ask for 1,0,1,0,512
        float limite = Time.realtimeSinceStartup + 1f;
        while (receptor.MensajesValidos == antes && Time.realtimeSinceStartup < limite)
            yield return null;

        bool[] b = receptor.Botones;
        string recibido = $"{B(b[0])},{B(b[1])},{B(b[2])},{B(b[3])},{receptor.Potenciometro}";
        bool correcto = receptor.MensajesValidos > antes && recibido == "1,0,1,0,512";
        resultadoValor = recibido + (correcto ? "  -> CORRECTO" : "  -> NO COINCIDE");

        GuardarLinea("prueba_valor.csv",
            "fecha;protocolo;valor_recibido;correcto",
            $"{Fecha()};{receptor.NombreProtocolo};{recibido};{(correcto ? "si" : "no")}");

        receptor.EnviarComando('N');                    // back to normal mode
        estado = "Listo";
        pruebaEnCurso = false;
    }

    // ------------------------------------------------------------------
    // Test 2: round-trip latency with pings
    // ------------------------------------------------------------------
    IEnumerator PruebaLatencia()
    {
        pruebaEnCurso = true;
        receptor.EnviarComando('M');                    // only answer pings
        yield return new WaitForSecondsRealtime(0.5f);  // let pending messages arrive

        var muestras = new List<double>();
        int perdidos = 0;
        string archivo = $"latencia_{receptor.NombreProtocolo}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        for (int i = 1; i <= cantidadPings; i++)
        {
            estado = $"Ping {i}/{cantidadPings}";
            receptor.EnviarPing();

            float limite = Time.realtimeSinceStartup + tiempoLimitePing;
            while (receptor.EsperandoPing && Time.realtimeSinceStartup < limite)
                yield return null;

            double latencia;
            if (receptor.EsperandoPing)
            {
                receptor.CancelarPing();
                perdidos++;
                latencia = -1;                          // -1 = lost ping
            }
            else
            {
                latencia = receptor.UltimaLatenciaMs;
                muestras.Add(latencia);
            }
            GuardarLinea(archivo, "ping;latencia_ms", $"{i};{N(latencia)}");

            yield return new WaitForSecondsRealtime(esperaEntrePings);
        }

        receptor.EnviarComando('N');

        if (muestras.Count > 0)
        {
            double promedio = muestras.Average();
            double desviacion = muestras.Count > 1
                ? Math.Sqrt(muestras.Sum(x => (x - promedio) * (x - promedio)) / (muestras.Count - 1))
                : 0;
            double minimo = muestras.Min();
            double maximo = muestras.Max();

            resultadoLatencia = $"prom {promedio:F2} ms | desv {desviacion:F2} | min {minimo:F2} | max {maximo:F2} | perdidos {perdidos}";
            GuardarLinea("resumen_latencia.csv",
                "fecha;protocolo;pings;perdidos;promedio_ms;desviacion_ms;minimo_ms;maximo_ms",
                $"{Fecha()};{receptor.NombreProtocolo};{cantidadPings};{perdidos};{N(promedio)};{N(desviacion)};{N(minimo)};{N(maximo)}");
        }
        else
        {
            resultadoLatencia = "Ningun ping respondio. Revisa el sketch y el puerto.";
        }

        estado = "Listo";
        pruebaEnCurso = false;
    }

    // ------------------------------------------------------------------
    // Test 3: messages per second, invalid messages and bytes per message
    // ------------------------------------------------------------------
    IEnumerator PruebaFrecuencia()
    {
        pruebaEnCurso = true;

        int validos0 = receptor.MensajesValidos;
        int invalidos0 = receptor.MensajesInvalidos;
        long bytes0 = receptor.BytesRecibidos;
        float inicio = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - inicio < duracionFrecuencia)
        {
            estado = $"Midiendo frecuencia... {duracionFrecuencia - (Time.realtimeSinceStartup - inicio):F0} s";
            yield return null;
        }

        float duracion = Time.realtimeSinceStartup - inicio;
        int validos = receptor.MensajesValidos - validos0;
        int invalidos = receptor.MensajesInvalidos - invalidos0;
        long bytes = receptor.BytesRecibidos - bytes0;

        double porSegundo = validos / duracion;
        double bytesPorMensaje = validos > 0 ? (double)bytes / validos : 0;
        double bytesPorSegundo = bytes / duracion;

        resultadoFrecuencia = $"{porSegundo:F2} msg/s | {bytesPorMensaje:F1} bytes/msg | {bytesPorSegundo:F0} B/s | invalidos {invalidos}";
        GuardarLinea("resumen_frecuencia.csv",
            "fecha;protocolo;duracion_s;mensajes_validos;mensajes_invalidos;mensajes_por_s;bytes_por_mensaje;bytes_por_s",
            $"{Fecha()};{receptor.NombreProtocolo};{N(duracion)};{validos};{invalidos};{N(porSegundo)};{N(bytesPorMensaje)};{N(bytesPorSegundo)}");

        estado = "Listo";
        pruebaEnCurso = false;
    }

    // ------------------------------------------------------------------
    // On-screen panel
    // ------------------------------------------------------------------
    void OnGUI()
    {
        GUI.skin.label.fontSize = 14;
        GUI.skin.button.fontSize = 14;

        GUILayout.BeginArea(new Rect(10, 10, 460, 360), GUI.skin.box);

        GUILayout.Label($"Protocolo: {receptor.NombreProtocolo}   Puerto: {receptor.puerto} ({(receptor.PuertoAbierto ? "abierto" : "CERRADO")})");
        bool[] b = receptor.Botones;
        GUILayout.Label($"Valor actual: b1={B(b[0])} b2={B(b[1])} b3={B(b[2])} b4={B(b[3])} pot={receptor.Potenciometro}");
        GUILayout.Label($"Mensajes/s: {mensajesPorSegundo:F1}   Invalidos: {receptor.MensajesInvalidos}");

        GUILayout.Space(6);
        GUI.enabled = !pruebaEnCurso && receptor.PuertoAbierto && Time.timeSinceLevelLoad > 3f;
        if (GUILayout.Button("1. Prueba de valor (1,0,1,0,512)")) StartCoroutine(PruebaValor());
        if (GUILayout.Button($"2. Prueba de latencia ({cantidadPings} pings)")) StartCoroutine(PruebaLatencia());
        if (GUILayout.Button($"3. Prueba de frecuencia ({duracionFrecuencia:F0} s)")) StartCoroutine(PruebaFrecuencia());
        GUI.enabled = true;

        GUILayout.Space(6);
        GUILayout.Label("Estado: " + estado);
        GUILayout.Label("Valor: " + resultadoValor);
        GUILayout.Label("Latencia: " + resultadoLatencia);
        GUILayout.Label("Frecuencia: " + resultadoFrecuencia);

        GUILayout.EndArea();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    static string B(bool v) => v ? "1" : "0";

    static string Fecha() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    // Numbers are saved with a dot as decimal separator, so Excel or
    // Google Sheets can read them no matter the language of the computer
    static string N(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

    // Appends one line to a CSV file (writes the header if the file is new)
    void GuardarLinea(string nombreArchivo, string encabezado, string linea)
    {
        string ruta = Path.Combine(carpeta, nombreArchivo);
        if (!File.Exists(ruta)) File.WriteAllText(ruta, encabezado + "\n");
        File.AppendAllText(ruta, linea + "\n");
    }
}
