import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/error/api_exception.dart';
import '../../core/network/error_interceptor.dart';
import '../models/expired_parcel.dart';

class ExpiredParcelApiService {
  ExpiredParcelApiService(this._dio);

  final Dio _dio;

  Future<List<ExpiredParcel>> getExpiredParcels(String stationId) async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.expiredParcels(stationId),
      );
      if (response.data is! List) {
        throw const FormatException('Expected a station slot list response.');
      }

      final parcels = <ExpiredParcel>[];
      for (final item in response.data as List) {
        if (item is! Map) {
          throw const FormatException('Expected a station slot object.');
        }
        final json = Map<String, dynamic>.from(item);
        final slotStatus = json['slotStatus'] ?? json['status'];
        if (json['orderStatus'] != 'FAILED' ||
            slotStatus != 'OCCUPIED' ||
            json['cancelReason'] != ExpiredParcel.pickupTimeoutReason) {
          continue;
        }
        parcels.add(ExpiredParcel.fromJson(json));
      }
      return parcels;
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<ClearExpiredParcelResponse> clearExpiredParcel({
    required String stationId,
    required ClearExpiredParcelRequest request,
  }) async {
    try {
      final response = await _dio.post<dynamic>(
        ApiEndpoints.clearExpiredParcel(stationId),
        data: request.toJson(),
      );
      if (response.data is! Map) {
        throw const FormatException(
          'Expected a clear-expired-parcel response object.',
        );
      }
      return ClearExpiredParcelResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      final problem = error.apiException;
      if (problem.statusCode == 403 || problem.statusCode == 409) {
        final context =
            problem.message == 'Forbidden' ||
                problem.message == 'Conflict' ||
                problem.message == 'Something went wrong. Please try again.'
            ? ''
            : ' ${problem.message}';
        final message = problem.statusCode == 403
            ? 'You are not authorized to clear parcels at this station.$context'
            : 'The parcel is no longer eligible to be cleared because its order or slot state changed.$context';
        throw ApiException(
          message: message,
          statusCode: problem.statusCode,
          code: problem.code,
        );
      }
      throw problem;
    }
  }
}
