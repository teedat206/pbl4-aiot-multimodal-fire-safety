"""
=============================================================================
PBL4 - AIoT Multimodal Fire Safety System
Script: train_yolo.py
Task:   [AI-02] - Fine-tuning YOLOv8n tren Dataset Khoi/Lua

Mo ta:
    Script nay thuc hien Fine-tuning mo hinh YOLOv8n
    tu COCO pre-trained weights sang nhan dien 2 class: Fire va Smoke.

    Hai bo tham so duoc cung cap:
      - MODE = "local_test"  -> Chay 1 epoch nhanh de kiem tra pipeline
      - MODE = "colab_full"  -> Tham so huan luyen that, danh cho GPU Colab.

Cach chay:
    # Test nhanh tren may local (1 epoch)
    python scripts/train_yolo.py

    # Khi len Colab, doi MODE = "colab_full" roi chay lai
=============================================================================
"""

import sys
import torch
from pathlib import Path
from ultralytics import YOLO

if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")


# =============================================================================
# CHON CHE DO CHAY - THAY DOI BIEN NAY
#   "local_test"  -> Kiem tra pipeline (1 epoch, CPU)
#   "colab_full"  -> Huan luyen day du (100 epochs, GPU)
MODE = "local_test"
# =============================================================================


WORKSPACE_ROOT = Path(__file__).parent.parent.resolve()
DATA_YAML      = WORKSPACE_ROOT / "data" / "datasets" / "fire_smoke" / "data.yaml"
BASE_WEIGHTS   = WORKSPACE_ROOT / "yolov8n.pt"
RUNS_DIR       = WORKSPACE_ROOT / "runs"

# =============================================================================
# BO THAM SO - LOCAL TEST (1 epoch, CPU, kiem tra pipeline)
# =============================================================================
LOCAL_TEST_PARAMS = {
    "epochs":   1,
    "batch":    4,
    "imgsz":    416,
    "workers":  2,
    "cache":    False,
    "device":   "cpu",
    "exist_ok": True,
    "name":     "fire_smoke_v1_localtest",
    "project":  str(RUNS_DIR / "detect"),
    "verbose":  True,
    "plots":    True,
}

# =============================================================================
# BO THAM SO - COLAB FULL (100 epochs, GPU, huan luyen that)
# =============================================================================
COLAB_FULL_PARAMS = {
    "epochs":       100,
    "batch":        16,
    "imgsz":        640,
    "workers":      4,
    "cache":        "ram",
    "device":       0,
    "exist_ok":     True,
    "name":         "fire_smoke_v1",
    "project":      str(RUNS_DIR / "detect"),
    "verbose":      True,
    "plots":        True,
    "weight_decay": 0.0005,
    "lr0":          0.01,
    "lrf":          0.01,
    "hsv_h":        0.015,
    "hsv_s":        0.7,
    "hsv_v":        0.4,
    "flipud":       0.0,
    "fliplr":       0.5,
    "patience":     50,
}


def main():
    print("=" * 65)
    print("  PBL4 Fire Safety - YOLOv8 Fine-tuning Script")
    print("=" * 65)

    print(f"\n[1/4] Kiem tra moi truong...")
    print(f"      Python     : {sys.version.split()[0]}")
    print(f"      PyTorch    : {torch.__version__}")
    print(f"      CUDA avail : {torch.cuda.is_available()}")
    if torch.cuda.is_available():
        print(f"      GPU        : {torch.cuda.get_device_name(0)}")
        print(f"      VRAM       : {torch.cuda.get_device_properties(0).total_memory / 1e9:.1f} GB")
    else:
        print(f"      GPU        : Khong co - se dung CPU (chi phu hop local_test)")

    print(f"\n[2/4] Kiem tra duong dan...")
    if not DATA_YAML.exists():
        print(f"      KHONG TIM THAY data.yaml: {DATA_YAML}")
        sys.exit(1)
    print(f"      data.yaml  : {DATA_YAML}")

    if not BASE_WEIGHTS.exists():
        print(f"      Khong tim thay weight local - se tai tu internet.")
    else:
        print(f"      Base weight: {BASE_WEIGHTS} ({BASE_WEIGHTS.stat().st_size / 1e6:.1f} MB)")

    print(f"\n[3/4] Che do chay: [{MODE.upper()}]")
    if MODE == "local_test":
        params = LOCAL_TEST_PARAMS
        print("      Chay kiem tra pipeline nhanh (1 epoch, CPU, batch=4)")
        print("      Muc tieu: Xac nhan KHONG co loi, khong danh gia accuracy")
    elif MODE == "colab_full":
        params = COLAB_FULL_PARAMS
        print(f"      Huan luyen day du ({params['epochs']} epochs, GPU, batch={params['batch']})")
        if not torch.cuda.is_available():
            print("\n      CANH BAO: Khong phat hien GPU nhung MODE='colab_full'!")
            answer = input("        Ban co muon tiep tuc khong? [y/N]: ").strip().lower()
            if answer != "y":
                print("        Da huy. Doi MODE='local_test' hoac chay tren Colab.")
                sys.exit(0)
    else:
        print(f"      MODE khong hop le: '{MODE}'")
        sys.exit(1)

    print(f"\n      Tham so training:")
    for k, v in params.items():
        print(f"        {k:15s} = {v}")

    print(f"\n[4/4] Khoi dong Fine-tuning...")
    print("-" * 65)

    model   = YOLO(str(BASE_WEIGHTS))
    results = model.train(data=str(DATA_YAML), **params)

    print("\n" + "=" * 65)
    print("  TRAINING HOAN TAT")
    print("=" * 65)

    save_dir = Path(params["project"]) / params["name"]
    best_pt  = save_dir / "weights" / "best.pt"

    print(f"\n  Ket qua luu tai  : {save_dir}")
    print(f"  Best weights     : {best_pt}")

    if best_pt.exists():
        print(f"\n  Danh gia nhanh tren tap val:")
        best_model = YOLO(str(best_pt))
        metrics    = best_model.val(data=str(DATA_YAML), imgsz=params["imgsz"], verbose=False)
        print(f"     mAP50      : {metrics.box.map50:.4f}")
        print(f"     mAP50-95   : {metrics.box.map:.4f}")
        print(f"     Precision  : {metrics.box.mp:.4f}")
        print(f"     Recall     : {metrics.box.mr:.4f}")

        print(f"\n  -- Buoc tiep theo --")
        print(f"  1. Copy best.pt vao: models/weights/best.pt")
        print(f"  2. Export ONNX: yolo export model=models/weights/best.pt format=onnx")
        print(f"  3. Test camera: doi MODEL_PATH trong test_camera_yolo.py")
    else:
        print(f"\n  Khong tim thay best.pt. Kiem tra log training.")

    print("=" * 65)


if __name__ == "__main__":
    main()
