import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/constants/api_endpoints.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/repositories/profile_repository_impl.dart';

import '../../support/fake_http_adapter.dart';

void main() {
  group('ProfileRepositoryImpl', () {
    test('getProfile returns User entity on 200', () async {
      final dio = Dio()..httpClientAdapter = FakeHttpAdapter((options) {
        expect(options.path, ApiEndpoints.userProfile);
        expect(options.method, 'GET');
        return jsonBody({
          'id': 'user-123',
          'email': 'customer@example.com',
          'fullName': 'Nguyen Van An',
          'phoneNumber': '0901234567',
          'avatarUrl': 'https://example.com/avatar.png',
          'status': 'ACTIVE',
          'role': {'code': 'CUSTOMER'},
          'createdAt': '2026-01-01T00:00:00Z',
        });
      });

      final repo = ProfileRepositoryImpl(dio);
      final user = await repo.getProfile();

      expect(user.id, 'user-123');
      expect(user.email, 'customer@example.com');
      expect(user.fullName, 'Nguyen Van An');
      expect(user.phoneNumber, '0901234567');
      expect(user.avatarUrl, 'https://example.com/avatar.png');
      expect(user.status, 'ACTIVE');
      expect(user.roleCode, 'CUSTOMER');
    });

    test('getProfile throws ApiException on error', () async {
      final dio = Dio()..httpClientAdapter = FakeHttpAdapter((options) {
        return jsonBody({'message': 'Unauthorized'}, 401);
      });

      final repo = ProfileRepositoryImpl(dio);
      expect(() => repo.getProfile(), throwsA(isA<ApiException>()));
    });

    test('updateProfile sends PUT /users/me and returns updated user', () async {
      final dio = Dio()..httpClientAdapter = FakeHttpAdapter((options) {
        expect(options.path, ApiEndpoints.userProfile);
        expect(options.method, 'PUT');
        expect(options.data, {
          'fullName': 'Nguyen Van B',
          'phoneNumber': '0909876543',
        });
        return jsonBody({
          'id': 'user-123',
          'email': 'customer@example.com',
          'fullName': 'Nguyen Van B',
          'phoneNumber': '0909876543',
          'avatarUrl': null,
          'status': 'ACTIVE',
          'role': 'CUSTOMER',
          'createdAt': '2026-01-01T00:00:00Z',
        });
      });

      final repo = ProfileRepositoryImpl(dio);
      final updated = await repo.updateProfile(
        fullName: 'Nguyen Van B',
        phoneNumber: '0909876543',
      );

      expect(updated.fullName, 'Nguyen Van B');
      expect(updated.phoneNumber, '0909876543');
    });

    test('changePassword sends PUT /users/me/password', () async {
      var called = false;
      final dio = Dio()..httpClientAdapter = FakeHttpAdapter((options) {
        expect(options.path, ApiEndpoints.changePassword);
        expect(options.method, 'PUT');
        expect(options.data, {
          'currentPassword': 'OldPassword123',
          'newPassword': 'NewPassword456',
        });
        called = true;
        return ResponseBody.fromString('', 204);
      });

      final repo = ProfileRepositoryImpl(dio);
      await repo.changePassword(
        currentPassword: 'OldPassword123',
        newPassword: 'NewPassword456',
      );

      expect(called, isTrue);
    });

    test('uploadAvatar sends multipart POST /users/me/avatar and returns avatarUrl', () async {
      final dio = Dio()..httpClientAdapter = FakeHttpAdapter((options) {
        expect(options.path, ApiEndpoints.uploadAvatar);
        expect(options.method, 'POST');
        expect(options.data, isA<FormData>());
        return jsonBody({
          'avatarUrl': 'http://localhost:9000/avatars/user-123.png',
        });
      });

      final repo = ProfileRepositoryImpl(dio);
      final url = await repo.uploadAvatar(
        bytes: [1, 2, 3],
        filename: 'avatar.png',
      );

      expect(url, 'http://localhost:9000/avatars/user-123.png');
    });
  });
}
