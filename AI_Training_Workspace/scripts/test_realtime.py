"""
=============================================================================
PBL4 - AIoT Multimodal Fire Safety System
Script: test_realtime.py
Task:   [AI-01.2] - Kiem thu real-time inference + Multi-Sensor Fusion

Boi canh du an:
    Mo hinh YOLO bi Domain Gap: bao gia (toc den/rem -> khoi) va bo sot
    khoi mong/lua nho. Giai phap: fine-tune voi Custom Dataset -> bestnew.pt.
    Script nay la cong cu nghiem thu toan dien:
      - Threaded CameraReader: tranh buffer lag do YOLO inference cham hon FPS
      - GPU auto-detect: chay CUDA neu co, fallback CPU muot ma
      - Multi-Sensor Fusion stub: doc cam bien bat dong bo (khong blocking video)
      - FPS thuc te hien thi tren man hinh
      - Bounding box ro rang voi Track ID (ByteTrack)

Cach chay:
    # Kich hoat venv truoc
    venv\\Scripts\\activate

    # Chay script (tu thu muc AI_Training_Workspace)
    python scripts/test_realtime.py

    # Cac tuy chon:
    python scripts/test_realtime.py --source 0          # Webcam mac dinh
    python scripts/test_realtime.py --source 1          # Camera thu 2
    python scripts/test_realtime.py --conf 0.5          # Nguong confidence

Phim tat khi dang chay:
    [Q] hoac [ESC]  - Thoat chuong trinh
    [S]             - Chup snapshot luu vao snapshots/
    [+] / [-]       - Tang/giam nguong confidence 0.05
=============================================================================
"""

import sys
import cv2
import time
import threading
import traceback
import argparse
import math
from datetime import datetime
from pathlib import Path

if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

try:
    from ultralytics import YOLO
    _YOLO_AVAILABLE = True
except ImportError:
    _YOLO_AVAILABLE = False

try:
    import torch
    _TORCH_AVAILABLE = True
except ImportError:
    _TORCH_AVAILABLE = False


# =============================================================================
# CONFIGURATION
# =============================================================================

WORKSPACE_ROOT        = Path(__file__).parent.parent
MODEL_PATH            = WORKSPACE_ROOT / "models" / "weights" / "bestnew.pt"
SNAPSHOT_DIR          = WORKSPACE_ROOT / "snapshots"
DEFAULT_CONF          = 0.45
INFERENCE_SIZE        = 640
WINDOW_NAME           = "PBL4_RealTime"
ALERT_CLASSES         = {"fire", "smoke", "flame"}
# Thoi gian toi da (giay) cho phep camera warm-up truoc khi bao loi
CAMERA_WARMUP_TIMEOUT = 5.0

COLOR_GREEN  = (0,   255,  0)
COLOR_YELLOW = (0,   200, 255)
COLOR_RED    = (0,    80, 255)
COLOR_WHITE  = (255, 255, 255)
COLOR_PURPLE = (200, 100, 255)
COLOR_BLUE   = (255, 180,  60)


# =============================================================================
# MODULE 1: CAMERA READER THREAD (anti buffer-lag)
# =============================================================================

class CameraReader(threading.Thread):
    """
    Thread doc camera lien tuc, giu frame moi nhat trong bo nho.
    OpenCV VideoCapture co internal buffer (~3-5 frames). YOLO inference
    cham hon FPS camera -> frame cu tich luy -> lag/giat. Thread nay
    lien tuc drain buffer nen main thread luon lay frame moi nhat.
    """

    def __init__(self, source, width=1280, height=720):
        super().__init__(daemon=True, name="CameraReader")
        # Voi webcam index dung backend MAC DINH (de Windows tu chon MSMF/DSHOW).
        # Voi URL/file khong can chi dinh backend.
        self.cap = cv2.VideoCapture(source)
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, width)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
        # BUFFERSIZE=1: giu frame moi nhat, tranh lag buffer
        self.cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        self._ret        = False
        self._frame      = None
        self._lock       = threading.Lock()
        self._stop_event = threading.Event()
        # Set khi thread doc duoc frame dau tien thanh cong
        self._first_frame_event = threading.Event()

    def run(self):
        while not self._stop_event.is_set():
            ret, frame = self.cap.read()
            with self._lock:
                self._ret   = ret
                self._frame = frame
            # Thong bao cho main thread biet frame dau tien da san sang
            if ret and not self._first_frame_event.is_set():
                self._first_frame_event.set()
            if not ret:
                break

    def read(self):
        with self._lock:
            frame_copy = self._frame.copy() if self._frame is not None else None
            return self._ret, frame_copy

    def isOpened(self):
        return self.cap.isOpened()

    def get(self, prop_id):
        return self.cap.get(prop_id)

    def stop(self):
        self._stop_event.set()
        self.join(timeout=3.0)
        self.cap.release()


