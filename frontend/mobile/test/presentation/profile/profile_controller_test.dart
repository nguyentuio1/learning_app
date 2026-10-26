import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';
import 'package:smart_drone_delivery/domain/repositories/profile_repository.dart';
import 'package:smart_drone_delivery/presentation/profile/profile_controller.dart';

import '../../support/test_helpers.dart';

class FakeProfileRepository implements ProfileRepository {
  User user = User(
    id: 'user-001',
    email: 'user@example.com',
    fullName: 'Test User',
    phoneNumber: '0901112233',
    avatarUrl: null,
    status: 'ACTIVE',
    roleCode: 'CUSTOMER',
    createdAt: DateTime(2026, 1, 1),
  );

  bool changePasswordCalled = false;
  String? lastCurrentPassword;
  String? lastNewPassword;

  @override
  Future<User> getProfile() async => user;

  @override
  Future<User> updateProfile({
    required String fullName,
    required String phoneNumber,
  }) async {
    user = user.copyWith(fullName: fullName, phoneNumber: phoneNumber);
    return user;
  }

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    changePasswordCalled = true;
    lastCurrentPassword = currentPassword;
    lastNewPassword = newPassword;
  }

  @override
  Future<String> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async {
    user = user.copyWith(avatarUrl: 'https://example.com/$filename');
    return user.avatarUrl!;
  }

  @override
  Future<String> uploadAvatarFromFile(String filePath) async {
    return uploadAvatar(bytes: [0], filename: 'avatar.png');
  }
}

void main() {
  group('ProfileController and related controllers', () {
    late FakeProfileRepository fakeRepo;

    setUp(() {
      fakeRepo = FakeProfileRepository();
    });

    test('profileControllerProvider loads user from repository', () async {
      final container = createContainer(
        overrides: [
          profileRepositoryProvider.overrideWithValue(fakeRepo),
        ],
      );

      final user = await container.read(profileControllerProvider.future);
      expect(user.fullName, 'Test User');
      expect(user.email, 'user@example.com');
      expect(user.phoneNumber, '0901112233');
    });

    test('updateProfileControllerProvider updates profile and updates profileController', () async {
      final container = createContainer(
        overrides: [
          profileRepositoryProvider.overrideWithValue(fakeRepo),
        ],
      );

      // Load initial profile
      await container.read(profileControllerProvider.future);

      // Execute update
      await container
          .read(updateProfileControllerProvider.notifier)
          .updateProfile(
            fullName: 'Updated Name',
            phoneNumber: '0909998877',
          );

      final updated = container.read(profileControllerProvider).value;
      expect(updated?.fullName, 'Updated Name');
      expect(updated?.phoneNumber, '0909998877');
    });

    test('changePasswordControllerProvider calls repository changePassword', () async {
      final container = createContainer(
        overrides: [
          profileRepositoryProvider.overrideWithValue(fakeRepo),
        ],
      );

      await container
          .read(changePasswordControllerProvider.notifier)
          .changePassword(
            currentPassword: 'Password123',
            newPassword: 'NewPassword456',
          );

      expect(fakeRepo.changePasswordCalled, isTrue);
      expect(fakeRepo.lastCurrentPassword, 'Password123');
      expect(fakeRepo.lastNewPassword, 'NewPassword456');
    });

    test('uploadAvatarControllerProvider uploads avatar and updates profileController', () async {
      final container = createContainer(
        overrides: [
          profileRepositoryProvider.overrideWithValue(fakeRepo),
        ],
      );

      // Load initial profile
      await container.read(profileControllerProvider.future);

      final url = await container
          .read(uploadAvatarControllerProvider.notifier)
          .uploadAvatarBytes(
            bytes: [1, 2, 3],
            filename: 'my_avatar.png',
          );

      expect(url, 'https://example.com/my_avatar.png');
      final updated = container.read(profileControllerProvider).value;
      expect(updated?.avatarUrl, 'https://example.com/my_avatar.png');
    });
  });
}
