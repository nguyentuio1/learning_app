import '../entities/user.dart';

abstract class ProfileRepository {
  Future<User> getProfile();

  Future<User> updateProfile({
    required String fullName,
    required String phoneNumber,
  });

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  });

  Future<String> uploadAvatar({
    required List<int> bytes,
    required String filename,
  });

  Future<String> uploadAvatarFromFile(String filePath);
}
