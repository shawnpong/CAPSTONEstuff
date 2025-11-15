import paho.mqtt.client as mqtt
import json
import time
import struct
from datetime import datetime
import ssl
import numpy as np
from collections import deque
from queue import Queue, Empty
from threading import Thread
from typing import Optional
from pynq import Overlay, allocate


# Ultra96 Hardware CNN Runner
class Ultra96CNNRunner:
    def __init__(
        self,
        bitfile="cnn_overlay_11.xsa",
        meta_path="artifacts_tf/meta.json",
        pca_npz="artifacts_tf/pca_params_summarizer.npz",
        cnn_ip_name="cnn1d_ip_0",
        dma_name="axi_dma_0",
    ):
        print("[AI] Loading overlay...")
        self._ol = Overlay(bitfile)
        self._ol.download()
        print("[AI] Overlay loaded:", bitfile)

        # Hardware handles
        self._cnn = getattr(self._ol, cnn_ip_name)
        self._dma = getattr(self._ol, dma_name)

        # Arm CNN once -> AP_START | AUTO_RESTART
        ctrl_before = self._cnn.read(0x00)
        self._cnn.write(0x00, 0x81)
        ctrl_after = self._cnn.read(0x00)
        print(f"[AI] CNN CTRL before=0x{ctrl_before:08X}, after=0x{ctrl_after:08X}")

        # Load preprocessing metadata
        with open(meta_path, "r") as f:
            meta = json.load(f)

        self.WINDOW = int(meta["window"])
        self.NUM_SEGMENTS = int(meta["num_segments"])
        self.STATS_LIST = list(meta["stats_list"])
        self.D_IN = int(meta["D_pca"])
        self.CLASSES = int(meta["classes"])
        self.class_names = list(meta.get("class_names", [str(i) for i in range(self.CLASSES)]))

        # PCA and scaler
        npz = np.load(pca_npz)
        self.scaler_mean = npz["scaler_mean"]
        self.scaler_scale = npz["scaler_scale"]
        self.pca_components = npz["pca_components"]
        self.pca_mean = npz["pca_mean"]

        print(f"[AI] Model expects D_IN={self.D_IN}, CLASSES={self.CLASSES}, WINDOW={self.WINDOW}")

        # Sliding window
        self.FEATS_PER_ROW = 30
        self._buf = deque(maxlen=self.WINDOW)

        # Tracks when each full window is completed
        self.window_ready_timestamp = None

        # DMA buffers
        self._in_buf = allocate(shape=(self.D_IN,), dtype=np.float32)
        self._out_buf = allocate(shape=(self.CLASSES,), dtype=np.float32)

        # Debounce and cooldown logic
        self.last_raw_pred = 0
        self.prev_filtered_pred = 0
        self.cooldown_until = 0

        self.HOP = 4

    def window_ready(self):
        return len(self._buf) >= self.WINDOW

    def push_row(self, row_30: np.ndarray):
        if row_30.shape[0] != self.FEATS_PER_ROW:
            raise ValueError("Row must have 30 features")

        # Each time the window is already full, a new row creates a new window
        if self.window_ready():
            self.window_ready_timestamp = datetime.now()

        self._buf.append(row_30.astype(np.float32))

        # First time buffer becomes full
        if len(self._buf) == self.WINDOW and self.window_ready_timestamp is None:
            self.window_ready_timestamp = datetime.now()

    @staticmethod
    def readings_to_row(sensor_readings):
        row = []
        for i in range(5):
            imu = next((r for r in sensor_readings if r.get("sensor_id") == i), None)
            if imu:
                acc = imu["acceleration"]
                gyr = imu["gyroscope"]
                row.extend([
                    float(acc["x"]), float(acc["y"]), float(acc["z"]),
                    float(gyr["x"]), float(gyr["y"]), float(gyr["z"]),
                ])
            else:
                row.extend([0.0] * 6)
        return np.array(row, dtype=np.float32)

    # Compute statistical features across segments
    def _summarize_window(self, win_2d):
        W = win_2d.shape[0]
        feats = []
        for s in range(self.NUM_SEGMENTS):
            a = round(s     * W / self.NUM_SEGMENTS)
            b = round((s+1) * W / self.NUM_SEGMENTS)
            seg = win_2d[a:b]
            parts = []
            if "mean" in self.STATS_LIST:   parts.append(seg.mean(axis=0))
            if "std" in self.STATS_LIST:    parts.append(seg.std(axis=0) + 1e-8)
            if "p2p" in self.STATS_LIST:    parts.append(seg.max(axis=0) - seg.min(axis=0))
            if "energy" in self.STATS_LIST: parts.append((seg**2).sum(axis=0))
            feats.append(np.concatenate(parts))
        return np.stack(feats).astype(np.float32)

    # PCA logic
    def _apply_scaler_pca(self, flat):
        flat = flat.reshape(-1)
        flat_scaled = (flat - self.scaler_mean) / self.scaler_scale
        flat_centered = flat_scaled - self.pca_mean
        return flat_centered.dot(self.pca_components.T).astype(np.float32)

    @staticmethod
    def _softmax(v):
        v = v - np.max(v)
        e = np.exp(v).astype(np.float32)
        return e / (np.sum(e) + 1e-9)

    # Full inference pipeline
    def infer_once(self):
        if not self.window_ready():
            return None

        win = np.stack(self._buf, axis=0)
        seg = self._summarize_window(win)
        xvec = self._apply_scaler_pca(seg.reshape(-1))
        np.copyto(self._in_buf, xvec)

        try:
            self._dma.recvchannel.transfer(self._out_buf)
            self._dma.sendchannel.transfer(self._in_buf)
            self._dma.sendchannel.wait()
            self._dma.recvchannel.wait()
        except Exception as e:
            print("[ERR] DMA:", e)
            return None

        logits = np.copy(self._out_buf)
        probs = self._softmax(logits)

        raw_pred = int(np.argmax(probs))
        max_prob = float(probs[raw_pred])

        # Confidence threshold
        if raw_pred != 0 and max_prob < 0.5:
            raw_pred = 0

        # Two consecutive rule
        confirmed = raw_pred if (raw_pred != 0 and raw_pred == self.last_raw_pred) else 0
        self.last_raw_pred = raw_pred

        # Cooldown
        now = time.time()
        if confirmed != 0:
            if now < self.cooldown_until:
                confirmed = 0
            else:
                self.cooldown_until = now + 3

        self.prev_filtered_pred = confirmed
        return confirmed

    def close(self):
        try: self._in_buf.freebuffer()
        except: pass
        try: self._out_buf.freebuffer()
        except: pass



