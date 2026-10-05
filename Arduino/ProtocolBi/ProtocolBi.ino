// =============================================================
// PROTOCOL C - Raw binary with header, length and checksum
// Packet format (6 bytes):
//   [0xAA][len][mask][potH][potL][checksum]
//   0xAA     -> header, marks the start of a packet
//   len      -> number of data bytes that follow (always 3)
//   mask     -> buttons, one bit per button (bit0 = b1 ... bit3 = b4)
//   potH/L   -> potentiometer (0-1023) split in high and low byte
//   checksum -> XOR of len, mask, potH and potL
//
// Commands received from Unity (one character each):
//   'N' -> normal mode: send a reading every 50 ms (default)
//   'M' -> measurement mode: stop periodic sending, only answer pings
//   'P' -> ping: send one reading immediately (round-trip latency test)
//   'V' -> test value: send the fixed value 1,0,1,0,512
// =============================================================

const int botonPins[4] = {4, 5, 6, 7};      // Digital pins of the 4 buttons
const int potPin = A0;                      // Analog pin of the potentiometer
const unsigned long INTERVALO_MS = 50;      // Time between readings (~20 per second)

const byte HEADER = 0xAA;                   // Start-of-packet byte
const byte LEN = 3;                         // Data bytes: mask, potH, potL

bool modoMedicion = false;                  // true = only answer pings
unsigned long ultimoEnvio = 0;              // Time of the last periodic message

void setup() {
  Serial.begin(9600);
  for (int i = 0; i < 4; i++) {
    // Internal pull-up: the pin reads LOW when the button is pressed
    pinMode(botonPins[i], INPUT_PULLUP);
  }
}

void loop() {
  // 1) Check if Unity sent a command
  while (Serial.available() > 0) {
    char comando = Serial.read();
    if (comando == 'M') modoMedicion = true;
    else if (comando == 'N') modoMedicion = false;
    else if (comando == 'P') enviarLectura();   // answer the ping right away
    else if (comando == 'V') enviarPaquete(0b0101, 512); // b1=1, b3=1, pot=512
  }

  // 2) Periodic sending (millis() is used instead of delay() so the
  //    board can answer commands at any moment)
  if (!modoMedicion && millis() - ultimoEnvio >= INTERVALO_MS) {
    ultimoEnvio = millis();
    enviarLectura();
  }
}

// Reads the buttons and the potentiometer and sends them
void enviarLectura() {
  byte mask = 0;
  for (int i = 0; i < 4; i++) {
    // Each pressed button turns on its own bit inside the mask
    if (digitalRead(botonPins[i]) == LOW) mask |= (1 << i);
  }
  int pot = analogRead(potPin);
  enviarPaquete(mask, pot);
}

// Builds and sends one 6-byte binary packet
void enviarPaquete(byte mask, int pot) {
  byte potH = (pot >> 8) & 0xFF;   // high byte (0 to 3)
  byte potL = pot & 0xFF;          // low byte (0 to 255)
  byte checksum = LEN ^ mask ^ potH ^ potL;

  Serial.write(HEADER);
  Serial.write(LEN);
  Serial.write(mask);
  Serial.write(potH);
  Serial.write(potL);
  Serial.write(checksum);
}
