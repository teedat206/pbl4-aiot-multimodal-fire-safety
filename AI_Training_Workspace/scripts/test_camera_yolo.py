"""
=============================================================================
PBL4 - AIoT Multimodal Fire Safety System
Script: test_camera_yolo.py
Task:   [AI-01.1] – Kiểm thử luồng video + YOLOv8 inference
Author: AI_Training_Workspace Team

Bối cảnh dự án:
    Mô hình YOLO bị Domain Gap: báo giả (tóc/rèm → khói) và bỏ sót khói
    mỏng/lửa nhỏ. Giải pháp: fine-tune với Custom Dataset (bestnew.pt).

Cập nhật v2 (16/09/2026):
    - Chuyển sang model fine-tuned bestnew.pt
    - Thêm CameraReader thread chống buffer-lag
    - Tự động chọn GPU/CPU
    - Cleanup đầy đủ: cap.release + destroyAllWindows + cuda.empty_cache

Cách chạy:
    python scripts/test_camera_yolo.py
=============================================================================
"""

import cv2
import time
import threading
from pathlib import Path
from ultralytics import YOLO

try:
    import torch
    _TORCH_AVAILABLE = True
except ImportError:
    _TORCH_AVAILABLE = False

# =============================================================================
# ██  CẤU HÌNH NGUỒN ĐẦU VÀO VIDEO  ██
#
# Thay đổi biến VIDEO_SOURCE bên dưới để chuyển đổi giữa các môi trường.
# Chỉ cần bỏ comment (xóa dấu #) ở dòng tương ứng và comment lại dòng khác.
#
# ── Lựa chọn 1: File video cục bộ (ĐANG DÙNG - MẶC ĐỊNH) ──────────────────
# VIDEO_SOURCE = str(Path(__file__).parent.parent / "data" / "raw_videos" / "test_chay.mp4")
#
# ── Lựa chọn 2: Webcam / USB Camera ──────────────────────────────────────────
#   Dùng số nguyên: 0 = camera mặc định, 1 = camera thứ 2, v.v.
VIDEO_SOURCE = 0
#
# ── Lựa chọn 3: HTTP Stream từ ESP32-S3 ──────────────────────────────────────
#   Thay <ESP32_IP> bằng địa chỉ IP thực của module ESP32-S3 trên LAN.
#   Ví dụ: nếu ESP32-S3 hiển thị IP là 192.168.1.105 thì:
#   VIDEO_SOURCE = "http://192.168.1.105/stream"
#   Hoặc nếu dùng endpoint MJPEG khác:
#   VIDEO_SOURCE = "http://192.168.1.105:81/stream"
#   VIDEO_SOURCE = "http://<ESP32_IP>/stream"
#
# =============================================================================

# =============================================================================
# CẤU HÌNH MODEL & INFERENCE
# =============================================================================

# Đường dẫn tới weight file fine-tuned chuyên nhận diện lửa/khói.
# best.pt    = model cũ (COCO/fire_smoke v1)
# bestnew.pt = model mới fine-tuned với Custom Dataset (khuyến nghị)
MODEL_PATH = str(Path(__file__).parent.parent / "models" / "weights" / "bestnew.pt")

# Ngưỡng confidence – chỉ hiển thị detection có độ tin cậy >= giá trị này
# 0.45 là điểm cân bằng tốt cho bestnew.pt: đủ nhạy với khói mỏng, lọc được false positive
CONFIDENCE_THRESHOLD = 0.45

# Kích thước frame inference (nhỏ hơn = nhanh hơn, nhưng kém chính xác hơn)
INFERENCE_SIZE = 640

# Hiển thị cửa sổ xem video (True = bật, False = chỉ chạy inference không hiện)
SHOW_WINDOW = True

# Giới hạn FPS để không burn CPU/GPU quá mức (None = không giới hạn)
MAX_FPS = 30


# =============================================================================
# CAMERA READER THREAD – chống buffer lag
# =============================================================================

