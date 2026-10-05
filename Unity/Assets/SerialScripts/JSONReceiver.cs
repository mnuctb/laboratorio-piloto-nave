using System.Text;
using UnityEngine;

// Class used by JsonUtility: the field names must match the JSON keys
[System.Serializable]
public class EstadoJSON
{
    public int b1, b2, b3, b4, pot;
}

// PROTOCOL B: decodes one JSON object per line, for example
//   {"b1":1,"b2":0,"b3":1,"b4":0,"pot":512}
// Framing: the line break ('\n') marks the end of each message.
// Deserialization: JsonUtility (built into Unity, no extra packages).
// Validation: all keys must be present (JsonUtility silently fills
// missing keys with 0), buttons must be 0 or 1 and the potentiometer
// must be between 0 and 1023.
public class JSONReceiver : ReceptorBase
{
    public override string NombreProtocolo => "JSON";

    const int LARGO_MAXIMO = 128;                   // protects against garbage without '\n'
    static readonly string[] CLAVES = { "\"b1\"", "\"b2\"", "\"b3\"", "\"b4\"", "\"pot\"" };
    readonly StringBuilder linea = new StringBuilder();

    protected override void ProcesarByte(byte dato)
    {
        char c = (char)dato;

        if (c == '\n')                              // end of message
        {
            string texto = linea.ToString().Trim();
            linea.Clear();
            if (texto.Length > 0) ProcesarLinea(texto);
        }
        else if (linea.Length < LARGO_MAXIMO)
        {
            linea.Append(c);
        }
        else
        {
            linea.Clear();
            RegistrarInvalido("line too long");
        }
    }

    void ProcesarLinea(string texto)
    {
        // Keep only the text between the braces
        int inicio = texto.IndexOf('{');
        int fin = texto.LastIndexOf('}');
        if (inicio < 0 || fin <= inicio)
        {
            RegistrarInvalido("no JSON object found");
            return;
        }
        texto = texto.Substring(inicio, fin - inicio + 1);

        foreach (string clave in CLAVES)
        {
            if (!texto.Contains(clave))
            {
                RegistrarInvalido("missing key " + clave);
                return;
            }
        }

        EstadoJSON estado;
        try
        {
            estado = JsonUtility.FromJson<EstadoJSON>(texto);
        }
        catch (System.Exception)
        {
            RegistrarInvalido("invalid JSON");
            return;
        }

        int[] valores = { estado.b1, estado.b2, estado.b3, estado.b4 };
        bool[] botones = new bool[4];
        for (int i = 0; i < 4; i++)
        {
            if (valores[i] != 0 && valores[i] != 1) { RegistrarInvalido("invalid button value"); return; }
            botones[i] = valores[i] == 1;
        }

        if (estado.pot < 0 || estado.pot > 1023)
        {
            RegistrarInvalido("invalid potentiometer value");
            return;
        }

        RegistrarValido(botones, estado.pot);
    }
}