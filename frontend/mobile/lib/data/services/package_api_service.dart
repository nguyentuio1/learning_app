import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/error/api_exception.dart';
import '../../core/network/error_interceptor.dart';
import '../models/package_model.dart';

class PackageApiService {
  PackageApiService(this._dio);

  final Dio _dio;

  Future<List<PackageModel>> getPackages({bool? available}) async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.packages,
        queryParameters: available == null ? null : {'available': available},
      );
      final data = response.data;
      if (data is! List) {
        throw const FormatException('Expected a package list response.');
      }
      return data.map(_packageFromData).toList(growable: false);
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<PackageModel> getPackage(String id) async {
    try {
      final response = await _dio.get<dynamic>('${ApiEndpoints.packages}/$id');
      return _packageFromData(response.data);
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<void> createPackage(PackageRequest package) async {
    try {
      await _dio.post<void>(ApiEndpoints.packages, data: package.toJson());
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<PackageModel> createPackageRecord(PackageRequest package) async {
    try {
      final response = await _dio.post<dynamic>(
        ApiEndpoints.packages,
        data: package.toJson(),
      );
      return _packageFromData(response.data);
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<String> uploadImage(
    String id, {
    required List<int> bytes,
    required String filename,
  }) async {
    if (bytes.isEmpty || bytes.length > 5 * 1024 * 1024) {
      throw const ApiException(
        message: 'Choose an image between 1 byte and 5 MB.',
      );
    }
    try {
      final response = await _dio.post<dynamic>(
        '${ApiEndpoints.packages}/$id/image',
        data: FormData.fromMap({
          'file': MultipartFile.fromBytes(bytes, filename: filename),
        }),
        options: Options(contentType: 'multipart/form-data'),
      );
      final value = response.data;
      if (value is! Map ||
          value['imageUrl'] is! String ||
          (value['imageUrl'] as String).isEmpty) {
        throw const FormatException('The server did not return a photo URL.');
      }
      return value['imageUrl'] as String;
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<void> updatePackage(String id, PackageRequest package) async {
    try {
      await _dio.put<void>(
        '${ApiEndpoints.packages}/$id',
        data: package.toJson(),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<void> deletePackage(String id) async {
    try {
      await _dio.delete<void>('${ApiEndpoints.packages}/$id');
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  PackageModel _packageFromData(dynamic data) {
    if (data is! Map) {
      throw const FormatException('Expected a package object response.');
    }
    return PackageModel.fromJson(Map<String, dynamic>.from(data));
  }
}
