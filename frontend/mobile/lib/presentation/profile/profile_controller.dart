import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/providers/repository_providers.dart';
import '../../domain/entities/user.dart';

/// Provider quản lý tải và hiển thị thông tin hồ sơ người dùng.
class ProfileController extends AsyncNotifier<User> {
  @override
  FutureOr<User> build() {
    return ref.read(profileRepositoryProvider).getProfile();
  }

  /// Tải lại hồ sơ từ server.
  Future<void> reload() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(profileRepositoryProvider).getProfile(),
    );
  }

  /// Cập nhật trực tiếp thực thể user trong state sau khi lưu thành công.
  void setUser(User user) {
    state = AsyncData(user);
  }

  /// Cập nhật URL avatar trong state.
  void setAvatarUrl(String url) {
    final current = state.value;
    if (current != null) {
      state = AsyncData(current.copyWith(avatarUrl: url));
    }
  }
}

final profileControllerProvider =
    AsyncNotifierProvider<ProfileController, User>(
  ProfileController.new,
  name: 'profileController',
);

/// Controller xử lý hành động cập nhật thông tin (Họ tên, SĐT).
class UpdateProfileController extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<void> updateProfile({
    required String fullName,
    required String phoneNumber,
  }) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final updatedUser =
          await ref.read(profileRepositoryProvider).updateProfile(
                fullName: fullName,
                phoneNumber: phoneNumber,
              );
      ref.read(profileControllerProvider.notifier).setUser(updatedUser);
    });
  }
}

final updateProfileControllerProvider =
    AsyncNotifierProvider.autoDispose<UpdateProfileController, void>(
  UpdateProfileController.new,
  name: 'updateProfileController',
);

/// Controller xử lý hành động đổi mật khẩu.
class ChangePasswordController extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      await ref.read(profileRepositoryProvider).changePassword(
            currentPassword: currentPassword,
            newPassword: newPassword,
          );
    });
  }
}

final changePasswordControllerProvider =
    AsyncNotifierProvider.autoDispose<ChangePasswordController, void>(
  ChangePasswordController.new,
  name: 'changePasswordController',
);

/// Controller xử lý tải lên avatar.
class UploadAvatarController extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<String?> uploadAvatarBytes({
    required List<int> bytes,
    required String filename,
  }) async {
    state = const AsyncLoading();
    String? avatarUrl;
    state = await AsyncValue.guard(() async {
      avatarUrl = await ref.read(profileRepositoryProvider).uploadAvatar(
            bytes: bytes,
            filename: filename,
          );
      if (avatarUrl != null) {
        ref.read(profileControllerProvider.notifier).setAvatarUrl(avatarUrl!);
      }
    });
    return avatarUrl;
  }
}

final uploadAvatarControllerProvider =
    AsyncNotifierProvider.autoDispose<UploadAvatarController, void>(
  UploadAvatarController.new,
  name: 'uploadAvatarController',
);
