import 'package:smart_drone_delivery/app/app.dart';
import 'package:smart_drone_delivery/app/providers.dart';
import 'package:smart_drone_delivery/core/storage/token_storage.dart';
import 'package:smart_drone_delivery/core/theme/app_theme.dart';
import 'package:smart_drone_delivery/presentation/auth/login_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/test_helpers.dart';

Future<void> _pump(WidgetTester tester, AppConfig config, TokenStorage storage) async {
  await tester.pumpWidget(ProviderScope(
    overrides: [
      appConfigProvider.overrideWithValue(config),
      tokenStorageProvider.overrideWithValue(storage),
    ],
    child: const App(),
  ));
  await tester.pumpAndSettle();
}

Future<InMemoryTokenStorage> _loggedIn() async {
  final s = InMemoryTokenStorage();
  await s.write(const AuthTokens(accessToken: 'a', refreshToken: 'r'));
  return s;
}

void main() {
  testWidgets('Customer đã đăng nhập -> vào Customer home', (tester) async {
    await _pump(tester, AppConfig.customer(), await _loggedIn());
    expect(find.text('Customer app'), findsOneWidget);
  });

  testWidgets('Operator đã đăng nhập -> vào Operator home', (tester) async {
    await _pump(tester, AppConfig.operator(), await _loggedIn());
    expect(find.text('Station Operator app'), findsOneWidget);
  });

  testWidgets('Chưa đăng nhập -> bị chuyển tới Login', (tester) async {
    await _pump(tester, AppConfig.customer(), InMemoryTokenStorage());
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.text('Customer app'), findsNothing);
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