# =============================================================================
# MODULE 2: MULTI-SENSOR FUSION STUB (Async - Non-blocking)
# Thay the _read_sensor() bang doc Serial/MQTT/HTTP thuc te.
# =============================================================================

class SensorFusion(threading.Thread):
    """
    Doc va hop nhat du lieu da cam bien bat dong bo.
    Thread nay KHONG blocking luong video chinh.

    De tich hop cam bien that:
        - Serial: import serial; ser.readline()
        - MQTT: paho-mqtt subscribe callback
        - HTTP: requests.get(f"http://{ESP32_IP}/sensors")
    """

    def __init__(self, enabled=True, poll_interval=0.5):
        super().__init__(daemon=True, name="SensorFusion")
        self._enabled       = enabled
        self._poll_interval = poll_interval
        self._lock          = threading.Lock()
        self._stop_event    = threading.Event()
        self._data = {
            "temp_c": 0.0, "humidity": 0.0,
            "smoke": 0.0, "gas_ppm": 0.0,
            "co_ppm": 0.0, "updated": False,
        }

    def run(self):
        if not self._enabled:
            return
        while not self._stop_event.is_set():
            raw = self._read_sensor()
            with self._lock:
                self._data.update(raw)
                self._data["updated"] = True
            self._stop_event.wait(timeout=self._poll_interval)

    def _read_sensor(self):
        """
        *** THAY THE DAY BANG DOC CAM BIEN THUC TE ***
        Hien tai: Stub mo phong cam bien binh thuong.

        Vi du Serial (Arduino gui JSON: {"temp":35,"smoke":10,"gas":120,"co":5}):
            import serial, json
            line = self.ser.readline().decode("utf-8", errors="ignore").strip()
            return json.loads(line)
        """
        t = time.time()
        return {
            "temp_c":   25.0 + math.sin(t * 0.1) * 2,
            "humidity": 60.0 + math.cos(t * 0.05) * 5,
            "smoke":     5.0 + abs(math.sin(t * 0.2)) * 3,
            "gas_ppm":  100.0,
            "co_ppm":    10.0,
        }

    def get_data(self):
        with self._lock:
            return dict(self._data)

    def stop(self):
        self._stop_event.set()
        self.join(timeout=2.0)


# =============================================================================
# MODULE 3: OVERLAY RENDERER
# =============================================================================

_fps_history = []

def smooth_fps(instant_fps, n=15):
    _fps_history.append(instant_fps)
    if len(_fps_history) > n:
        _fps_history.pop(0)
    return sum(_fps_history) / len(_fps_history)


