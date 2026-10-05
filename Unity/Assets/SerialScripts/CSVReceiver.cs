using System.Text;

// PROTOCOL A: decodes text lines with the format "b1,b2,b3,b4,pot\n".
// Framing: the line break ('\n') marks the end of each message.
// Validation: exactly 5 fields, buttons must be 0 or 1 and the
// potentiometer must be a number between 0 and 1023. Otherwise the
// message is discarded and the previous valid state is kept.
public class CSVReceiver : ReceptorBase
{
    public override string NombreProtocolo => "CSV";

    const int LARGO_MAXIMO = 64;                    // protects against garbage without '\n'
    readonly StringBuilder linea = new StringBuilder();

    protected override void ProcesarByte(byte dato)
    {
        char c = (char)dato;

        if (c == '\n')                              // end of message
        {
            string texto = linea.ToString().Trim(); // Trim removes the '\r' sent by println
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
        string[] partes = texto.Split(',');
        if (partes.Length != 5)
        {
            RegistrarInvalido("expected 5 fields");
            return;
        }

        bool[] botones = new bool[4];
        for (int i = 0; i < 4; i++)
        {
            if (partes[i] == "1") botones[i] = true;
            else if (partes[i] == "0") botones[i] = false;
            else { RegistrarInvalido("invalid button value"); return; }
        }

        // A temporary variable is used so a bad value never moves the ship
        if (!int.TryParse(partes[4], out int pot) || pot < 0 || pot > 1023)
        {
            RegistrarInvalido("invalid potentiometer value");
            return;
        }

        RegistrarValido(botones, pot);
    }
}