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
      used_in: [ "moreonfb.py", "tcp_unity.py", "fbserver.ino" ]
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
    
