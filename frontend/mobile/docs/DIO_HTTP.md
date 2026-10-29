# Dio HTTP client: base URL, timeout, logging

File liên quan (đều trong `lib/core/network/`):

| File | Vai trò |
|---|---|
| `network_config.dart` | `NetworkConfig`: baseUrl, 3 timeout, tuỳ chọn log. Đọc từ `--dart-define` |
| `dio_client.dart` | `DioClient.create()` dựng `Dio` + gắn interceptor |
| `logging_interceptor.dart` | `AppLogInterceptor` (log đẹp, có thời gian) + `LogRedactor` (che dữ liệu nhạy cảm) |
| `auth_interceptor.dart` / `error_interceptor.dart` | Gắn token + refresh; đổi lỗi thành `ApiException` |
| `network_providers.dart` | `networkConfigProvider`, `dioProvider` (DI) |

## 1. Base URL

`BaseOptions.baseUrl` lấy từ `NetworkConfig.baseUrl` (mặc định là `AppConfig.baseUrl`). Đổi theo môi trường lúc build, không sửa code:

```bash
flutter run -t lib/main_customer.dart --flavor customer \
  --dart-define=API_BASE_URL=https://staging.example.com/v1
```

Lưu ý:
- Ghi luôn version trong baseUrl (`.../v1`); khi gọi chỉ viết `'/stations'` → URL cuối là `https://…/v1/stations`.
- Đừng truyền URL tuyệt đối (`https://…`) vào `dio.get(...)`: Dio sẽ bỏ qua baseUrl.
- `DioClient.create` kiểm tra baseUrl và ném `ArgumentError` ngay nếu rỗng/sai (ví dụ quên giá trị `--dart-define`), thay vì để app lỗi khó hiểu lúc gọi API.
- Dùng `https` ở bản thật; Android 9+ và iOS mặc định chặn `http://`.

## 2. Timeout

| Tham số | Ý nghĩa | Mặc định | `--dart-define` |
|---|---|---|---|
| `connectTimeout` | Chờ thiết lập kết nối (DNS + TCP + TLS) | 15 s | `HTTP_CONNECT_TIMEOUT_MS` |
| `sendTimeout` | Chờ gửi xong body lên server (chỉ khi có body; không hỗ trợ trên web) | 20 s | `HTTP_SEND_TIMEOUT_MS` |
| `receiveTimeout` | Chờ server trả dữ liệu (tính giữa các lần nhận dữ liệu, **không phải** tổng thời gian) | 20 s | `HTTP_RECEIVE_TIMEOUT_MS` |

- Hết giờ → Dio ném `DioException` loại `connectionTimeout` / `sendTimeout` / `receiveTimeout`. `ErrorInterceptor` đổi thành `ApiException(isNetwork: true)` với thông báo "Network problem…", UI hiển thị bằng `errorMessage(e)`.
- Endpoint đặc biệt (upload ảnh, báo cáo nặng) đổi riêng từng request:
  ```dart
  dio.post('/upload', data: form, options: Options(sendTimeout: const Duration(minutes: 2)));
  ```
- Muốn giới hạn **tổng** thời gian của một lời gọi: dùng `CancelToken` kết hợp `Future.timeout`.
- Trạng thái HTTP 4xx/5xx không phải timeout: `validateStatus` giữ mặc định (chỉ 2xx là thành công), còn lại Dio ném `DioException.badResponse`.

## 3. Logging

`AppLogInterceptor` ghi 3 loại dòng:

```
--> POST https://api.example.com/v1/auth/login
    body: {"email":"a@b.c","password":"***"}
<-- 200 POST https://api.example.com/v1/auth/login (123 ms)
    body: {"access_token":"***","user":{"name":"An"}}
<-x 401 GET https://api.example.com/v1/me (87 ms) [badResponse]
    body: {"message":"Token expired"}
```

**Bảo mật (quan trọng):**
- Header `Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key` luôn bị che thành `***`.
- Trong body JSON (kể cả lồng nhau), các key `password`, `token`, `access_token`, `refresh_token`, `otp`, `pin`, `cvv`, `card_number`… bị che. Thêm key mới ở `LogRedactor.sensitiveKeys`.
- **Log luôn tắt ở bản release**, kể cả khi truyền `HTTP_LOGGING=true` (`enableLogging: !kReleaseMode && …`).
- Body dài bị cắt ở `maxLogBodyLength` (mặc định 2000 ký tự); file upload (`FormData`) và dữ liệu nhị phân chỉ ghi chú thích, không in nội dung.

Tuỳ chọn:

| `--dart-define` | Mặc định | Tác dụng |
|---|---|---|
| `HTTP_LOGGING=false` | `true` (debug) | Tắt hẳn log |
| `HTTP_LOG_HEADERS=true` | `false` | In header (đã che) |
| `HTTP_LOG_BODIES=false` | `true` | Không in body |
| `HTTP_LOG_MAX_BODY=500` | `2000` | Độ dài tối đa của body |

### Vì sao Log đứng đầu danh sách interceptor?

Thứ tự gắn: `[Log, Auth, Error]`. Dio chạy interceptor theo đúng thứ tự này cho cả request, response và error.

- Khi API trả 401, `Log` thấy lỗi **trước** khi `AuthInterceptor` refresh token và gửi lại → log thể hiện đủ chuỗi: `401` → `POST /auth/refresh` → request gửi lại `200`.
- Lời gọi refresh dùng một `Dio` riêng (`refreshDio`), nên mình gắn thêm một `AppLogInterceptor` cho nó.
- Đánh đổi: dòng `-->` được ghi trước khi `AuthInterceptor` gắn token, nên log request không hiện header Authorization (dù có bật `HTTP_LOG_HEADERS`).

## 4. Test (không cần mạng)

`test/support/fake_http_adapter.dart` thay lớp mạng thật bằng hàm giả. Các test kiểm tra: BaseOptions, thứ tự interceptor, baseUrl sai, timeout → `ApiException`, ghép đường dẫn, và việc log không lộ `Authorization` / `password` / token.

Trong test của feature, thay cấu hình mạng bằng:
```dart
networkConfigProvider.overrideWithValue(const NetworkConfig(baseUrl: 'https://api.test/v1')),
```
