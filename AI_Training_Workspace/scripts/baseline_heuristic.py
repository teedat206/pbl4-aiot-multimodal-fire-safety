
import json
import sys

# Fix UTF-8 output trên Windows console (tránh UnicodeEncodeError với tiếng Việt)
if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
from pathlib import Path
# ── Nhiệt độ ─────────────────────────────────────────────────────────────────
TEMP_WARN     = 35.0    # °C – bắt đầu theo dõi
TEMP_HIGH     = 50.0    # °C – nguy cơ cháy âm ỉ
TEMP_CRITICAL = 65.0    # °C – hỏa hoạn bùng phát

# ── Khói (Smoke) ──────────────────────────────────────────────────────────────
SMOKE_WARN     = 20     # 0–100 – mức cảnh báo đầu tiên
SMOKE_HIGH     = 45     # 0–100 – khói dày, khả năng cháy âm ỉ
SMOKE_CRITICAL = 75     # 0–100 – khói đặc, hỏa hoạn rõ ràng

# ── Gas LPG/Butane (ppm) ─────────────────────────────────────────────────────
GAS_WARN     = 200      # ppm – nồng độ tăng bất thường
GAS_HIGH     = 800      # ppm – rò rỉ đáng kể
GAS_CRITICAL = 1200     # ppm – nguy cơ rò rỉ nghiêm trọng

# ── CO – Carbon Monoxide (ppm) ───────────────────────────────────────────────
CO_WARN     = 25        # ppm – OSHA: giới hạn 8 giờ tiếp xúc
CO_HIGH     = 80        # ppm – Nguy hiểm khi tiếp xúc kéo dài
CO_CRITICAL = 200       # ppm – Nguy hiểm chết người

# ── Bù trừ độ ẩm ─────────────────────────────────────────────────────────────
# Khi độ ẩm rất cao (>65%), hơi nước có thể làm cảm biến khói đọc cao hơn thực tế.
# Nhân hệ số giảm để tránh false-positive trong môi trường ẩm ướt.
HUM_COMPENSATION_THRESHOLD = 65.0   # % – bắt đầu áp dụng bù trừ
HUM_COMPENSATION_MAX       = 90.0   # % – độ ẩm tối đa được xét
HUM_COMPENSATION_FACTOR    = 0.003  # Mỗi 1% hum trên ngưỡng → giảm 0.3% smoke


# =============================================================================
# HÀM CHÍNH: ĐÁNH GIÁ MỨC ĐỘ RỦI RO
# =============================================================================

def evaluate_risk(temp: float, hum: float, smoke: float, gas: float, co: float) -> int:
    """
    Đánh giá mức độ rủi ro môi trường dựa trên dữ liệu cảm biến.

    Args:
        temp  (float): Nhiệt độ (°C).
        hum   (float): Độ ẩm tương đối (%).
        smoke (float): Nồng độ khói, thang 0–100.
        gas   (float): Nồng độ gas LPG/Butane (ppm).
        co    (float): Nồng độ CO (ppm).

    Returns:
        int: Class từ 0 (bình thường) đến 4 (hỏa hoạn bùng phát).
    """

    # ── Bước 1: Bù trừ cảm biến khói theo độ ẩm ─────────────────────────────
    # Môi trường rất ẩm (bếp hơi, phòng tắm) làm tăng ADC của cảm biến khói
    # giả tạo → áp dụng hệ số hiệu chỉnh để tránh báo nhầm.
    smoke_adjusted = smoke
    if hum > HUM_COMPENSATION_THRESHOLD:
        excess_hum = min(hum, HUM_COMPENSATION_MAX) - HUM_COMPENSATION_THRESHOLD
        correction = 1.0 - (excess_hum * HUM_COMPENSATION_FACTOR)
        smoke_adjusted = smoke * max(correction, 0.7)   # Không giảm quá 30%

    # ── Bước 2: Đánh giá phân tầng ───────────────────────────────────────────
    # Nguyên tắc: Kiểm tra class cao nhất trước (nguy hiểm nhất),
    # nếu không thỏa thì xuống class thấp hơn.

    # ── Class 4: Hỏa hoạn bùng phát ──────────────────────────────────────────
    # Điều kiện: Nhiệt độ cực cao VÀ khói đặc; HOẶC khói đặc kèm CO nguy hiểm
    if (temp >= TEMP_CRITICAL and smoke_adjusted >= SMOKE_CRITICAL) or \
       (smoke_adjusted >= SMOKE_CRITICAL and co >= CO_CRITICAL):
        return 4

    # ── Class 3: Cháy âm ỉ (Smoldering) ─────────────────────────────────────
    # Điều kiện A: Nhiệt độ cao + khói dày + CO tích tụ (đặc trưng vật liệu cháy chậm)
    # Điều kiện B: Nhiệt độ rất cao + khói ở mức vừa (đám cháy bùng nhưng thông thoáng)
    if (temp >= TEMP_HIGH and smoke_adjusted >= SMOKE_HIGH and co >= CO_HIGH) or \
       (temp >= TEMP_CRITICAL and smoke_adjusted >= SMOKE_WARN):
        return 3

    # ── Class 2: Rò rỉ Gas (chưa có lửa) ────────────────────────────────────
    # Điều kiện: Gas cao bất thường NHƯNG khói và nhiệt độ vẫn thấp
    # Phân biệt với Class 3/4: nếu có lửa thì khói và nhiệt sẽ cùng tăng
    if gas >= GAS_CRITICAL and smoke_adjusted < SMOKE_WARN and temp < TEMP_HIGH:
        return 2

    # ── Class 1: Cảnh báo sớm ────────────────────────────────────────────────
    # Bất kỳ chỉ số nào vượt ngưỡng cảnh báo đầu tiên
    if (temp    >= TEMP_WARN  or
        smoke_adjusted >= SMOKE_WARN or
        gas     >= GAS_WARN   or
        co      >= CO_WARN):
        return 1

    # ── Class 0: Bình thường ─────────────────────────────────────────────────
    return 0