class CameraReader(threading.Thread):
    """
    Đọc frame từ camera trong thread riêng biệt.

    Tại sao cần thread riêng?
    OpenCV VideoCapture có internal buffer (~3-5 frames). Nếu YOLO inference
    chậm hơn tốc độ camera, frame cũ tích lũy trong buffer → lag/giật khi
    hiển thị. Thread này liên tục drain buffer, giữ frame mới nhất.
    """

    def __init__(self, source):
        super().__init__(daemon=True)  # daemon=True: tự tắt khi main thread tắt
        self.cap = cv2.VideoCapture(source)
        # Thiết lập độ phân giải HD
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
        # Giảm buffer size xuống 1 để luôn lấy frame mới nhất
        self.cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        self.ret   = False
        self.frame = None
        self._lock  = threading.Lock()
        self._stop  = threading.Event()

    def run(self):
        """Vòng lặp liên tục đọc và cập nhật frame mới nhất."""
        while not self._stop.is_set():
            ret, frame = self.cap.read()
            with self._lock:
                self.ret   = ret
                self.frame = frame
            if not ret:
                break  # Stream ngắt

    def read(self):
        """Trả về frame mới nhất (thread-safe)."""
        with self._lock:
            return self.ret, (self.frame.copy() if self.frame is not None else None)

    def isOpened(self):
        return self.cap.isOpened()

    def get(self, prop_id):
        return self.cap.get(prop_id)

    def stop(self):
        """Dừng thread và giải phóng camera."""
        self._stop.set()
        self.join(timeout=2.0)
        self.cap.release()


# =============================================================================
# HÀM TIỆN ÍCH
# =============================================================================