# MQTT Subscriber
class Ultra96MQTTSubscriber:
    def __init__(self):
        self.session_counter = 1000

        self.MQTT_BROKER = "localhost"
        self.MQTT_PORT = 8883

        self.topic_sensor_to_ultra96 = "robot/sensor/to_ultra96"
        self.topic_processed_data = "ultra96/processed/to_firebeetle"

        self.TLS_CA   = "/etc/mosquitto/certs/ca.crt"
        self.TLS_CERT = "/etc/mosquitto/certs/ultra96.crt"
        self.TLS_KEY  = "/etc/mosquitto/certs/ultra96.key"

        self.client = mqtt.Client(client_id="ultra96_subscriber_tls", userdata=self)
        self.client.tls_set(
            ca_certs=self.TLS_CA,
            certfile=self.TLS_CERT,
            keyfile=self.TLS_KEY,
            tls_version=ssl.PROTOCOL_TLSv1_2,
        )
        self.client.tls_insecure_set(True)

        self.client.on_connect = self.on_connect
        self.client.on_message = self.on_message

        self.work_queue = Queue(maxsize=10)
        Thread(target=self._worker_loop, daemon=True).start()

        self.ai = Ultra96CNNRunner()

    def on_connect(self, client, userdata, flags, rc):
        print("Connected." if rc == 0 else f"MQTT connect failed {rc}")
        client.subscribe(self.topic_sensor_to_ultra96)

    @staticmethod
    def parse_packet_exact_4_frames(raw):
        expected = 4 * 5 * 6 * 4
        if len(raw) != expected:
            return None, f"Invalid size {len(raw)}, expected {expected}"
        frames = []
        off = 0
        for _ in range(4):
            frame = []
            for imu_id in range(5):
                vals = struct.unpack("!6f", raw[off:off+24])
                off += 24
                frame.append({
                    "sensor_id": imu_id,
                    "acceleration": {"x": vals[0], "y": vals[1], "z": vals[2]},
                    "gyroscope":    {"x": vals[3], "y": vals[4], "z": vals[5]},
                })
            frames.append(frame)
        return frames, None

    def on_message(self, client, userdata, msg):
        frames, err = self.parse_packet_exact_4_frames(msg.payload)
        if err:
            print("[ERR]", err)
            return

        while not self.work_queue.empty():
            try: self.work_queue.get_nowait()
            except Empty: break

        self.work_queue.put_nowait((frames, self.session_counter))
        self.session_counter += 1

    # Main processing worker
    def _worker_loop(self):
        while True:
            try:
                frames, sess = self.work_queue.get(timeout=1.0)
            except Empty:
                continue

            for sensor_readings in frames:
                self.ai.push_row(self.ai.readings_to_row(sensor_readings))
                pred = self.ai.infer_once()

                if pred and pred != 0:

                    ts_ready = self.ai.window_ready_timestamp
                    ts_ready_str = ts_ready.strftime("%H:%M:%S.%f")[:-3] if ts_ready else "N/A"

                    ts_pred = datetime.now()
                    ts_pred_str = ts_pred.strftime("%H:%M:%S.%f")[:-3]

                    if ts_ready:
                        latency_ms = (ts_pred - ts_ready).total_seconds() * 1000
                        latency_str = f"{latency_ms:.1f} ms"
                    else:
                        latency_ms = None
                        latency_str = "N/A"

                    print(f"[PRED] win_ready={ts_ready_str}  pred={ts_pred_str}  "
                          f"latency={latency_str}  → class={pred}")

                    payload = {
                        "session": sess,
                        "prediction": int(pred),
                        "window_ready_time": ts_ready.isoformat() if ts_ready else None,
                        "prediction_time": ts_pred.isoformat(),
                        "latency_ms": latency_ms,
                        "status": "success"
                    }
                    self.client.publish(self.topic_processed_data, json.dumps(payload), qos=1)

    def start(self):
        print("Connecting to MQTT broker...")
        attempt = 0
        while attempt < 1000:
            try:
                self.client.connect(self.MQTT_BROKER, self.MQTT_PORT, 60)
                print("Connected.")
                break
            except Exception as e:
                attempt += 1
                print(f"[MQTT] connect failed {attempt}/1000: {e}")
                time.sleep(10)

        self.client.loop_start()
        print("MQTT loop started.")

        try:
            while True:
                time.sleep(1)
        except KeyboardInterrupt:
            print("Shutdown requested.")
        finally:
            self.client.loop_stop()
            self.client.disconnect()
            self.ai.close()



if __name__ == "__main__":
    print("=" * 60)
    print(" Ultra96 MQTT Subscriber + CNN (latency-tracked)")
    print("=" * 60)
    Ultra96MQTTSubscriber().start()
