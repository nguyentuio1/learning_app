import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/error_interceptor.dart';
import '../models/station_operation_models.dart';

class StationOperationsApiService {
  StationOperationsApiService(this._dio);

  final Dio _dio;

  Future<StationOrderInfo> lookupOrder({
    required String stationId,
    required String trackingNumber,
  }) async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.stationOrderLookup(stationId, trackingNumber),
      );
      if (response.data is! Map) {
        throw const FormatException('Expected station order info object.');
      }
      return StationOrderInfo.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<ConfirmPickupResponse> confirmPickup({
    required String stationId,
    required ConfirmPickupRequest request,
  }) async {
    try {
      final response = await _dio.post<dynamic>(
        ApiEndpoints.stationPickup(stationId),
        data: request.toJson(),
      );
      if (response.data is! Map) {
        throw const FormatException('Expected pickup confirmation object.');
      }
      return ConfirmPickupResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<PackageIntakeResponse> confirmIntake({
    required String stationId,
    required PackageIntakeRequest request,
  }) async {
    try {
      final response = await _dio.post<dynamic>(
        ApiEndpoints.stationIntake(stationId),
        data: request.toJson(),
      );
      if (response.data is! Map) {
        throw const FormatException('Expected intake confirmation object.');
      }
      return PackageIntakeResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<RejectIntakeResponse> rejectIntake({
    required String stationId,
    required RejectIntakeRequest request,
  }) async {
    try {
      final response = await _dio.post<dynamic>(
        ApiEndpoints.stationIntakeReject(stationId),
        data: request.toJson(),
      );
      if (response.data is! Map) {
        throw const FormatException('Expected intake rejection object.');
      }
      return RejectIntakeResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }
}
