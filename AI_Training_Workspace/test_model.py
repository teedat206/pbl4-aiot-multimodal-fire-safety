"""
Script kiểm tra và đánh giá nhanh trọng số YOLOv8 (models/weights/best.pt)
Tự động tìm kiếm file ảnh/video test và chạy inference báo cáo checklist.
"""

import sys
import time
from pathlib import Path

# Đảm bảo mã hóa UTF-8 cho terminal Windows
if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass


def find_test_input():
    """Tìm một file ảnh hoặc video mẫu trong workspace."""
    # Nếu người dùng truyền đường dẫn qua tham số CLI
    if len(sys.argv) > 1:
        custom_path = Path(sys.argv[1])
        if custom_path.exists():
            return custom_path
        print(f"[!] Đường dẫn cung cấp qua tham số '{sys.argv[1]}' không tồn tại. Đang tự động quét...")

    # Các thư mục ưu tiên tìm kiếm
    search_dirs = [
        Path("AI_Training_Workspace/data/datasets/fire_smoke/test/images"),
        Path("AI_Training_Workspace/data/custom_dataset/images"),
        Path("AI_Training_Workspace/data/datasets/fire_smoke/valid/images"),
        Path("AI_Training_Workspace/data/datasets/fire_smoke/train/images"),
        Path("data/datasets/fire_smoke/test/images"),
        Path("data/custom_dataset/images"),
        Path("."),
    ]

    valid_extensions = {".jpg", ".jpeg", ".png", ".bmp", ".webp", ".mp4", ".avi", ".mov"}

    for s_dir in search_dirs:
        if s_dir.exists():
            for file_path in s_dir.rglob("*"):
                if file_path.is_file() and file_path.suffix.lower() in valid_extensions:
                    # Bỏ qua các file trong venv, .git, cache
                    parts_lower = [p.lower() for p in file_path.parts]
                    if any(ignored in parts_lower for ignored in ["venv", ".git", "runs", "__pycache__"]):
                        continue
                    return file_path

    return None


def main():
    # 1. Kiểm tra sự tồn tại của file trọng số
    weights_path = Path("models/weights/best.pt")
    if not weights_path.exists():
        # Fallback thử kiểm tra trong thư mục AI_Training_Workspace nếu đang ở root
        fallback_path = Path("AI_Training_Workspace/models/weights/best.pt")
        if fallback_path.exists():
            weights_path = fallback_path
        else:
            print(f"[ ] Error: Không tìm thấy file trọng số tại '{weights_path}'!")
            print("    Vui lòng kiểm tra lại đường dẫn models/weights/best.pt.")
            sys.exit(1)

    print("[x] Checked: File trọng số best.pt mới đã tồn tại.")

    # 2. Load model YOLO với file trọng số đó
    try:
        from ultralytics import YOLO
        model = YOLO(str(weights_path))
        print("[x] Checked: Load model YOLO thành công.")
    except Exception as e:
        print(f"[ ] Error: Không thể load model YOLO: {e}")
        sys.exit(1)

    # 3. Tìm file ảnh hoặc video test
    test_file = find_test_input()
    if not test_file or not test_file.exists():
        print("[ ] Error: Không tìm thấy file ảnh/video test nào trong workspace.")
        print("    Vui lòng cung cấp đường dẫn file bằng lệnh: python test_model.py <đường_dẫn_file>")
        sys.exit(1)

    print(f"[x] Checked: Đã tìm thấy file test đầu vào tại [{test_file}].")

    # 4. Chạy hàm predict
    try:
        t_start = time.perf_counter()
        results = model.predict(source=str(test_file), save=True, conf=0.25, verbose=False)
        t_total = (time.perf_counter() - t_start) * 1000  # ms
        print("[x] Run: Chạy predict thành công!")
    except Exception as e:
        print(f"[ ] Error: Quá trình chạy predict thất bại: {e}")
        sys.exit(1)

    # 5. Phân tích kết quả và hiển thị thông tin
    res = results[0]
    num_objects = len(res.boxes) if res.boxes is not None else 0

    # Lấy inference time từ results nếu có, hoặc dùng t_total
    speed = getattr(res, "speed", {})
    infer_time_ms = speed.get("inference", t_total)

    print(f"[x] Info: Tốc độ xử lý (inference time) là {infer_time_ms:.1f}ms và phát hiện được {num_objects} đối tượng (khói/lửa).")

    # In thêm thông tin chi tiết các phát hiện (nếu có)
    if num_objects > 0:
        names = model.names if hasattr(model, "names") else {}
        detections = []
        for box in res.boxes:
            cls_id = int(box.cls[0].item())
            cls_name = names.get(cls_id, f"class_{cls_id}")
            conf = float(box.conf[0].item())
            detections.append(f"{cls_name} ({conf*100:.1f}%)")
        print(f"    Chi tiết phát hiện: {', '.join(detections)}")

    if hasattr(res, "save_dir") and res.save_dir:
        print(f"    Ảnh kết quả (bounding box) đã được lưu tại: {res.save_dir}")


if __name__ == "__main__":
    main()
