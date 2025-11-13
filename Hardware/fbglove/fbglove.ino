  #include <Wire.h>
  #include <WiFi.h>
  #include <AES.h>
  #include "MPU6050.h"
  #include <base64.h>

  #define NUM_IMU 5
  #define TCA_ADDR 0x70
  #define FUEL_ADDR 0x36
  #define MOTOR_PIN 2   // vibration motor pin
  #define AES_BLOCK_SIZE 16
  #define N_SETS_PER_PACKET 4   

  MPU6050 imu[NUM_IMU];

  // Conversion factors
  #define ACCEL_SCALE 16384.0
  #define GYRO_SCALE 131.0

  float accelOffset[NUM_IMU][3] = {0};
  float gyroOffset[NUM_IMU][3]  = {0};

  // WiFi
  const char* ssid = "iPhone";
  const char* password = "zhuqshenw";

  // === IMU DATA SENDING CLIENT (to laptop) ===
  // RENAMED 'client' to 'dataClient' for clarity
  WiFiClient dataClient;
  const char* laptop_ip = "172.20.10.6"; 
  const int data_port = 4210; // Port for sending IMU data

  // === COMMAND RECEIVING SERVER (from laptop) ===
  // NEW: Listening on port 5001 to avoid conflict with fbcar (5000)
  WiFiServer commandServer(5001);
  WiFiClient commandClient;
  bool commandClientConnected = false;

  // --- NEW TOGGLE FOR COMMAND CHANNEL ---
  bool isCommandChannelActive = true; // Default: Command channel is enabled

  // --- NEW TOGGLE AND TIMING FOR BATTERY PRINT ---
  bool isBatteryPrintEnabled = true; // Default: Battery print is enabled
  unsigned long lastBatteryPrintTime = 0;
  const long batteryPrintInterval = 5000; // Print every 5 seconds

  // AES
  AES aes;
  byte aes_key[16] = {0x2B,0x7E,0x15,0x16,0x28,0xAE,0xD2,0xA6,
                      0xAB,0xF7,0x15,0x88,0x09,0xCF,0x4F,0x3C};
  byte aes_iv[16]  = {0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,
                      0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F};

  // === Motor buzz parameters ===
  #define BUZZ_DEFAULT 500  // Standard buzz duration 
  #define BUZZ_SHORT 120    // Standard short acknowledgement buzz for commands 1-6

  bool motorBuzzing = false;
  unsigned long motorStartTime = 0;
  unsigned int currentBuzzDuration = BUZZ_DEFAULT; 

  // Last periodic buzz time (logic removed, but variable kept if needed later)
  unsigned long lastBuzzTime = 0;

  // Helper: select TCA channel
  void tcaSelect(uint8_t channel) {
    if (channel > 7) return;
    Wire.beginTransmission(TCA_ADDR);
    Wire.write(1 << channel);
    Wire.endTransmission();
  }

  // === MAX17043 fuel gauge read voltage ===
  float readFuelVoltage() {
    tcaSelect(5); // channel for fuel gauge
    Wire.beginTransmission((uint8_t)FUEL_ADDR);
    Wire.write(0x02); // VCELL register
    Wire.endTransmission(false);
    
    // Explicitly casting for robustness
    uint8_t bytesReceived = Wire.requestFrom((uint8_t)FUEL_ADDR, (uint8_t)2, (bool)true);

    if (bytesReceived == 2) {
      uint8_t msb = Wire.read();
      uint8_t lsb = Wire.read();
      uint16_t raw = (msb << 8) | lsb;
      float voltage = (raw >> 4) * 0.00125; // volts
      return voltage;
    }
    return 0.0; // Return 0 on read failure
  }

  // === MAX17043 fuel gauge read percentage ===
  float readFuelPercentage() {
    tcaSelect(5); // channel for fuel gauge
    Wire.beginTransmission((uint8_t)FUEL_ADDR);
    Wire.write(0x04); // SOC register
    Wire.endTransmission(false);
    
    // Explicitly casting for robustness
    uint8_t bytesReceived = Wire.requestFrom((uint8_t)FUEL_ADDR, (uint8_t)2, (bool)true);

    if (bytesReceived == 2) {
      uint8_t msb = Wire.read();
      uint8_t lsb = Wire.read();
      uint16_t raw = (msb << 8) | lsb;
      float soc = (raw >> 8); // upper byte is percentage (0-100)
      return soc;
    }
    return 0.0; // Return 0 on read failure
  }

  // === Motor control functions ===

  // Flexible function to start the motor buzz for a custom duration
  void startMotorBuzz(unsigned int duration) {
    // If the command channel is disabled, this code block won't be reached
    // for commands 1-6, but we keep the local state check for safety.
    
    // Only start the buzz if it's not currently buzzing
    if (!motorBuzzing) {
      currentBuzzDuration = duration;
      digitalWrite(MOTOR_PIN, HIGH);
      motorStartTime = millis();
      motorBuzzing = true;
      Serial.println("✅ Motor ON for " + String(duration) + "ms");
    }
  }

  // Non-blocking function to turn the motor off after its duration
  void updateMotorBuzz() {
    if(motorBuzzing && millis() - motorStartTime >= currentBuzzDuration){
      digitalWrite(MOTOR_PIN, LOW);
      motorBuzzing = false;
      // Serial.println("Motor OFF"); 
    }
  }

  // PKCS7 + AES CBC + Base64
  String encryptData(String plaintext) {
    int inputLength = plaintext.length();
    byte input[inputLength + 1]; // +1 for null terminator
    plaintext.getBytes(input, inputLength + 1);

    int paddedLength = ((inputLength + AES_BLOCK_SIZE) / AES_BLOCK_SIZE) * AES_BLOCK_SIZE;
    byte paddedInput[paddedLength];
    memcpy(paddedInput, input, inputLength);
    
    // PKCS7 padding
    byte padValue = paddedLength - inputLength;
    for (int i = inputLength; i < paddedLength; i++) {
      paddedInput[i] = padValue;
    }

    byte encrypted[paddedLength];
    aes.set_key(aes_key, 16);

    // Copy IV to avoid modifying the original
    byte iv_copy[16];
    memcpy(iv_copy, aes_iv, 16);
    
    // Encrypt
    aes.cbc_encrypt(paddedInput, encrypted, paddedLength / 16, iv_copy);

    String encoded = base64::encode(encrypted, paddedLength);
    return encoded;
  }

  // --- NEW FUNCTION TO HANDLE PERIODIC BATTERY PRINT ---
  void handleBatteryPrint(float voltage, float percent) {
    if (isBatteryPrintEnabled && (millis() - lastBatteryPrintTime >= batteryPrintInterval)) {
      Serial.print("🔋 Battery Status (V/%) | ");
      Serial.print(voltage, 3);
      Serial.print("V | ");
      Serial.print(percent, 0);
      Serial.println("%");
      lastBatteryPrintTime = millis();
    }
  }

  void setup() {
    Serial.begin(115200);
    Wire.begin();
    
    // Initialize motor pin
    pinMode(MOTOR_PIN, OUTPUT);
    digitalWrite(MOTOR_PIN, LOW);
    
    WiFi.begin(ssid,password);
    while(WiFi.status()!=WL_CONNECTED){ delay(500); Serial.print("."); }
    Serial.println("\n✅ WiFi connected. IP: " + WiFi.localIP().toString());

    // === 1. IMU Data Client Connection ===
    // Use dataClient to send IMU data to the laptop (4210)
    if (!dataClient.connect(laptop_ip, data_port)) Serial.println("❌ IMU Data Client connect failed");
    else Serial.println("✅ IMU Data Client connected to laptop at " + String(laptop_ip) + ":" + String(data_port));

    // === 2. Command Server Initialization ===
    // Start the server to receive buzz commands from the laptop (5001)
    commandServer.begin();
    Serial.println("📡 Command Server started on port 5001");

    // Initialize IMUs
    for (int i=0;i<NUM_IMU;i++){
      tcaSelect(i);
      imu[i].initialize();
      if (imu[i].testConnection()) Serial.print("IMU "); Serial.print(i); Serial.println(" connected");
    }
  }

  void loop() {
    // === 1. COMMAND RECEIVING SERVER MANAGEMENT ===
    
    if (isCommandChannelActive) {
      // Handle new client connections (Laptop connecting to Glove's server)
      if (!commandClientConnected) {
        commandClient = commandServer.available();
        if (commandClient) {
          commandClientConnected = true;
          Serial.println("✅ Command Client connected (Laptop -> Glove)");
        }
      }

      // Check for and process incoming commands
      if (commandClientConnected && commandClient.connected()) {
        if (commandClient.available()) {
          // Read the incoming UNENCRYPTED command line
          String command = commandClient.readStringUntil('\n');
          command.trim(); // Remove any leading/trailing whitespace

          // --- Channel Toggle Commands ---
          if (command.equalsIgnoreCase("CHANNEL_OFF")) {
            isCommandChannelActive = false;
            commandClient.stop(); // Immediately close the connection
            commandClientConnected = false;
            digitalWrite(MOTOR_PIN, LOW); // Ensure motor is off
            motorBuzzing = false;
            Serial.println("🔴 COMMAND CHANNEL DISABLED. Connection closed.");
          } 
          else if (command.equalsIgnoreCase("CHANNEL_ON")) {
            // CHANNEL_ON command only takes effect if the channel was already active
            Serial.println("🟢 COMMAND CHANNEL IS ACTIVE.");
          }
          // --- NEW Battery Print Toggle Commands ---
          else if (command.equalsIgnoreCase("BAT_PRINT_ON")) {
            isBatteryPrintEnabled = true;
            Serial.println("🟢 BATTERY PRINTING ENABLED.");
          }
          else if (command.equalsIgnoreCase("BAT_PRINT_OFF")) {
            isBatteryPrintEnabled = false;
            Serial.println("🔴 BATTERY PRINTING DISABLED.");
          }
          // ----------------------------------

          // --- Unified short buzz for commands 1-6, no buzz for 0 ---
          else if (command.equalsIgnoreCase("come_here") || command.equalsIgnoreCase("forward") || command.equalsIgnoreCase("come") || command == "1" ||
              command.equalsIgnoreCase("go_away") || command.equalsIgnoreCase("backward") || command.equalsIgnoreCase("go") || command == "2" ||
              command.equalsIgnoreCase("turn_around") || command.equalsIgnoreCase("turn") || command == "3" ||
              command.equalsIgnoreCase("pet") || command.equalsIgnoreCase("4") ||
              command.equalsIgnoreCase("feed") || command.equalsIgnoreCase("5") ||
              command.equalsIgnoreCase("throw_ball") || command.equalsIgnoreCase("throw") || command == "6")
          {
            Serial.println("📢 Received movement/action command (1-6). Buzzing SHORT!");
            startMotorBuzz(BUZZ_SHORT);
          } 
          else if (command.equalsIgnoreCase("stop") || command == "0") {
            Serial.println("📢 Received STOP command (0). No buzz.");
          }
          else if (command.equalsIgnoreCase("BUZZ")) {
            // Legacy BUZZ command for direct control
            Serial.println("📢 Received BUZZ (legacy) command!");
            startMotorBuzz(BUZZ_DEFAULT); 
          } 
          else if (command.length() > 0) {
            Serial.println("❌ Unknown command: '" + command + "'");
            Serial.println("💡 Available commands: 0-6, BUZZ, CHANNEL_OFF, CHANNEL_ON, BAT_PRINT_ON, BAT_PRINT_OFF or keywords");
          }
          
          // Send an ACK back to the laptop on the command client
          if (commandClient.connected()) {
            commandClient.println("ACK:Command received");
          }
        }
      }
      
      // Handle command client disconnection
      if (commandClientConnected && !commandClient.connected()) {
        commandClient.stop();
        commandClientConnected = false;
        Serial.println("⚠️ Command Client disconnected");
      }
    } else {
      // Channel is disabled, so we are not listening for new commands.
      // Allow re-enable via Serial Monitor
      if (Serial.available()) {
        String serialCmd = Serial.readStringUntil('\n');
        serialCmd.trim();
        if (serialCmd.equalsIgnoreCase("CHANNEL_ON")) {
          isCommandChannelActive = true;
          Serial.println("🟢 COMMAND CHANNEL RE-ENABLED via Serial.");
        }
      }
    }

    // === 2. IMU DATA SENDING CLIENT MANAGEMENT ===
    String packet = "";

    for (int set = 0; set < N_SETS_PER_PACKET; set++) {
      for (int i = 0; i < NUM_IMU; i++) {
        tcaSelect(i);
        int16_t ax, ay, az, gx, gy, gz;
        imu[i].getMotion6(&ax, &ay, &az, &gx, &gy, &gz);

        float ax_g = ax / ACCEL_SCALE;
        float ay_g = ay / ACCEL_SCALE;
        float az_g = az / ACCEL_SCALE;
        float gx_dps = gx / GYRO_SCALE;
        float gy_dps = gy / GYRO_SCALE;
        float gz_dps = gz / GYRO_SCALE;

        packet += "IMU" + String(i) + ":";
        packet += String(ax_g, 3) + "," + String(ay_g, 3) + "," + String(az_g, 3) + ",";
        packet += String(gx_dps, 3) + "," + String(gy_dps, 3) + "," + String(gz_dps, 3) + ";";
      }
    }

    // Add battery data to the packet
    float voltage = readFuelVoltage();
    float percent = readFuelPercentage();
    packet += "Fuel:" + String(voltage, 3) + "V," + String(percent, 0) + "%;";
    
    // --- NEW: Handle periodic battery print after reading data ---
    handleBatteryPrint(voltage, percent);

    String encrypted = encryptData(packet);
    if (dataClient.connected()) { // Use dataClient for sending
      dataClient.write(encrypted.c_str(), encrypted.length());
      dataClient.write("\n");
      // Serial.println("📤 Sent " + String(N_SETS_PER_PACKET) + " sets of data + Battery");
    } else {
      // Attempt reconnection if disconnected
      if (dataClient.connect(laptop_ip, data_port)) // Use dataClient for reconnecting
        Serial.println("✅ Reconnected to IMU Data Server");
    }

    // === 3. Motor Management (CRITICAL) ===
    // Checks if the motor's current buzz duration has finished and turns it off.
    updateMotorBuzz(); 

    delay(10);
  }