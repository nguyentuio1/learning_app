import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:smart_drone_delivery/app/app.dart';
import 'package:smart_drone_delivery/app/providers.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/providers/landing_station_providers.dart';
import 'package:smart_drone_delivery/core/theme/app_theme.dart';
import 'package:smart_drone_delivery/data/services/landing_station_api_service.dart';
import 'package:smart_drone_delivery/presentation/auth/login_page.dart';
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
  }) async =>
      const [];
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
      landingStationApiServiceProvider.overrideWithValue(
        _EmptyStationService(),
      ),
    ];

void main() {
  testWidgets('Customer đã đăng nhập -> vào Customer home', (tester) async {
    await _pump(tester, AppConfig.customer(), await _loggedIn());
    expect(find.text('Customer app'), findsOneWidget);
  });

  testWidgets('Operator đã đăng nhập -> vào Operator home', (tester) async {
    await _pump(
      tester,
      AppConfig.operator(),
      await _loggedIn(),
      extra: _operatorOverrides(),
    );
    // Operator home shows the availability dashboard title.
    expect(find.text('Station availability'), findsOneWidget);
  });

  testWidgets('Chưa đăng nhập -> bị chuyển tới Login', (tester) async {
    await _pump(tester, AppConfig.customer(), InMemoryTokenStorage());
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.text('Customer app'), findsNothing);
  });

  testWidgets('Register validates inline and link returns to Login', (
    tester,
  ) async {
    await _pump(tester, AppConfig.customer(), InMemoryTokenStorage());
    await tester.tap(find.text('Create a customer account'));
    await tester.pumpAndSettle();

    expect(find.text('Create account'), findsOneWidget);
    await tester.tap(find.text('Register'));
    await tester.pumpAndSettle();
    expect(find.text('Enter your full name'), findsOneWidget);
    expect(find.text('Enter a valid email'), findsOneWidget);
    expect(
      find.text('Enter a phone number with 9 to 11 digits'),
      findsOneWidget,
    );

    await tester.tap(find.text('Already have an account? Log in'));
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
        ],
        child: const App(),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Create a customer account'));
    await tester.pumpAndSettle();
    final fields = find.byType(TextFormField);
    await tester.enterText(fields.at(0), 'Test Customer');
    await tester.enterText(fields.at(1), 'used@example.com');
    await tester.enterText(fields.at(2), '0901234567');
    await tester.enterText(fields.at(3), 'Passw0rd12');
    await tester.tap(find.text('Register'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.text('Email is already registered.'), findsOneWidget);
  });

  testWidgets('successful registration signs in and opens My orders', (
    tester,
  ) async {
    await _pump(tester, AppConfig.customer(), InMemoryTokenStorage());
    await tester.tap(find.text('Create a customer account'));
    await tester.pumpAndSettle();
    final fields = find.byType(TextFormField);
    await tester.enterText(fields.at(0), 'Test Customer');
    await tester.enterText(fields.at(1), 'new@example.com');
    await tester.enterText(fields.at(2), '0901234567');
    await tester.enterText(fields.at(3), 'Passw0rd12');
    await tester.tap(find.text('Register'));
    await tester.pumpAndSettle();

    expect(find.text('My orders'), findsOneWidget);
  });

  testWidgets('Đăng xuất -> router đưa về Login', (tester) async {
    await _pump(tester, AppConfig.customer(), await _loggedIn());
    await tester.tap(find.byIcon(Icons.logout));
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
