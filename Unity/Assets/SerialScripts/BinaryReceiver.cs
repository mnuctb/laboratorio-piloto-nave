// PROTOCOL C: decodes binary packets with the format
//   [0xAA][len][mask][potH][potL][checksum]
// Framing: header byte (0xAA) + length byte.
// Integrity: XOR checksum of len, mask, potH and potL.
//
// A state machine reads one byte at a time:
//   EsperandoHeader -> Longitud -> Payload -> Checksum -> EsperandoHeader
//
// About escaping: no byte stuffing is used. A data byte can be equal to
// 0xAA (for example when pot = 170, 426, 682 or 938), but while the
// receiver is synchronized it reads the data by length, so that byte is
// never taken as a header. If synchronization is lost, the length check
// and the checksum detect the wrong packet and it is discarded.
public class BinaryReceiver : ReceptorBase
{
    public override string NombreProtocolo => "Binario";

    const byte HEADER = 0xAA;
    const byte LONGITUD_ESPERADA = 3;   // mask, potH, potL

    enum Estado { EsperandoHeader, Longitud, Payload, Checksum }
    Estado estado = Estado.EsperandoHeader;

    readonly byte[] payload = new byte[LONGITUD_ESPERADA];
    int indicePayload;

    protected override void ProcesarByte(byte dato)
    {
        switch (estado)
        {
            case Estado.EsperandoHeader:
                if (dato == HEADER) estado = Estado.Longitud;
                break;

            case Estado.Longitud:
                if (dato == LONGITUD_ESPERADA)
                {
                    indicePayload = 0;
                    estado = Estado.Payload;
                }
                else
                {
                    // Wrong length: this was not a real header
                    RegistrarInvalido("unexpected length " + dato);
                    estado = (dato == HEADER) ? Estado.Longitud : Estado.EsperandoHeader;
                }
                break;

            case Estado.Payload:
                payload[indicePayload++] = dato;
                if (indicePayload >= LONGITUD_ESPERADA) estado = Estado.Checksum;
                break;

            case Estado.Checksum:
                byte calculado = LONGITUD_ESPERADA;
                for (int i = 0; i < LONGITUD_ESPERADA; i++) calculado ^= payload[i];

                if (calculado == dato)
                {
                    byte mask = payload[0];
                    bool[] botones = new bool[4];
                    for (int i = 0; i < 4; i++)
                        botones[i] = (mask & (1 << i)) != 0;   // bit i = button i+1

                    int pot = (payload[1] << 8) | payload[2];  // join high and low byte
                    if (pot <= 1023) RegistrarValido(botones, pot);
                    else RegistrarInvalido("potentiometer out of range");
                }
                else
                {
                    RegistrarInvalido("wrong checksum");
                }
                estado = Estado.EsperandoHeader;   // ready for the next packet
                break;
        }
    }
}