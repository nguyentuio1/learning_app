# Charge Apps (Customer + Station Operator)

One Flutter codebase, two apps, selected by entrypoint + flavor.

## Setup
```bash
flutter create --org com.example --project-name charge_apps --platforms=android,ios .
flutter pub add flutter_riverpod dio flutter_secure_storage
```
(`flutter create .` keeps the `lib/` files from this scaffold.)

## Run
```bash
flutter run -t lib/main_customer.dart --flavor customer --dart-define=API_BASE_URL=https://staging.example.com/v1
flutter run -t lib/main_operator.dart --flavor operator --dart-define=API_BASE_URL=https://staging.example.com/v1
```
(Drop `--flavor` until native flavors are configured; the Dart side works without it.)

## Native flavors (needed for two distinct store apps)
- **Android** `android/app/build.gradle(.kts)`: add `productFlavors` `customer` (`applicationId com.example.charge`, label "Charge") and `operator` (`com.example.charge.station`, label "Charge Station"); use `${applicationName}` via `resValue`/manifest placeholder.
- **iOS**: duplicate the Runner scheme into `customer` / `operator`, add build configs with distinct bundle IDs and display names.
- Android: `flutter_secure_storage` needs `minSdkVersion 23`.

## Structure
```
lib/
  main_customer.dart / main_operator.dart   # entrypoints
  app/            bootstrap (zones, ProviderScope), root App widget
  core/
    config/       AppConfig + flavor, API base URL
    theme/        colors (seed per flavor), ThemeData, theme-mode provider
    network/      Dio factory, Auth/Error interceptors, providers
    storage/      TokenStorage (secure)
    error/        ApiException
    widgets/      shared UI
  features/
    auth/         shared login/session (data/domain/presentation)
    customer/     customer-only features
    operator/     operator-only features
```
Add features as `features/<area>/{data,domain,presentation}`.

## Token flow
Request -> bearer attached -> 401 -> single-flight refresh -> replay once.
Refresh rejected -> storage cleared -> `AuthController.onSessionExpired()` (route to login by watching `authControllerProvider`).
Adjust `/auth/refresh` and the JSON keys in `auth_interceptor.dart` to your API.
