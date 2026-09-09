"""
=============================================================================
PBL4 - AIoT Multimodal Fire Safety System
Script: test_camera_yolo.py
Task:   [AI-01.1] – Kiểm thử luồng video + YOLOv8 inference
Author: AI_Training_Workspace Team

Mô tả:
    Script này tải model YOLOv8n pre-trained và chạy object detection
    real-time trên luồng video được cấu hình sẵn. Hỗ trợ 3 nguồn đầu vào:
      1. File video cục bộ (test_chay.mp4)   ← MẶC ĐỊNH
      2. Webcam/USB Camera
      3. HTTP MJPEG Stream từ ESP32-CAM

Cách chạy:
    python scripts/test_camera_yolo.py
=============================================================================
"""

import cv2
import time
from pathlib import Path
from ultralytics import YOLO

# =============================================================================
# ██  CẤU HÌNH NGUỒN ĐẦU VÀO VIDEO  ██
#
# Thay đổi biến VIDEO_SOURCE bên dưới để chuyển đổi giữa các môi trường.
# Chỉ cần bỏ comment (xóa dấu #) ở dòng tương ứng và comment lại dòng khác.
#
# ── Lựa chọn 1: File video cục bộ (ĐANG DÙNG - MẶC ĐỊNH) ──────────────────
VIDEO_SOURCE = str(Path(__file__).parent.parent / "data" / "raw_videos" / "test_chay.mp4")
#
# ── Lựa chọn 2: Webcam / USB Camera ──────────────────────────────────────────
#   Dùng số nguyên: 0 = camera mặc định, 1 = camera thứ 2, v.v.
#   VIDEO_SOURCE = 0
#
# ── Lựa chọn 3: HTTP Stream từ ESP32-CAM ─────────────────────────────────────
#   Thay <ESP32_IP> bằng địa chỉ IP thực của module ESP32-CAM trên LAN.
#   Ví dụ: nếu ESP32-CAM hiển thị IP là 192.168.1.105 thì:
#   VIDEO_SOURCE = "http://192.168.1.105/stream"
#   Hoặc nếu dùng endpoint MJPEG khác:
#   VIDEO_SOURCE = "http://192.168.1.105:81/stream"
#   VIDEO_SOURCE = "http://<ESP32_IP>/stream"
#
# =============================================================================

# =============================================================================
# CẤU HÌNH MODEL & INFERENCE
# =============================================================================

# Đường dẫn tới weight file.
# "yolov8n.pt" sẽ tự động download từ Ultralytics khi chạy lần đầu.
# Sau khi fine-tune, thay bằng đường dẫn tới best.pt của bạn:
#   MODEL_PATH = str(Path(__file__).parent.parent / "models" / "weights" / "best.pt")
MODEL_PATH = "yolov8n.pt"

# Ngưỡng confidence – chỉ hiển thị detection có độ tin cậy >= giá trị này
CONFIDENCE_THRESHOLD = 0.40

# Kích thước frame inference (nhỏ hơn = nhanh hơn, nhưng kém chính xác hơn)
INFERENCE_SIZE = 640

# Hiển thị cửa sổ xem video (True = bật, False = chỉ chạy inference không hiện)
SHOW_WINDOW = True

# Giới hạn FPS để không burn CPU/GPU quá mức (None = không giới hạn)
MAX_FPS = 30


# =============================================================================
# HÀM TIỆN ÍCH
# =============================================================================

def draw_info_overlay(frame: cv2.Mat, fps: float, source_label: str) -> cv2.Mat:
    """Vẽ thông tin FPS và nguồn video lên frame."""
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

    return frame


def get_source_label(source) -> str:
    """Trả về nhãn mô tả nguồn video để hiển thị."""
    if isinstance(source, int):
        return f"Webcam #{source}"
    if isinstance(source, str):
        if source.startswith("http"):
            return f"ESP32-CAM Stream"
        return Path(source).name
    return "Unknown Source"


# =============================================================================
# HÀM CHÍNH
# =============================================================================

