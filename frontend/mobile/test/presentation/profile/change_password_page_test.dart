import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/core/session/auth_controller.dart';
import 'package:smart_drone_delivery/core/storage/auth_tokens.dart';
import 'package:smart_drone_delivery/core/storage/storage_providers.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';
import 'package:smart_drone_delivery/domain/repositories/profile_repository.dart';
import 'package:smart_drone_delivery/presentation/profile/change_password_page.dart';

import '../../support/test_helpers.dart';

class MockProfileRepo implements ProfileRepository {
  bool throw401 = false;
  bool changePasswordCalled = false;

  @override
  Future<User> getProfile() async => User(
        id: '1',
        email: 'test@example.com',
        fullName: 'Test User',
        phoneNumber: '0901234567',
        status: 'ACTIVE',
        roleCode: 'CUSTOMER',
        createdAt: DateTime(2026, 1, 1),
      );

  @override
  Future<User> updateProfile({
    required String fullName,
    required String phoneNumber,
  }) async =>
      getProfile();

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    changePasswordCalled = true;
    if (throw401) {
      throw const ApiException(
        statusCode: 401,
        message: 'Current password does not match.',
      );
    }
  }

  @override
  Future<String> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async =>
      'https://example.com/avatar.png';

  @override
  Future<String> uploadAvatarFromFile(String filePath) async =>
      'https://example.com/avatar.png';
}

void main() {
  group('ChangePasswordPage flows', () {
    late InMemoryTokenStorage storage;
    late MockProfileRepo mockRepo;

    setUp(() async {
      storage = InMemoryTokenStorage();
      await storage.write(
        const AuthTokens(accessToken: 'access', refreshToken: 'refresh'),
      );
      mockRepo = MockProfileRepo();
    });

    testWidgets('thành công -> gọi signOut() và hiện thông báo đăng nhập lại',
        (tester) async {
      final container = createContainer(
        overrides: [
          tokenStorageProvider.overrideWithValue(storage),
          profileRepositoryProvider.overrideWithValue(mockRepo),
        ],
      );

      // Verify user is initially authenticated
      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: container,
          child: const MaterialApp(
            home: Scaffold(body: ChangePasswordPage()),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(container.read(authControllerProvider), AuthStatus.authenticated);

      // Enter passwords
      final textFields = find.byType(TextFormField);
      await tester.enterText(textFields.at(0), 'OldPass123');
      await tester.enterText(textFields.at(1), 'NewPass123');
      await tester.enterText(textFields.at(2), 'NewPass123');
      await tester.pumpAndSettle();

      // Tap submit
      await tester.tap(find.widgetWithText(FilledButton, 'Update Password'));
      await tester.pumpAndSettle();

      expect(mockRepo.changePasswordCalled, isTrue);
      expect(container.read(authControllerProvider), AuthStatus.unauthenticated);
      expect(await storage.read(), isNull);
      expect(find.text('Đổi mật khẩu thành công. Vui lòng đăng nhập lại.'),
          findsOneWidget);
    });

    testWidgets('thất bại với 401 -> hiện lỗi và không đăng xuất',
        (tester) async {
      mockRepo.throw401 = true;

      final container = createContainer(
        overrides: [
          tokenStorageProvider.overrideWithValue(storage),
          profileRepositoryProvider.overrideWithValue(mockRepo),
        ],
      );

      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: container,
          child: const MaterialApp(
            home: Scaffold(body: ChangePasswordPage()),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(container.read(authControllerProvider), AuthStatus.authenticated);

      // Enter wrong current password
      final textFields = find.byType(TextFormField);
      await tester.enterText(textFields.at(0), 'WrongPass123');
      await tester.enterText(textFields.at(1), 'NewPass123');
      await tester.enterText(textFields.at(2), 'NewPass123');
      await tester.pumpAndSettle();

      // Tap submit
      await tester.tap(find.widgetWithText(FilledButton, 'Update Password'));
      await tester.pumpAndSettle();

      expect(mockRepo.changePasswordCalled, isTrue);
      // User stays authenticated, tokens are preserved
      expect(container.read(authControllerProvider), AuthStatus.authenticated);
      expect(await storage.read(), isNotNull);
      expect(find.text('Current password does not match.'), findsOneWidget);
    });
  });
}
