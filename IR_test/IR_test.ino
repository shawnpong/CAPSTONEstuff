#define KY022_PIN 11  // connect OUT pin here

void setup() {
  Serial.begin(115200);
  pinMode(KY022_PIN, INPUT);
}

void loop() {
  int state = digitalRead(KY022_PIN);
  
  if(state == LOW) {  // KY-022 output is LOW when signal received
    Serial.println("IR signal detected!");
  } else {
    Serial.println("No IR signal");
  }
  
  delay(100);  // check 10 times per second
}
