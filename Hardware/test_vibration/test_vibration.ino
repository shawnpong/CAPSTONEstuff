// Define the pin where the vibration motor module is connected
const int motorPin = 3; // Use a PWM-capable pin for intensity control (e.g., 3, 5, 6, 9, 10, 11 on Uno)

void setup() {
  // Set the motor pin as an output
  pinMode(motorPin, OUTPUT);
}

void loop() {
  // Turn the motor on at full intensity
  digitalWrite(motorPin, HIGH);
  delay(1000); // Vibrate for 1 second

  // Turn the motor off
  digitalWrite(motorPin, LOW);
  delay(1000); // Stay off for 1 second

  // Example of controlling intensity using PWM (AnalogWrite)
  // This requires connecting the motor module to a PWM pin (e.g., pin 9)
  // analogWrite(motorPin, 150); // Vibrate at medium intensity (0-255)
  // delay(1000);
  // analogWrite(motorPin, 0); // Turn off
  // delay(1000);
}