# =============================================================================
# HELPER: IN KẾT QUẢ ĐẸP RA TERMINAL
# =============================================================================

CLASS_LABELS = {
    0: ("✅ Bình thường",         "green"),
    1: ("⚠️  Cảnh báo sớm",       "yellow"),
    2: ("🔶 Rò rỉ Gas",           "orange"),
    3: ("🔥 Cháy âm ỉ",           "red"),
    4: ("🆘 HỎA HOẠN BÙNG PHÁT", "critical"),
}

# ANSI color codes cho terminal
ANSI = {
    "green":    "\033[92m",
    "yellow":   "\033[93m",
    "orange":   "\033[33m",
    "red":      "\033[91m",
    "critical": "\033[1m\033[91m",  # Bold + Bright Red
    "reset":    "\033[0m",
    "bold":     "\033[1m",
    "dim":      "\033[2m",
}


def print_result(scenario: dict, predicted_class: int) -> None:
    """In kết quả so sánh class dự đoán vs class thực tế lên terminal."""
    actual_class = scenario.get("class", -1)
    label, color = CLASS_LABELS[predicted_class]
    is_correct = (predicted_class == actual_class)
    verdict = f"{ANSI['green']}ĐÚNG ✓{ANSI['reset']}" if is_correct \
              else f"{ANSI['red']}SAI ✗  (Expected: Class {actual_class}){ANSI['reset']}"

    print(f"  Kịch bản : {ANSI['bold']}{scenario.get('_scenario', 'N/A')}{ANSI['reset']}")
    print(f"  Đầu vào  : temp={scenario['temp']}°C | hum={scenario['hum']}% | "
          f"smoke={scenario['smoke']} | gas={scenario['gas']}ppm | co={scenario['co']}ppm")
    print(f"  Dự đoán  : {ANSI[color]}{label} (Class {predicted_class}){ANSI['reset']}")
    print(f"  Kết quả  : {verdict}")


# =============================================================================
# KHỐI KIỂM THỬ TỰ ĐỘNG
# =============================================================================

if __name__ == "__main__":
    # Đường dẫn tới file JSON mock data (tương đối so với script này)
    MOCK_DATA_PATH = Path(__file__).parent.parent / "data" / "mock_sensor_data.json"

    print("=" * 65)
    print("  PBL4 Fire Safety – Baseline Heuristic Validation")
    print("=" * 65)

    # ── 1. Đọc file JSON ──────────────────────────────────────────────────────
    if not MOCK_DATA_PATH.exists():
        print(f"\n✗ Không tìm thấy file mock data tại: {MOCK_DATA_PATH}")
        print("  Hãy chắc chắn file 'mock_sensor_data.json' tồn tại trong data/")
        exit(1)

    with open(MOCK_DATA_PATH, encoding="utf-8") as f:
        scenarios = json.load(f)

    print(f"\nĐã nạp {len(scenarios)} kịch bản từ: {MOCK_DATA_PATH.name}\n")

    # ── 2. Chạy evaluate_risk() cho từng kịch bản ────────────────────────────
    correct_count = 0
    results = []

    for i, sc in enumerate(scenarios):
        predicted = evaluate_risk(
            temp  = sc["temp"],
            hum   = sc["hum"],
            smoke = sc["smoke"],
            gas   = sc["gas"],
            co    = sc["co"],
        )
        is_correct = (predicted == sc["class"])
        if is_correct:
            correct_count += 1

        results.append({**sc, "predicted": predicted, "correct": is_correct})

        print(f"{'─' * 65}")
        print(f"  [{i+1}/{len(scenarios)}]")
        print_result(sc, predicted)

    # ── 3. In tổng kết ───────────────────────────────────────────────────────
    print(f"\n{'═' * 65}")
    accuracy = correct_count / len(scenarios) * 100
    acc_color = "green" if accuracy == 100 else ("yellow" if accuracy >= 60 else "red")
    print(f"  KẾT QUẢ TỔNG HỢP: {ANSI[acc_color]}{correct_count}/{len(scenarios)} đúng "
          f"({accuracy:.0f}%){ANSI['reset']}")

    if accuracy < 100:
        print(f"\n  {ANSI['yellow']}⚠  Có kịch bản phân loại sai. Điều chỉnh các ngưỡng"
              f"\n     THRESHOLD ở đầu file baseline_heuristic.py.{ANSI['reset']}")
    else:
        print(f"\n  {ANSI['green']}Baseline heuristic hoạt động chính xác 100% trên mock data.{ANSI['reset']}")
        print(f"  Sẵn sàng dùng làm nhãn (label) cho pipeline huấn luyện Random Forest.")

    print("=" * 65)