def draw_info_overlay(frame: cv2.Mat, fps: float, source_label: str, device_label: str) -> cv2.Mat:
    """Vẽ thông tin FPS, device và nguồn video lên frame."""
    h, w = frame.shape[:2]

    # Nền mờ cho text
    overlay = frame.copy()
    cv2.rectangle(overlay, (0, 0), (w, 40), (0, 0, 0), -1)
    cv2.addWeighted(overlay, 0.5, frame, 0.5, 0, frame)

    # Text FPS
    fps_text = f"FPS: {fps:.1f}"
    cv2.putText(frame, fps_text, (10, 28),
                cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 0), 2)

    # Text nguồn video
    source_text = f"Source: {source_label}"
    cv2.putText(frame, source_text, (w // 2 - 120, 28),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 200, 0), 2)

    # Text device (GPU/CPU)
    cv2.putText(frame, device_label, (w - 140, 28),
                cv2.FONT_HERSHEY_SIMPLEX, 0.6, (180, 180, 255), 2)

    return frame


def get_source_label(source) -> str:
    """Trả về nhãn mô tả nguồn video để hiển thị."""
    if isinstance(source, int):
        return f"Webcam #{source}"
    if isinstance(source, str):
        if source.startswith("http"):
            return f"ESP32-S3 Stream"
        return Path(source).name
    return "Unknown Source"


# =============================================================================
# HÀM CHÍNH
# =============================================================================

def main():
    print("=" * 60)
    print("  PBL4 Fire Safety – YOLOv8 Camera Test  [bestnew.pt]")
    print("=" * 60)

    # ── 0. Tự động chọn device GPU/CPU ───────────────────────────────────────
    if _TORCH_AVAILABLE and torch.cuda.is_available():
        device      = "0"
        device_label = f"GPU:{torch.cuda.get_device_name(0)[:12]}"
    else:
        device      = "cpu"
        device_label = "CPU"
    print(f"\n[0/3] Device: {device_label}")

    # ── 1. Tải model YOLOv8 ──────────────────────────────────────────────────
    print(f"\n[1/3] Đang tải model: {MODEL_PATH}")
    try:
        model = YOLO(MODEL_PATH)
        print(f"      OK Model loaded: {MODEL_PATH}")
    except Exception as e:
        print(f"      Loi tai model: {e}")
        return

    # ── 2. Mở nguồn video (CameraReader thread) ───────────────────────────────
    source_label = get_source_label(VIDEO_SOURCE)
    print(f"\n[2/3] Dang ket noi nguon video (threaded): {source_label}")
    print(f"      Path/URL: {VIDEO_SOURCE}")

    cap = CameraReader(VIDEO_SOURCE)
    cap.start()  # Khởi động thread đọc frame
    time.sleep(0.5)  # Chờ thread warm-up lấy được frame đầu tiên

    if not cap.isOpened():
        print(f"\n  KHONG THE MO NGUON VIDEO: {VIDEO_SOURCE}")
        print("  Goi y kiem tra:")
        if isinstance(VIDEO_SOURCE, str) and VIDEO_SOURCE.startswith("http"):
            print("  - Kiem tra ESP32-S3 da duoc cap nguon va ket noi LAN chua.")
            print("  - Thu mo URL truc tiep tren trinh duyet de xac nhan stream.")
        elif isinstance(VIDEO_SOURCE, int):
            print("  - Kiem tra Webcam da duoc cam vao va driver da cai chua.")
        else:
            print(f"  - Kiem tra file video ton tai tai: {VIDEO_SOURCE}")
        cap.stop()
        return

    # Kiểm tra frame đầu tiên (phát hiện webcam bị lock bởi tiến trình khác)
    ret_test, _ = cap.read()
    if not ret_test:
        print("\n  [LOI] Khong doc duoc frame dau tien.")
        print("  Nguyen nhan co the la:")
        print("    1. Mot tien trinh khac (Teams, Zoom, OBS...) dang giu webcam.")
        print("    2. Driver webcam bi loi - thu cam lai cap USB.")
        print("    3. Thu doi device index: VIDEO_SOURCE = 1 thay vi 0.")
        cap.stop()
        return

    # Lay thong tin video
    orig_width  = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    orig_height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    orig_fps    = cap.get(cv2.CAP_PROP_FPS) or 30.0
    print(f"      OK Ket noi thanh cong | {orig_width}x{orig_height} @ {orig_fps:.1f} FPS")

    # ── 3. Vòng lặp inference ─────────────────────────────────────────────────
    print(f"\n[3/3] Đang chạy YOLOv8 inference...")
    print("      Nhấn [Q] hoặc [ESC] trong cửa sổ video để thoát.\n")

    frame_count  = 0
    fps_display  = 0.0
    t_start      = time.time()
    t_frame_prev = time.time()

    min_frame_time = (1.0 / MAX_FPS) if MAX_FPS else 0.0

    # Cấu hình cửa sổ hiển thị HD (1280x720) và cho phép resize linh hoạt
    window_name = "PBL4_Camera"
    if SHOW_WINDOW:
        cv2.namedWindow(window_name, cv2.WINDOW_NORMAL)
        cv2.resizeWindow(window_name, 1280, 720)

    while True:
        # Đọc frame
        ret, frame = cap.read()
        if not ret:
            # Với file video: loop lại từ đầu
            if not isinstance(VIDEO_SOURCE, int) and not str(VIDEO_SOURCE).startswith("http"):
                print("  [INFO] Hết video – bắt đầu lại từ đầu.")
                cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
                continue
            else:
                print("  [WARN] Mất kết nối stream. Đang thoát...")
                break

        # Lật khung hình như gương (ngang) để chống ngược
        if isinstance(VIDEO_SOURCE, int):
            frame = cv2.flip(frame, 1)

        # Giới hạn FPS
        elapsed = time.time() - t_frame_prev
        if elapsed < min_frame_time:
            time.sleep(min_frame_time - elapsed)
        t_frame_prev = time.time()

        # Chạy YOLO Track (ByteTrack) – bám sát mục tiêu ổn định hơn predict()
        # persist=True: giữ nguyên Track ID giữa các frame (không bị nháy ID)
        # device: tự động GPU nếu có, fallback CPU
        results = model.track(
            frame,
            conf=CONFIDENCE_THRESHOLD,
            imgsz=INFERENCE_SIZE,
            persist=True,
            tracker="bytetrack.yaml",
            device=device,
            verbose=False,
        )

        # Vẽ bounding box lên frame
        annotated_frame = results[0].plot()

        # Tính FPS hiển thị
        frame_count += 1
        if frame_count % 10 == 0:
            fps_display = 10.0 / (time.time() - t_start)
            t_start = time.time()

        # Vẽ overlay thông tin
        annotated_frame = draw_info_overlay(annotated_frame, fps_display, source_label, device_label)

        # Hiển thị số lượng detection của frame hiện tại
        n_detections = len(results[0].boxes) if results[0].boxes is not None else 0
        if n_detections > 0:
            print(f"  Frame {frame_count:05d} | FPS: {fps_display:5.1f} | "
                  f"Detections: {n_detections}", end="\r")

        # Hiển thị cửa sổ video
        if SHOW_WINDOW:
            cv2.imshow(window_name, annotated_frame)

            # Nhấn Q hoặc ESC để thoát
            key = cv2.waitKey(1) & 0xFF
            if key in (ord("q"), ord("Q"), 27):
                print("\n\n  [INFO] Người dùng dừng chương trình.")
                break

    # ── Cleanup đầy đủ ────────────────────────────────────────────────────────
    cap.stop()              # Dừng CameraReader thread + giải phóng camera
    cv2.destroyAllWindows()
    if _TORCH_AVAILABLE and torch.cuda.is_available():
        torch.cuda.empty_cache()  # Trả lại VRAM cho hệ thống

    print("\n" + "=" * 60)
    print(f"  Tổng số frame đã xử lý: {frame_count}")
    print("  Kết thúc chương trình.")
    print("=" * 60)


if __name__ == "__main__":
    main()
