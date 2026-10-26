import 'dart:io';
import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/auth_interceptor.dart';
import '../../core/network/error_interceptor.dart';
import '../../domain/entities/user.dart';
import '../../domain/repositories/profile_repository.dart';
import '../models/user_model.dart';

class ProfileRepositoryImpl implements ProfileRepository {
  ProfileRepositoryImpl(this._dio);
  final Dio _dio;

  @override
  Future<User> getProfile() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.me,
      );
      final data = res.data!;
      return UserModel.fromJson(data).toEntity();
    } on DioException catch (e) {
      throw e.apiException;
    }
  }

  @override
  Future<User> updateProfile({
    required String fullName,
    required String phoneNumber,
  }) async {
    try {
      final res = await _dio.put<Map<String, dynamic>>(
        ApiEndpoints.me,
        data: {
          'fullName': fullName.trim(),
          'phoneNumber': phoneNumber.trim(),
        },
      );
      final data = res.data!;
      return UserModel.fromJson(data).toEntity();
    } on DioException catch (e) {
      throw e.apiException;
    }
  }

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    try {
      await _dio.put<void>(
        ApiEndpoints.changePassword,
        data: {
          'currentPassword': currentPassword,
          'newPassword': newPassword,
        },
        options: Options(
          extra: {AuthInterceptor.skipRefresh: true},
        ),
      );
    } on DioException catch (e) {
      throw e.apiException;
    }
  }

  @override
  Future<String> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async {
    try {
      final formData = FormData.fromMap({
        'file': MultipartFile.fromBytes(bytes, filename: filename),
      });
      final res = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.uploadAvatar,
        data: formData,
        options: Options(
          contentType: 'multipart/form-data',
        ),
      );
      final data = res.data!;
      return data['avatarUrl'] as String;
    } on DioException catch (e) {
      throw e.apiException;
    }
  }

  @override
  Future<String> uploadAvatarFromFile(String filePath) async {
    try {
      final file = File(filePath);
      final filename = filePath.split(Platform.pathSeparator).last;
      final bytes = await file.readAsBytes();
      return uploadAvatar(bytes: bytes, filename: filename);
    } on DioException catch (e) {
      throw e.apiException;
    }
  }
}
