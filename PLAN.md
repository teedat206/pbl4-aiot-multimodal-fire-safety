# Kế hoạch thiết lập dự án PBL4

## Trạng thái hiện tại

- Đây là ứng dụng Flutter tên `pbl4_app`, đang ở phiên bản `1.0.0+1`.
- Cấu hình yêu cầu Dart SDK `^3.13.2`; dependency hiện chỉ gồm Flutter và `cupertino_icons`.
- Dự án đã có các nền tảng Android, iOS, web, Windows, macOS và Linux.
- Cấu trúc mã, tài liệu và kiểm thử đã tồn tại nhưng chưa được khảo sát chi tiết để tránh quét repo không cần thiết.
- `README.md` vẫn là mẫu Flutter mặc định; chưa nêu mục tiêu sản phẩm, kiến trúc hoặc cách chạy riêng của PBL4.

## Ưu tiên cao nhất

Xác định mục tiêu sản phẩm và luồng người dùng quan trọng nhất, rồi đối chiếu với entry point và các màn hình hiện có. Đây là điều kiện để mọi quyết định về kiến trúc, dependency và kiểm thử có căn cứ.

## Kế hoạch 5 bước

1. **Chốt phạm vi MVP và tiêu chí hoàn thành.**
   - Ghi rõ người dùng mục tiêu, 1–3 luồng cốt lõi, nền tảng phát hành đầu tiên và các giới hạn ngoài phạm vi.
   - **Verify:** một người trong nhóm có thể mô tả mục tiêu, luồng chính và tiêu chí nghiệm thu trong một đoạn ngắn mà không cần suy đoán.

2. **Khảo sát có mục tiêu phần mã liên quan đến luồng chính.**
   - Chỉ đọc entry point, các tệp dưới `lib/` được nó tham chiếu, và các test tương ứng; lập bản đồ màn hình, state và service hiện hữu.
   - **Verify:** có danh sách chính xác các điểm vào, màn hình/feature liên quan và khoảng trống so với MVP.

3. **Chọn cấu trúc feature và ranh giới kỹ thuật.**
   - Quyết định quản lý state, điều hướng, mô hình dữ liệu, nguồn API/lưu trữ và quy ước tệp theo nhu cầu đã xác nhận; chỉ thêm package khi có lý do cụ thể.
   - **Verify:** thiết kế ghi rõ ownership của từng feature và ứng dụng vẫn vượt qua `flutter analyze` sau thay đổi cấu trúc ban đầu.

4. **Triển khai theo lát cắt dọc của luồng ưu tiên.**
   - Hoàn thành từng lát cắt end-to-end: UI, state, dữ liệu và xử lý lỗi; chia thành các hạng mục nhỏ có tiêu chí nghiệm thu.
   - **Verify:** chạy được luồng chính trên nền tảng mục tiêu, gồm trường hợp thành công và ít nhất một lỗi dự kiến.

5. **Củng cố chất lượng và tài liệu bàn giao.**
   - Bổ sung test cho logic/luồng trọng yếu, cập nhật README với cách cài đặt–chạy–kiểm thử, và ghi lại các quyết định kỹ thuật quan trọng.
   - **Verify:** `flutter analyze` và `flutter test` đều thành công; một thành viên mới có thể chạy dự án chỉ theo README.

## Quy tắc khảo sát

Mỗi lần khảo sát tiếp theo chỉ mở các tệp cần để trả lời một câu hỏi cụ thể của kế hoạch trên. Không quét toàn bộ repository trừ khi phạm vi đã được thống nhất.
