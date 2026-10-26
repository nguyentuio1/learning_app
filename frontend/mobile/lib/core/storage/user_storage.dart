import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../data/models/user_model.dart';
import '../../domain/entities/user.dart';

abstract interface class UserStorage {
  Future<User?> read();
  Future<void> write(User user);
  Future<void> clear();
}

/// Keychain (iOS) / EncryptedSharedPreferences-backed Keystore (Android) cho thông tin người dùng.
class SecureUserStorage implements UserStorage {
  SecureUserStorage([FlutterSecureStorage? storage])
      : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;
  static const _userKey = 'current_user';

  @override
  Future<User?> read() async {
    final raw = await _storage.read(key: _userKey);
    if (raw == null) return null;
    try {
      final map = jsonDecode(raw) as Map<String, dynamic>;
      return UserModel.fromJson(map).toEntity();
    } catch (_) {
      return null;
    }
  }

  @override
  Future<void> write(User user) async {
    final model = UserModel(
      id: user.id,
      email: user.email,
      fullName: user.fullName,
      phoneNumber: user.phoneNumber,
      avatarUrl: user.avatarUrl,
      status: user.status,
      roleCode: user.roleCode,
      createdAt: user.createdAt,
    );
    await _storage.write(key: _userKey, value: jsonEncode(model.toJson()));
  }

  @override
  Future<void> clear() async {
    await _storage.delete(key: _userKey);
  }
}
