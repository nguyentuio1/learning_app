import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/error_interceptor.dart';
import '../models/package_model.dart';

class PackageApiService {
  PackageApiService(this._dio);

  final Dio _dio;

  Future<List<Package>> getPackages() async {
    try {
      final response = await _dio.get<dynamic>(ApiEndpoints.packages);
      final data = response.data;
      if (data is! List) {
        throw const FormatException('Expected a package list response.');
      }
      return data.map(_packageFromData).toList(growable: false);
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<Package> getPackage(String id) async {
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

  Package _packageFromData(dynamic data) {
    if (data is! Map) {
      throw const FormatException('Expected a package object response.');
    }
    return Package.fromJson(Map<String, dynamic>.from(data));
  }
}