def main():
    print("=" * 60)
    print("  PBL4 Fire Safety – YOLOv8 Camera Test")
    print("=" * 60)

    # ── 1. Tải model YOLOv8 ──────────────────────────────────────────────────
    print(f"\n[1/3] Đang tải model: {MODEL_PATH}")
    try:
        model = YOLO(MODEL_PATH)
        print(f"      ✓ Model loaded: {model.info()}")
    except Exception as e:
        print(f"      ✗ Lỗi tải model: {e}")
        return

    # ── 2. Mở nguồn video ────────────────────────────────────────────────────
    source_label = get_source_label(VIDEO_SOURCE)
    print(f"\n[2/3] Đang kết nối nguồn video: {source_label}")
    print(f"      Path/URL: {VIDEO_SOURCE}")

    cap = cv2.VideoCapture(VIDEO_SOURCE)

    if not cap.isOpened():
        print(f"\n✗ KHÔNG THỂ MỞ NGUỒN VIDEO: {VIDEO_SOURCE}")
        print("  Gợi ý kiểm tra:")
        if isinstance(VIDEO_SOURCE, str) and VIDEO_SOURCE.startswith("http"):
            print("  • Kiểm tra ESP32-CAM đã được cấp nguồn và kết nối LAN chưa.")
            print("  • Thử mở URL trực tiếp trên trình duyệt để xác nhận stream.")
        elif isinstance(VIDEO_SOURCE, int):
            print("  • Kiểm tra Webcam đã được cắm vào và driver đã cài chưa.")
        else:
            print(f"  • Kiểm tra file video tồn tại tại: {VIDEO_SOURCE}")
        return

    # Lấy thông tin video
    orig_width  = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    orig_height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    orig_fps    = cap.get(cv2.CAP_PROP_FPS) or 30.0
    print(f"      ✓ Kết nối thành công | {orig_width}×{orig_height} @ {orig_fps:.1f} FPS")

    # ── 3. Vòng lặp inference ─────────────────────────────────────────────────
    print(f"\n[3/3] Đang chạy YOLOv8 inference...")
    print("      Nhấn [Q] hoặc [ESC] trong cửa sổ video để thoát.\n")

    frame_count  = 0
    fps_display  = 0.0
    t_start      = time.time()
    t_frame_prev = time.time()

    min_frame_time = (1.0 / MAX_FPS) if MAX_FPS else 0.0

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

        # Giới hạn FPS
        elapsed = time.time() - t_frame_prev
        if elapsed < min_frame_time:
            time.sleep(min_frame_time - elapsed)
        t_frame_prev = time.time()

        # Chạy YOLO inference
        results = model(
            frame,
            conf=CONFIDENCE_THRESHOLD,
            imgsz=INFERENCE_SIZE,
            verbose=False,    # Tắt log từng frame để console sạch hơn
        )

        # Vẽ bounding box lên frame
        annotated_frame = results[0].plot()

        # Tính FPS hiển thị
        frame_count += 1
        if frame_count % 10 == 0:
            fps_display = 10.0 / (time.time() - t_start)
            t_start = time.time()

        # Vẽ overlay thông tin
        annotated_frame = draw_info_overlay(annotated_frame, fps_display, source_label)

        # Hiển thị số lượng detection của frame hiện tại
        n_detections = len(results[0].boxes) if results[0].boxes is not None else 0
        if n_detections > 0:
            print(f"  Frame {frame_count:05d} | FPS: {fps_display:5.1f} | "
                  f"Detections: {n_detections}", end="\r")

        # Hiển thị cửa sổ video
        if SHOW_WINDOW:
            cv2.imshow("PBL4 Fire Safety – YOLOv8 Detection", annotated_frame)

            # Nhấn Q hoặc ESC để thoát
            key = cv2.waitKey(1) & 0xFF
            if key in (ord("q"), ord("Q"), 27):
                print("\n\n  [INFO] Người dùng dừng chương trình.")
                break

    # ── Cleanup ───────────────────────────────────────────────────────────────
    cap.release()
    cv2.destroyAllWindows()

    print("\n" + "=" * 60)
    print(f"  Tổng số frame đã xử lý: {frame_count}")
    print("  Kết thúc chương trình.")
    print("=" * 60)


if __name__ == "__main__":
    main()
