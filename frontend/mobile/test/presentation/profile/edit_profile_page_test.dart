import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';
import 'package:smart_drone_delivery/domain/repositories/profile_repository.dart';
import 'package:smart_drone_delivery/presentation/profile/edit_profile_page.dart';

import '../../support/test_helpers.dart';

class MockProfileRepoEdit implements ProfileRepository {
  bool throw409 = false;

  @override
  Future<User> getProfile() async => User(
        id: '1',
        email: 'user@example.com',
        fullName: 'Original Name',
        phoneNumber: '0901234567',
        status: 'ACTIVE',
        roleCode: 'CUSTOMER',
        createdAt: DateTime(2026, 1, 1),
      );

  @override
  Future<User> updateProfile({
    required String fullName,
    required String phoneNumber,
  }) async {
    if (throw409) {
      throw const ApiException(
        statusCode: 409,
        message: 'Phone number is already registered.',
      );
    }
    return User(
      id: '1',
      email: 'user@example.com',
      fullName: fullName,
      phoneNumber: phoneNumber,
      status: 'ACTIVE',
      roleCode: 'CUSTOMER',
      createdAt: DateTime(2026, 1, 1),
    );
  }

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {}

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
  group('EditProfilePage tests', () {
    late MockProfileRepoEdit mockRepo;

    setUp(() {
      mockRepo = MockProfileRepoEdit();
    });

    testWidgets('Lỗi 409 trùng số điện thoại hiện thông báo thân thiện và set lỗi input',
        (tester) async {
      mockRepo.throw409 = true;

      final container = createContainer(
        overrides: [
          profileRepositoryProvider.overrideWithValue(mockRepo),
        ],
      );

      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: container,
          child: const MaterialApp(
            home: Scaffold(body: EditProfilePage()),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Enter a new phone number that triggers 409
      final phoneField = find.widgetWithText(TextFormField, 'Phone Number *');
      await tester.enterText(phoneField, '0988776655');
      await tester.pumpAndSettle();

      // Save changes
      await tester.tap(find.widgetWithText(FilledButton, 'Save Changes'));
      await tester.pumpAndSettle();

      // Verify custom 409 friendly message and input field error
      expect(find.text('Số điện thoại này đã được sử dụng bởi tài khoản khác.'),
          findsOneWidget);
      expect(find.text('Số điện thoại đã được đăng ký'), findsOneWidget);
    });
  });
}
