import paho.mqtt.client as mqtt
import json
import time
import struct
from datetime import datetime
import ssl
import csv
import os
import threading
import sys
import termios
import tty

CSV_FILE = "sijia_class_6.csv"

header = ["t"]
for imu_id in range(5):
    header += [
        f"IMU{imu_id}_ax", f"IMU{imu_id}_ay", f"IMU{imu_id}_az",
        f"IMU{imu_id}_gx", f"IMU{imu_id}_gy", f"IMU{imu_id}_gz"
    ]

# buffer in RAM (we only write after Z key pressed)
capture_buffer = []
is_capturing = False


def getch():
    """Read one character without pressing enter"""
    fd = sys.stdin.fileno()
    old = termios.tcgetattr(fd)
    try:
        tty.setraw(fd)
        return sys.stdin.read(1)
    finally:
        termios.tcsetattr(fd, termios.TCSADRAIN, old)

def keyboard_listener():
    global is_capturing, capture_buffer

    print("\n======= KEYBOARD CONTROL =======")
    print("Press  q   start capturing IMU data")
    print("Press  z   stop and write CSV")
    print("Press  x   exit program")
    print("================================\n")

    fd = sys.stdin.fileno()
    old_settings = termios.tcgetattr(fd)

    try:
        tty.setraw(fd)

        while True:
            ch = sys.stdin.read(1)

            if ch.lower() == 'q':
                is_capturing = True
                capture_buffer = []
                print("\n✅ CAPTURE STARTED (press z to stop)\n")

            elif ch.lower() == 'z':
                is_capturing = False
                print("\nSTOPPED — writing CSV...\n")
                write_csv()
                print(f"Saved {len(capture_buffer)} samples → {CSV_FILE}\n")

            elif ch.lower() == 'x':
                print("\nEXITING PROGRAM...\n")
                break

    finally:
        termios.tcsetattr(fd, termios.TCSADRAIN, old_settings)
        os._exit(0)



def write_csv():
    new_file = not os.path.exists(CSV_FILE)

    with open(CSV_FILE, "a", newline="") as f:
        writer = csv.writer(f)

        # write header only if file didn't exist
        if new_file:
            writer.writerow(header)

        writer.writerows(capture_buffer)


class Ultra96CSVLogger:
    def __init__(self):
        self.MQTT_BROKER = "localhost"
        self.MQTT_PORT = 8883

        self.topic_sensor_to_ultra96 = "robot/sensor/to_ultra96"

        self.TLS_CA = "/etc/mosquitto/certs/ca.crt"
        self.TLS_CERT = "/etc/mosquitto/certs/ultra96.crt"
        self.TLS_KEY = "/etc/mosquitto/certs/ultra96.key"

        self.client = mqtt.Client(client_id="ultra96_csv_logger")
        self.client.tls_set(
            ca_certs=self.TLS_CA,
            certfile=self.TLS_CERT,
            keyfile=self.TLS_KEY,
            tls_version=ssl.PROTOCOL_TLSv1_2
        )
        self.client.tls_insecure_set(True)

        self.client.on_connect = self.on_connect
        self.client.on_message = self.on_message

    def on_connect(self, client, userdata, flags, rc):
        print(f"✅ MQTT connected ({rc})")
        client.subscribe(self.topic_sensor_to_ultra96)
        print(f"📡 Subscribed: {self.topic_sensor_to_ultra96}")

    @staticmethod
    def parse_frames(raw_data):
        if len(raw_data) % 24 != 0:
            print(f"[WARN] Bad payload size: {len(raw_data)}")
            return []

        frames = []
        offset = 0
        total_imus = len(raw_data) // 24
        num_frames = total_imus // 5

        for _ in range(num_frames):
            frame = []
            for _ in range(5):
                imu_values = struct.unpack("!6f", raw_data[offset:offset + 24])
                offset += 24
                frame.append(imu_values)
            frames.append(frame)

        return frames

    def on_message(self, client, userdata, msg):
        global is_capturing, capture_buffer

        raw_data = msg.payload
        frames = self.parse_frames(raw_data)

        if not frames:
            return

        if not is_capturing:
            return

        # Save frames to RAM buffer
        for frame in frames:
            timestamp = datetime.now().isoformat()
            row = [timestamp]

            for imu_vals in frame:
                row += [f"{v:.6f}" for v in imu_vals]

            capture_buffer.append(row)

        print(f"[BUFFER] +{len(frames)} frames (total={len(capture_buffer)})")

    def start(self):
        self.client.connect(self.MQTT_BROKER, self.MQTT_PORT, 60)
        self.client.loop_start()


# MAIN
if __name__ == "__main__":
    print("="*60)
    print(" Ultra96 — CSV Data Capture (Press Q to start, Z to save)")
    print("="*60)

    logger = Ultra96CSVLogger()

    # start MQTT in background
    logger.start()

    # start keyboard listener (runs forever)
    keyboard_listener()
