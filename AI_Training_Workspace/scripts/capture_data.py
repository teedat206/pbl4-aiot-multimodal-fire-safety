"""
=============================================================================
PBL4 - AIoT Multimodal Fire Safety System
Script: capture_data.py
Purpose: Thu thap anh tuy chinh (Custom Dataset) bang webcam

=== BOI CANH DU AN - CONTEXT MEMORY ===
Mo hinh YOLOv8 hien tai bi Domain Gap (lech pha du lieu):
  - Bao dong gia: Nhan dien nham mai toc den bu xo, rem cua choi sang
    hoac bong am thanh "khoi" (Smoke False Positive).
  - Bo sot: Khoi mong, lua nho, khoi co mau bat thuong bi bo qua.

Nguyen nhan: Bo dataset goc (Roboflow) duoc chup trong moi truong nuoc
ngoai, khac xa dieu kien thuc te cua phong thi nghiem/nha o Viet Nam
(anh sang den, rem vai mau toi, mau da, toc nguoi Viet...).

Giai phap: Thu thap Custom Dataset bang chinh webcam laptop:
  - Loai 1 (Positive): Anh khoi that (nhan tu nen/huong thom/bep gas)
    trong dieu kien anh sang phong thi nghiem thuc te.
  - Loai 2 (Negative / Hard Negative): Anh cac vat the de gay nham
    (toc den, rem cua, bong den, bat dui...) de day model phan biet.

Sau khi thu thap, anh se duoc dan nhan bang LabelImg/Roboflow
va dua vao Fine-tuning YOLOv8 lan 2 (Transfer Learning).
=========================================

Cach chay:
    python scripts/capture_data.py

Phim tat:
    SPACE : Chup va luu anh hien tai
    q / ESC : Thoat chuong trinh
=============================================================================
"""

import cv2
import os
import time
from pathlib import Path
from datetime import datetime

# =============================================================================
# CAU HINH
# =============================================================================

# Thu muc luu anh - dat trong data/ cua workspace
SAVE_DIR = Path(__file__).parent.parent / "data" / "custom_dataset" / "images"

# Camera ID (0 = webcam mac dinh, 1 = camera thu 2...)
CAMERA_ID = 0

# Do phan giai mong muon
CAM_WIDTH  = 1280
CAM_HEIGHT = 720

# Ten cua so hien thi
WINDOW_NAME = "PBL4_Capture"

# Thoi gian hieu ung chop man hinh khi chup (ms)
FLASH_DURATION_MS = 50

# =============================================================================
# MAIN
# =============================================================================

def main():
    # --- 1. Tao thu muc luu anh neu chua ton tai ---
    SAVE_DIR.mkdir(parents=True, exist_ok=True)
    print("=" * 60)
    print("  PBL4 Fire Safety - Custom Dataset Capture Tool")
    print("=" * 60)
    print(f"\n  Thu muc luu anh : {SAVE_DIR}")
    print(f"  SPACE           : Chup anh")
    print(f"  Q / ESC         : Thoat\n")

    # --- 2. Khoi tao camera ---
    cap = cv2.VideoCapture(CAMERA_ID)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH,  CAM_WIDTH)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, CAM_HEIGHT)

    if not cap.isOpened():
        print(f"  [LOI] Khong mo duoc camera ID {CAMERA_ID}.")
        print("  Kiem tra lai ket noi webcam hoac thu doi CAMERA_ID = 1.")
        return

    actual_w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    actual_h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    print(f"  Camera ID {CAMERA_ID} | Do phan giai: {actual_w}x{actual_h}")
    print("-" * 60)

    # --- 3. Tao cua so co the thay doi kich thuoc ---
    cv2.namedWindow(WINDOW_NAME, cv2.WINDOW_NORMAL)
    cv2.resizeWindow(WINDOW_NAME, CAM_WIDTH, CAM_HEIGHT)

    # --- 4. Dem so anh da chup ---
    capture_count = 0
    flash_end_time = 0.0

    # --- 5. Vong lap chinh ---
    while True:
        ret, frame = cap.read()
        if not ret:
            print("  [LOI] Khong doc duoc frame. Kiem tra lai webcam.")
            break

        # Lat ngang (hieu ung guong)
        frame = cv2.flip(frame, 1)

        # Hieu ung chop man hinh ngay sau khi chup
        display_frame = frame.copy()
        current_time  = time.time()
        if current_time < flash_end_time:
            display_frame = cv2.bitwise_not(display_frame)

        # Ve overlay thong tin huong dan len frame
        h, w = display_frame.shape[:2]
        overlay = display_frame.copy()
        cv2.rectangle(overlay, (0, 0), (w, 50), (0, 0, 0), -1)
        cv2.addWeighted(overlay, 0.55, display_frame, 0.45, 0, display_frame)

        guide_text = f"[SPACE] Chup | [Q/ESC] Thoat | Da chup: {capture_count} anh"
        cv2.putText(display_frame, guide_text, (12, 33),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.75, (0, 255, 180), 2, cv2.LINE_AA)

        cv2.imshow(WINDOW_NAME, display_frame)

        # Quan sat phim bam
        key = cv2.waitKey(1) & 0xFF

        # --- SPACE: Chup anh ---
        if key == ord(" "):
            timestamp   = datetime.now().strftime("%Y%m%d_%H%M%S_%f")[:-3]
            filename    = f"capture_{timestamp}.jpg"
            save_path   = SAVE_DIR / filename
            cv2.imwrite(str(save_path), frame)   # Luu frame goc (khong co overlay)

            capture_count += 1
            flash_end_time = time.time() + (FLASH_DURATION_MS / 1000.0)

            print(f"  [{capture_count:04d}] Da luu: {filename}")

        # --- Q hoac ESC: Thoat ---
        elif key in (ord("q"), ord("Q"), 27):
            print("\n  Nguoi dung dung chuong trinh.")
            break

    # --- 6. Cleanup ---
    cap.release()
    cv2.destroyAllWindows()

    print("\n" + "=" * 60)
    print(f"  Tong so anh da chup : {capture_count} anh")
    if capture_count > 0:
        print(f"  Anh duoc luu tai   : {SAVE_DIR}")
        print(f"\n  Buoc tiep theo:")
        print(f"    1. Dan nhan anh bang LabelImg hoac upload len Roboflow")
        print(f"    2. Them vao dataset fire_smoke de Fine-tuning lan 2")
    print("=" * 60)


if __name__ == "__main__":
    main()
