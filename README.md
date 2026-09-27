# MacFanControl BootCamp (MBP 16" 2019)

Ứng dụng giám sát nhiệt độ CPU, GPU và điều khiển tốc độ quạt dành riêng cho **MacBook Pro 16-inch 2019** (Intel Core i9, AMD Radeon Pro 5300M/5500M/5600M, Chip Apple T2) chạy **Windows 10 Boot Camp**.

## 🚀 Tính năng chính
- **Giám sát phần cứng thời gian thực**:
  - Đo nhiệt độ CPU (Intel Core i9-9880H / i9-9980HK) qua Intel DTS / MSR.
  - Đo nhiệt độ GPU (AMD Radeon Pro) qua ADL API.
  - Đọc tốc độ quạt trái (CPU Fan) và quạt phải (GPU Fan) theo thời gian thực (RPM).
- **Điều khiển quạt thông minh**:
  - Hỗ trợ chế độ quạt tự động theo **Custom Fan Curve** (đường cong nhiệt độ).
  - Chế độ thủ công (Manual Slider / Cố định tốc độ RPM mong muốn).
  - Chế độ Turbo Max Blast (100% tốc độ quạt khi chơi game hoặc render nặng).
  - Khôi phục chế độ mặc định an toàn của Apple Boot Camp khi thoát ứng dụng.
- **Giao diện & Tiện ích**:
  - Giao diện Fluent Dark hiện đại viết bằng C# WPF (.NET 8).
  - Thu nhỏ khay hệ thống (System Tray) với biểu tượng động hiển thị số nhiệt độ trực tiếp trên thanh Taskbar.
  - Tự động chạy cùng Windows không bị chặn bởi hộp thoại UAC (qua Windows Task Scheduler).

## 💻 Yêu cầu hệ thống
- MacBook Pro 16-inch (2019) – Identifier: `MacBookPro16,1` hoặc `MacBookPro16,4`.
- Hệ điều hành: Windows 10 x64 (Boot Camp) kèm đầy đủ Boot Camp Support Software.
- .NET 8.0 Desktop Runtime x64.
- Quyền Administrator (để giao tiếp với cảm biến phần cứng & SMC).

## 🛠️ Công nghệ
- **Ngôn ngữ**: C# (.NET 8)
- **Framework UI**: WPF (Windows Presentation Foundation)
- **Hardware Sensor Provider**: LibreHardwareMonitorLib
- **Apple SMC Provider**: T2 SMC Controller Interface
