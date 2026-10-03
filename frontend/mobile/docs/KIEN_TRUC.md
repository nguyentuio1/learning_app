# Kiến trúc project: core · data · domain · presentation · routes

Chia theo **layer** (lớp). Trong mỗi layer, code chia tiếp theo tính năng (`auth/`, sau này `stations/`, `charging/`…) và theo app (`customer/`, `operator/`) ở tầng giao diện.

```
lib/
├─ main_customer.dart / main_operator.dart   Entrypoint của 2 app
├─ app/            Điểm lắp ráp: bootstrap, widget App, barrel providers.dart
├─ core/           Hạ tầng dùng chung, KHÔNG biết gì về nghiệp vụ
│  ├─ config/      AppConfig, flavor, API_BASE_URL
│  ├─ constants/   ApiEndpoints
│  ├─ di/          AppProviderObserver (log state khi debug)
│  ├─ error/       ApiException, errorMessage()
│  ├─ network/     NetworkConfig (baseUrl, timeout, log), Dio, Log/Auth/Error interceptor
│  ├─ session/     AuthController (trạng thái đăng nhập toàn app)
│  ├─ storage/     AuthTokens, TokenStorage (secure), storage_providers
│  ├─ theme/       màu theo flavor, ThemeData, theme mode
│  └─ utils/       hàm tiện ích
├─ domain/         NGHIỆP VỤ – Dart thuần (không Flutter/Dio/Riverpod)
│  ├─ entities/    đối tượng nghiệp vụ (User, Station… – thêm khi cần)
│  ├─ repositories/ interface (AuthRepository)
│  └─ usecases/    mỗi file một hành động (LoginUseCase)
├─ data/           LẤY/LƯU dữ liệu – triển khai interface của domain
│  ├─ datasources/ remote/ (API), local/ (cache, DB)
│  ├─ models/      DTO + fromJson/toJson, chuyển sang entity
│  ├─ repositories/ AuthRepositoryImpl (dùng Dio)
│  └─ providers/   repository_providers.dart: nối interface → implementation
├─ presentation/   GIAO DIỆN + state màn hình
│  ├─ common/      SplashPage, widgets/AsyncValueView
│  ├─ auth/        LoginPage, LoginController, loginUseCaseProvider
│  ├─ customer/    màn hình riêng của app Customer
│  └─ operator/    màn hình riêng của app Station Operator
└─ routes/         ĐIỀU HƯỚNG
   ├─ route_paths.dart   hằng số đường dẫn
   └─ app_router.dart    GoRouter theo flavor + chặn truy cập theo đăng nhập
```

## 1. Quy tắc phụ thuộc (ai được import ai)

```
presentation ──► domain ◄── data
     │             │          │
     ▼             ▼          ▼
   routes          └────► core ◄───┘
```

| Layer | Được import | Cấm |
|---|---|---|
| `core` | chỉ `core` | domain, data, presentation, routes |
| `domain` | `core`, `domain` + **không** dùng flutter / dio / riverpod / go_router | data, presentation, routes |
| `data` | `core`, `domain`, `data` | presentation, routes |
| `presentation` | `core`, `domain`, `data`(chỉ providers), `routes` | — |
| `routes` | `core`, `presentation` | domain, data |
| `app`, `main_*` | tất cả | — |

Chạy `python3 tool/check_architecture.py` để kiểm tra tự động (đã gắn vào `init_project.sh` và `verify_build.sh`).

Hai chỗ đặt hơi "đặc biệt", có chủ đích:
- **`core/session/AuthController`**: trạng thái phiên được cả Dio (khi refresh thất bại), router và UI dùng → để ở `core` thì không layer nào bị phụ thuộc ngược.
- **`core/storage/auth_tokens.dart`** là Dart thuần để `domain` dùng được `AuthTokens`.

## 2. Một yêu cầu đi qua các layer như thế nào (ví dụ đăng nhập)

```
LoginPage (presentation)         bấm nút → ref.read(loginControllerProvider.notifier).login()
  └─ LoginController             AsyncNotifier, giữ state loading/lỗi
       └─ LoginUseCase (domain)  chuẩn hoá email, gọi repository (interface)
            └─ AuthRepositoryImpl (data)   Dio POST ApiEndpoints.login → AuthTokens / ApiException
       └─ AuthController.signedIn()        lưu token, đổi AuthStatus = authenticated
app_router (routes)              nghe AuthStatus → tự chuyển từ /login sang trang chủ của flavor
```

## 3. Routes

- `RoutePaths` giữ tất cả đường dẫn; màn hình gọi `context.go(RoutePaths.login)`.
- `routerProvider` tạo `GoRouter` **theo flavor**: Customer chỉ có `/customer`, Operator chỉ có `/operator`.
- Redirect theo `AuthStatus`: `unknown → /splash`, `unauthenticated → /login`, `authenticated → trang chủ` (khi đang ở splash/login).
- Đăng xuất hoặc refresh token bị từ chối → `AuthStatus` đổi → router tự đưa về `/login`, không cần gọi điều hướng thủ công.
- Gói dùng: `go_router`.

## 4. Thêm một tính năng mới (ví dụ `stations`)

1. `domain/entities/station.dart`, `domain/repositories/station_repository.dart`, `domain/usecases/get_nearby_stations_usecase.dart`
2. `data/models/station_model.dart` (fromJson + `toEntity()`), `data/repositories/station_repository_impl.dart`; thêm `stationRepositoryProvider` vào `data/providers/repository_providers.dart`
3. `presentation/customer/stations/` gồm `stations_controller.dart` (AsyncNotifier) và `stations_page.dart` (dùng `AsyncValueView`)
4. `routes/route_paths.dart` thêm đường dẫn; `routes/app_router.dart` thêm `GoRoute` vào đúng flavor
5. Thêm endpoint vào `core/constants/api_endpoints.dart`
6. `python3 tool/check_architecture.py` + viết test theo mẫu `test/core/di/di_wiring_test.dart`
