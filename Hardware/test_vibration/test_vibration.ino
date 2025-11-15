const int motorPin = 3; 

void setup() {
  pinMode(motorPin, OUTPUT);
}

void loop() {
  digitalWrite(motorPin, HIGH);
  delay(1000); // Vibrate for 1 second

  digitalWrite(motorPin, LOW);
  delay(1000); // Stay off for 1 second

  // Example of controlling intensity using PWM (AnalogWrite)
  // This requires connecting the motor module to a PWM pin (e.g., pin 9)
  // analogWrite(motorPin, 150); // Vibrate at medium intensity (0-255)
  // delay(1000);
  // analogWrite(motorPin, 0); // Turn off
  // delay(1000);
}
