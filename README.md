## Repository Files For Comms

| File | Role |
|------|------|
| `tcp_unity.py` | Laptop bridge forwarding AI prediction → FireBeetle & Unity |
| `moreonfb.py` | IMU TCP server + MQTT publisher + XOR command relay |

---

## Requirements

### Python
```bash
pip install paho-mqtt pycryptodome
```

---
## Network Configuration:
  Requirement: All devices must be on the same network and use matching static/local IPs
  
  Parameters:
  
    broker_ip: 
      example: "172.17.183.135"  
      used_in: [ "moreonfb.py", "tcp_unity.py" ]
    firebeetle_ip:
      example: [ "172.20.10.10", "172.20.10.4" ]
      used_in: [ "moreonfb.py", "tcp_unity.py" ]
    unity_ip:
      example: "172.20.10.3"
      used_in: [ "moreonfb.py", "tcp_unity.py" ]
    ports:
      tcp: [ 4210, 5000, 5001, 6000, 6001 ]
      note: "Must match for both sender and receiver scripts"
  

---

## Run Instructions:
  **Step 1: Start MQTT Broker in WSL**
  ```
  mosquitto -c /etc/mosquitto/mosquitto.conf
  ```
    
  **Step 2: Start IMU Server + Command Handler**
  ```
  python3 moreonfb.py
  ```

  
  Expected Output:
  
    TCP server listening on 0.0.0.0:4210   
    Received AES-encrypted IMU data...
    Published 120 bytes to robot/sensor/to_ultra96
        
  **Step 3: Start Laptop Prediction Bridge**
    
    python tcp_unity.py
    
  Expected Output:
  
    Laptop bridge starting...
    Subscribed to Ultra96 topic
    Movement class: 3
    Sent to Unity: <encrypted bytes>
    Sent to FireBeetle: <xor bytes>
    
## Repository Files For Hardware
- `fbglove` — Glove/hand sensing peripheral
- `fbcar` — FireBeetle-based car/robot controller

fbglove
- Path: `Hardware/fbglove/fbglove.ino`
- Purpose: Read sensors (IMU, flex sensors, etc.) and forward processed data to the laptop/robot communications stack.
- Quick start:
  1. Open `fbglove.ino` in the Arduino IDE or PlatformIO.
  2. Install any libraries referenced by the sketch (check the top of the file for `#include` directives).
  3. Select the correct FireBeetle/ESP board type and the serial/USB port.
  4. Upload the sketch.
  5. Open the Serial Monitor (baud set by `Serial.begin(...)` inside the sketch) to observe boot and sensor output.
- Notes:
  - Confirm that any network settings (SSID, passwords, static IPs) and MQTT parameters match the values used by `Comms/` scripts.
  - If the sketch uses encryption or custom framing (AES/XOR), ensure the keys/params match the receiving scripts.

fbcar
- Path: `Hardware/fbcar/fbcar.ino`
- Purpose: Receive movement/actuation commands and drive motors/servos for the robot/car.
- Quick start:
  1. Open `fbcar.ino` in the Arduino IDE or PlatformIO.
  2. Install any motor/sensor libraries used by the sketch.
  3. Wire motors, drivers, and sensors per the comments at the top of the sketch or the pin definitions inside the file.
  4. Upload the sketch and open the Serial Monitor for status messages.
- Notes:
  - Double-check motor driver power supply and signal levels before powering motors.
  - Verify network/MQTT configuration if the sketch connects over Wi-Fi.
