import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:smart_drone_delivery/app/app.dart';
import 'package:smart_drone_delivery/app/providers.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/providers/landing_station_providers.dart';
import 'package:smart_drone_delivery/data/providers/notification_providers.dart';
import 'package:smart_drone_delivery/core/theme/app_theme.dart';
import 'package:smart_drone_delivery/data/services/landing_station_api_service.dart';
import 'package:smart_drone_delivery/presentation/auth/login_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/my_orders_provider.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/test_helpers.dart';

/// Fake service trả về danh sách trống để smoke test không cần mạng.
class _EmptyStationService extends LandingStationApiService {
  _EmptyStationService() : super(Dio());

  @override
  Future<List<LandingStation>> getStations({
    String? status,
    String? search,
  }) async => const [];
}

class _EmptyOrdersNotifier extends MyOrdersNotifier {
  @override
  Future<List<DeliveryOrder>> build() async => [];
}

Future<void> _pump(
  WidgetTester tester,
  AppConfig config,
  TokenStorage storage, {
  List<Override> extra = const [],
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        appConfigProvider.overrideWithValue(config),
        tokenStorageProvider.overrideWithValue(storage),
        authRepositoryProvider.overrideWithValue(FakeAuthRepository()),
        userStorageProvider.overrideWithValue(InMemoryUserStorage()),
        myOrdersProvider.overrideWith(_EmptyOrdersNotifier.new),
        unreadNotificationsCountProvider.overrideWith((ref) async => 0),
        ...extra,
      ],
      child: const App(),
    ),
  );
  await tester.pumpAndSettle();
}

Future<InMemoryTokenStorage> _loggedIn() async {
  final s = InMemoryTokenStorage();
  await s.write(const AuthTokens(accessToken: 'a', refreshToken: 'r'));
  return s;
}

/// Override station service so the Operator home loads without a backend.
List<Override> _operatorOverrides() => [
  landingStationApiServiceProvider.overrideWithValue(_EmptyStationService()),
];

Future<void> _openRegisterPage(WidgetTester tester) async {
  final link = find.text('Create account');
  await tester.ensureVisible(link);
  await tester.tap(link);
  await tester.pumpAndSettle();
}

Future<void> _fillRegistrationForm(
  WidgetTester tester, {
  required String email,
  String confirmPassword = 'Passw0rd12',
}) async {
  final values = [
    'Test Customer',
    email,
    '0901234567',
    confirmPassword,
    'Passw0rd12',
  ];
  final fields = find.byType(TextFormField);
  for (var index = 0; index < values.length; index++) {
    final field = fields.at(index);
    await tester.ensureVisible(field);
    await tester.enterText(field, values[index]);
  }
  tester.testTextInput.hide();
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('Customer đã đăng nhập -> vào Customer home', (tester) async {
    await _pump(tester, AppConfig.customer(), await _loggedIn());
    expect(find.text('Send a delivery'), findsOneWidget);
    expect(find.text('Create delivery'), findsWidgets);
  });

  testWidgets('Operator đã đăng nhập -> vào Operator home', (tester) async {
    await _pump(
      tester,
      AppConfig.operator(),
      await _loggedIn(),
      extra: _operatorOverrides(),
    );
    // Operator home shows the availability dashboard title.
    expect(find.text('Station operations'), findsOneWidget);
  });

  testWidgets('Chưa đăng nhập -> bị chuyển tới Login', (tester) async {
    await _pump(tester, AppConfig.customer(), InMemoryTokenStorage());
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.text('Send a delivery'), findsNothing);
    expect(find.text('Create delivery'), findsNothing);
  });

  testWidgets('Register validates inline and link returns to Login', (
    tester,
  ) async {
    await _pump(tester, AppConfig.customer(), InMemoryTokenStorage());
    await _openRegisterPage(tester);

    expect(find.text('Customers can register on their own.'), findsOneWidget);
    final submitButton = find.widgetWithText(FilledButton, 'Create account');
    await tester.ensureVisible(submitButton);
    await tester.tap(submitButton);
    await tester.pumpAndSettle();
    expect(find.text('Enter your full name'), findsNWidgets(2));
    expect(find.text('Enter a valid email'), findsOneWidget);
    expect(
      find.text('Enter a phone number with 9 to 11 digits'),
      findsOneWidget,
    );
    await _fillRegistrationForm(
      tester,
      email: 'new@example.com',
      confirmPassword: 'Different12',
    );
    await tester.ensureVisible(submitButton);
    await tester.tap(submitButton);
    await tester.pumpAndSettle();
    expect(find.text('Passwords do not match'), findsOneWidget);

    final backButton = find.byIcon(Icons.arrow_back);
    await tester.ensureVisible(backButton);
    await tester.tap(backButton);
    await tester.pumpAndSettle();
    expect(find.byType(LoginPage), findsOneWidget);
  });

  testWidgets('Register shows 409 API error in a snackbar', (tester) async {
    final config = AppConfig.customer();
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          appConfigProvider.overrideWithValue(config),
          tokenStorageProvider.overrideWithValue(InMemoryTokenStorage()),
          authRepositoryProvider.overrideWithValue(
            FakeAuthRepository(
              registerError: const ApiException(
                message: 'Email is already registered.',
                statusCode: 409,
              ),
            ),
          ),
          userStorageProvider.overrideWithValue(InMemoryUserStorage()),
          unreadNotificationsCountProvider.overrideWith((ref) async => 0),
        ],
        child: const App(),
      ),
    );
    await tester.pumpAndSettle();
    await _openRegisterPage(tester);
    await _fillRegistrationForm(tester, email: 'used@example.com');
    final submitButton = find.widgetWithText(FilledButton, 'Create account');
    await tester.ensureVisible(submitButton);
    await tester.tap(submitButton);
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.text('Email is already registered.'), findsOneWidget);
  });

  testWidgets('successful registration signs in and opens Customer home', (
    tester,
  ) async {
    await _pump(tester, AppConfig.customer(), InMemoryTokenStorage());
    await _openRegisterPage(tester);
    await _fillRegistrationForm(tester, email: 'new@example.com');
    final submitButton = find.widgetWithText(FilledButton, 'Create account');
    await tester.ensureVisible(submitButton);
    await tester.tap(submitButton);
    await tester.pumpAndSettle();

    expect(find.text('Send a delivery'), findsOneWidget);
    expect(find.text('Recent deliveries'), findsOneWidget);
  });

  testWidgets('Đăng xuất -> router đưa về Login', (tester) async {
    await _pump(tester, AppConfig.customer(), await _loggedIn());
    final container = ProviderScope.containerOf(
      tester.element(find.byType(App)),
    );
    await container.read(authControllerProvider.notifier).signOut();
    await tester.pumpAndSettle();
    expect(find.byType(LoginPage), findsOneWidget);
  });

  test('Mỗi flavor có màu theme khác nhau', () {
    expect(
      AppTheme.light(AppFlavor.customer).colorScheme.primary,
      isNot(AppTheme.light(AppFlavor.operator).colorScheme.primary),
    );
  });
}
