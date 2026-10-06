# Quản lý state & Dependency Injection (Riverpod 3)

> Cấu trúc thư mục: xem `docs/KIEN_TRUC.md` (chia theo layer core/data/domain/presentation/routes). Tài liệu này mô tả cách state và DI hoạt động.

## 1. Vì sao chọn Riverpod (không dùng Bloc)

- Riverpod vừa là **state management** vừa là **DI container**: không cần thêm `get_it` hay `RepositoryProvider`.
- Dio, token storage, repository đều phụ thuộc lẫn nhau → dùng `ref.watch` để nối, không cần khởi tạo thủ công.
- Test dễ: thay bất kỳ phần nào bằng `overrideWithValue` mà không sửa code production.
- Không dùng code generation (`riverpod_generator`) để tránh `build_runner`; provider viết tay, rõ ràng.

Nếu sau này đội muốn Bloc: giữ nguyên `core/` + Repository, chỉ thay lớp Controller bằng Cubit và dùng `RepositoryProvider` (hoặc `get_it`) cho DI.

## 2. Các tầng và hướng phụ thuộc

```
UI (Widget)                 ref.watch(provider)  /  ref.read(x.notifier).method()
   │
Controller (Notifier / AsyncNotifier)       ← giữ state màn hình, gọi repository
   │
UseCase (domain/)  →  Repository (interface ở domain/, impl ở data/)  ← chỉ nói chuyện với Dio, ném ApiException
   │
Dio  ←  AuthInterceptor / ErrorInterceptor
   │
TokenStorage (Secure)  +  AppConfig (flavor, baseUrl)
```

Chỉ phụ thuộc **xuống dưới**. Widget không bao giờ import Dio; Repository không biết Flutter.

## 3. Bản đồ provider hiện có

| Provider | Loại | Vai trò |
|---|---|---|
| `appConfigProvider` | `Provider<AppConfig>` | Cấu hình theo flavor. Được override trong `bootstrap()` |
| `tokenStorageProvider` | `Provider<TokenStorage>` | Lưu token an toàn |
| `dioProvider` | `Provider<Dio>` | HTTP client + interceptor |
| `authRepositoryProvider` (`data/providers`) | `Provider<AuthRepository>` | Nối interface domain → `AuthRepositoryImpl` |
| `loginUseCaseProvider` (`presentation/auth`) | `Provider<LoginUseCase>` | Use case đăng nhập |
| `routerProvider` (`routes`) | `Provider<GoRouter>` | Router theo flavor + chặn truy cập |
| `authControllerProvider` (`core/session`) | `NotifierProvider<AuthController, AuthStatus>` | Trạng thái phiên toàn app |
| `loginControllerProvider` | `AsyncNotifierProvider.autoDispose` | State màn hình đăng nhập |
| `themeModeProvider` | `NotifierProvider<…, ThemeMode>` | Sáng/tối |

Import gọn: `import 'package:smart_drone_delivery/app/providers.dart';`

## 4. Quy ước

1. **`ref.watch`** trong `build()` của widget/provider (để rebuild khi state đổi); **`ref.read`** trong hàm xử lý sự kiện (onPressed, method của Notifier). Không dùng `ref.read` trong `build()` để lấy state.
2. State màn hình gọi API → dùng **`AsyncNotifier`** + **`AsyncValue.guard`**; UI hiển thị bằng `AsyncValueView` (xử lý loading / lỗi / retry giống nhau).
3. State của riêng một màn hình → thêm **`.autoDispose`**. State toàn app (auth, theme, config) → không autoDispose.
4. Repository **chỉ ném `ApiException`**. Controller/UI chuyển thành chữ bằng `errorMessage(error)`.
5. Mỗi provider mới nên đặt `name:` để log của `AppProviderObserver` dễ đọc.
6. Provider hạ tầng dùng chung → `core/…`; provider nối repository → `data/providers/`; provider use case và controller → `presentation/<tính năng>/`. Muốn xuất ra toàn app thì thêm một dòng `export` vào `app/providers.dart`.
7. Provider riêng cho flavor: truyền `overrides` vào `bootstrap()` ở `main_customer.dart` / `main_operator.dart`:
   ```dart
   void main() => bootstrap(AppConfig.customer(), overrides: [
     // ví dụ: stationRepositoryProvider.overrideWith((ref) => CustomerStationRepository(...)),
   ]);
   ```

## 5. Thêm một tính năng mới

Xem checklist đầy đủ ở `docs/KIEN_TRUC.md` (mục 4). Mẫu hoàn chỉnh để copy là luồng đăng nhập:
`domain/repositories/auth_repository.dart` → `domain/usecases/login_usecase.dart` → `data/repositories/auth_repository_impl.dart` → `data/providers/repository_providers.dart` → `presentation/auth/`.

## 6. Ghi chú về `bootstrap.dart` và phiên bản

- `bootstrap()` thêm `AppProviderObserver` (chỉ ở debug), tắt auto-retry của Riverpod 3 (`retry: (_, __) => null`) và nhận tham số tuỳ chọn `overrides` để mỗi flavor có DI riêng.
- Code dùng API của Riverpod 3 (`ProviderObserverContext`, tham số `retry`); `init_project.sh` ghim `flutter_riverpod:^3.0.0`.

## 7. Lưu ý đã biết

- `AuthController.build()` gọi `_restore()` bất đồng bộ. Nếu gọi `signedIn()` trước khi restore xong thì kết quả vẫn đúng, nhưng thứ tự này phụ thuộc vào hàng đợi microtask. Khi làm màn hình splash, nên đợi `authControllerProvider != AuthStatus.unknown` rồi mới điều hướng.
- Endpoint `/auth/login`, `/auth/logout`, `/auth/refresh` và các key JSON là giả định, cần khớp với backend.