def draw_top_bar(frame, fps, source_label, device_label, conf):
    h, w = frame.shape[:2]
    overlay = frame.copy()
    cv2.rectangle(overlay, (0, 0), (w, 44), (15, 15, 15), -1)
    cv2.addWeighted(overlay, 0.65, frame, 0.35, 0, frame)
    cv2.putText(frame, f"FPS: {fps:.1f}", (10, 30),
                cv2.FONT_HERSHEY_SIMPLEX, 0.85, COLOR_GREEN, 2)
    cv2.putText(frame, f"SRC: {source_label}", (w // 2 - 130, 30),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, COLOR_YELLOW, 2)
    cv2.putText(frame, f"{device_label}  CONF:{conf:.2f}", (w - 270, 30),
                cv2.FONT_HERSHEY_SIMPLEX, 0.65, COLOR_PURPLE, 2)


def draw_sensor_panel(frame, sensor_data):
    if not sensor_data.get("updated", False):
        return
    h, w = frame.shape[:2]
    px, py = 10, h - 115
    lines = [
        f"Temp : {sensor_data['temp_c']:.1f} C",
        f"Smoke: {sensor_data['smoke']:.1f}/100",
        f"Gas  : {sensor_data['gas_ppm']:.0f} ppm",
        f"CO   : {sensor_data['co_ppm']:.0f} ppm",
    ]
    overlay = frame.copy()
    cv2.rectangle(overlay, (px - 4, py - 18),
                  (px + 215, py + len(lines) * 22 + 4), (15, 15, 15), -1)
    cv2.addWeighted(overlay, 0.6, frame, 0.4, 0, frame)
    cv2.putText(frame, "SENSORS", (px, py - 2),
                cv2.FONT_HERSHEY_SIMPLEX, 0.48, COLOR_BLUE, 1)
    for i, line in enumerate(lines):
        color = COLOR_RED if ("Temp" in line and sensor_data["temp_c"] > 50) else COLOR_WHITE
        cv2.putText(frame, line, (px, py + 16 + i * 22),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.55, color, 1)


def draw_alert(frame, alert):
    if not alert:
        return
    h, w = frame.shape[:2]
    cv2.rectangle(frame, (0, 0), (w - 1, h - 1), COLOR_RED, 6)
    cv2.putText(frame, "!!! FIRE / SMOKE DETECTED !!!",
                (w // 2 - 240, h - 18),
                cv2.FONT_HERSHEY_SIMPLEX, 1.0, COLOR_RED, 3)


def draw_hints(frame, conf):
    h, w = frame.shape[:2]
    for i, txt in enumerate(["[Q/ESC] Quit", "[S] Snapshot", "[+/-] Conf"]):
        cv2.putText(frame, txt, (w - 160, h - 58 + i * 22),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, (140, 140, 140), 1)


def check_alert(results):
    if not results or results[0].boxes is None:
        return False
    names = results[0].names
    for box in results[0].boxes:
        if any(kw in names.get(int(box.cls[0]), "").lower() for kw in ALERT_CLASSES):
            return True
    return False


# =============================================================================
# MAIN
# =============================================================================

def parse_args():
    parser = argparse.ArgumentParser(description="PBL4 Real-Time Fire/Smoke Detection")
    parser.add_argument("--source", default=0, help="Camera index or stream URL")
    parser.add_argument("--conf",   type=float, default=DEFAULT_CONF)
    parser.add_argument("--model",  default=str(MODEL_PATH))
    parser.add_argument("--no-sensor", action="store_true")
    parser.add_argument("--no-track",  action="store_true")
    return parser.parse_args()


def main():
    args = parse_args()
    try:
        source = int(args.source)
    except (ValueError, TypeError):
        source = args.source
    conf = args.conf

    print("=" * 65)
    print("  PBL4 Fire Safety - Real-Time Inference  [test_realtime.py]")
    print("=" * 65)

    # Device
    if _TORCH_AVAILABLE and torch.cuda.is_available():
        device       = "0"
        device_label = f"GPU:{torch.cuda.get_device_name(0)[:12]}"
    else:
        device       = "cpu"
        device_label = "CPU"
    print(f"\n[0/4] Device: {device_label}")

    # Model
    if not _YOLO_AVAILABLE:
        print("  [ERROR] pip install ultralytics")
        return
    model_path = args.model
    print(f"\n[1/4] Loading model: {model_path}")
    if not Path(model_path).exists():
        print(f"  [ERROR] File not found: {model_path}")
        return
    try:
        model = YOLO(model_path)
        print(f"  OK: {Path(model_path).name}")
    except Exception as e:
        print(f"  [ERROR] {e}")
        return

    # Sensor thread
    sensor_enabled = not args.no_sensor
    print(f"\n[2/4] Sensor Fusion: {'ENABLED (stub)' if sensor_enabled else 'DISABLED'}")
    sensor = SensorFusion(enabled=sensor_enabled)
    sensor.start()

    # Camera thread
    source_label = (f"Webcam #{source}" if isinstance(source, int)
                    else ("ESP32-S3 Stream" if str(source).startswith("http")
                          else Path(str(source)).name))
    print(f"\n[3/4] Starting CameraReader: {source_label}")
    cam = CameraReader(source)
    cam.start()

    if not cam.isOpened():
        print(f"  [ERROR] Cannot open camera: {source}")
        cam.stop(); sensor.stop(); return

    # Doi frame dau tien thuc su san sang (thay cho sleep co dinh).
    # MSMF/Windows camera can 1-3 giay warm-up; polling nay chinh xac hon.
    print(f"  Waiting for first frame (timeout={CAMERA_WARMUP_TIMEOUT}s)...", end="", flush=True)
    got_frame = cam._first_frame_event.wait(timeout=CAMERA_WARMUP_TIMEOUT)
    if not got_frame:
        print(f"\n  [ERROR] Cannot read first frame after {CAMERA_WARMUP_TIMEOUT}s.")
        print("  Checklist:")
        print("    - Webcam co bi app khac chiem dung (Zoom, Teams, OBS)?")
        print("    - Thu --source 1 neu co nhieu camera.")
        print("    - Kiem tra Device Manager > Cameras.")
        cam.stop(); sensor.stop(); return
    print(" OK")

    w_cam = int(cam.get(cv2.CAP_PROP_FRAME_WIDTH))
    h_cam = int(cam.get(cv2.CAP_PROP_FRAME_HEIGHT))
    print(f"  OK: {w_cam}x{h_cam}")

    # Window
    print(f"\n[4/4] Running... Press [Q] or [ESC] to quit.\n")
    cv2.namedWindow(WINDOW_NAME, cv2.WINDOW_NORMAL)
    cv2.resizeWindow(WINDOW_NAME, 1280, 720)
    SNAPSHOT_DIR.mkdir(parents=True, exist_ok=True)

    frame_count = 0
    t_prev      = time.perf_counter()
    fps_smooth  = 0.0
    use_track   = not args.no_track

    try:
        while True:
            ret, frame = cam.read()
            if not ret or frame is None:
                print("\n  [WARN] Stream lost.")
                break

            if isinstance(source, int):
                frame = cv2.flip(frame, 1)

            # Inference
            try:
                if use_track:
                    results = model.track(frame, conf=conf, imgsz=INFERENCE_SIZE,
                                          persist=True, tracker="bytetrack.yaml",
                                          device=device, verbose=False)
                else:
                    results = model.predict(frame, conf=conf, imgsz=INFERENCE_SIZE,
                                            device=device, verbose=False)
            except MemoryError:
                print("\n  [ERROR] Out of memory! Giam imgsz hoac dong chuong trinh.")
                break
            except RuntimeError as e:
                # Bat RuntimeError ro rang: CUDA OOM, tensor size mismatch, v.v.
                print(f"\n  [ERROR] RuntimeError trong inference: {e}")
                traceback.print_exc()
                break
            except Exception as e:
                # Loi nhe (frame bi corrupt, timeout ngan, ...) -> bo qua frame do
                print(f"\n  [WARN] Inference error (frame skipped): {type(e).__name__}: {e}")
                continue

            annotated = results[0].plot()

            # FPS
            t_now = time.perf_counter()
            fps_smooth = smooth_fps(1.0 / max(t_now - t_prev, 1e-9))
            t_prev = t_now
            frame_count += 1

            # Overlay
            sensor_data = sensor.get_data()
            alert       = check_alert(results)
            draw_top_bar(annotated, fps_smooth, source_label, device_label, conf)
            draw_sensor_panel(annotated, sensor_data)
            draw_alert(annotated, alert)
            draw_hints(annotated, conf)

            if frame_count % 30 == 0:
                n_det = len(results[0].boxes) if results[0].boxes is not None else 0
                print(f"  Frame {frame_count:06d} | FPS:{fps_smooth:5.1f} | "
                      f"Det:{n_det} | {'ALERT' if alert else 'OK'}      ", end="\r")

            cv2.imshow(WINDOW_NAME, annotated)

            key = cv2.waitKey(1) & 0xFF
            if key in (ord("q"), ord("Q"), 27):
                print("\n\n  [INFO] User stopped.")
                break
            elif key == ord("s"):
                ts   = datetime.now().strftime("%Y%m%d_%H%M%S")
                path = SNAPSHOT_DIR / f"snap_{ts}_f{frame_count:05d}.jpg"
                cv2.imwrite(str(path), annotated)
                print(f"\n  [SNAP] Saved: {path}")
            elif key == ord("+"):
                conf = min(conf + 0.05, 0.95)
                print(f"\n  [CONF] New threshold: {conf:.2f}")
            elif key == ord("-"):
                conf = max(conf - 0.05, 0.05)
                print(f"\n  [CONF] New threshold: {conf:.2f}")

    except KeyboardInterrupt:
        print("\n\n  [INFO] Ctrl+C pressed.")
    finally:
        print("\n  [CLEANUP] Releasing resources...")
        cam.stop()
        sensor.stop()
        cv2.destroyAllWindows()
        if _TORCH_AVAILABLE and torch.cuda.is_available():
            torch.cuda.empty_cache()
        print("  [CLEANUP] Done.")

    print("\n" + "=" * 65)
    print(f"  Total frames : {frame_count}")
    print(f"  Avg FPS      : {fps_smooth:.1f}")
    print(f"  Model        : {Path(model_path).name}")
    print(f"  Device       : {device_label}")
    print("=" * 65)


if __name__ == "__main__":
    main()
