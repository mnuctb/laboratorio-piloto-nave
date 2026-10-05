// =============================================================
// PROTOCOL A - Delimited plain text (CSV)
// Message format: b1,b2,b3,b4,pot\n   (example: 1,0,1,0,512)
//   b1..b4 -> buttons (1 = pressed, 0 = released)
//   pot    -> potentiometer value (0 to 1023)
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
    else if (comando == 'P') enviarLectura();            // answer the ping right away
    else if (comando == 'V') enviarMensaje(1, 0, 1, 0, 512); // fixed test value
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
  int b[4];
  for (int i = 0; i < 4; i++) {
    b[i] = (digitalRead(botonPins[i]) == LOW) ? 1 : 0;
  }
  int pot = analogRead(potPin);
  enviarMensaje(b[0], b[1], b[2], b[3], pot);
}

// Builds and sends one CSV line
void enviarMensaje(int b1, int b2, int b3, int b4, int pot) {
  Serial.print(b1); Serial.print(",");
  Serial.print(b2); Serial.print(",");
  Serial.print(b3); Serial.print(",");
  Serial.print(b4); Serial.print(",");
  Serial.println(pot);   // println adds the line break that ends the message
}