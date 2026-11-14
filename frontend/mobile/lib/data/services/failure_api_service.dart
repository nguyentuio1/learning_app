import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/error_interceptor.dart';
import '../models/failure_models.dart';
import '../models/paged_result.dart';

class FailureApiService {
  FailureApiService(this._dio);

  final Dio _dio;

  Future<PagedResult<FailedOrderSummary>> getFailedOrders(
    FailureOrderQuery query,
  ) async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.failedOrders,
        queryParameters: query.toQueryParameters(),
      );
      if (response.data is! Map) {
        throw const FormatException('Expected a failed orders page response.');
      }
      return PagedResult<FailedOrderSummary>.fromJson(
        Map<String, dynamic>.from(response.data as Map),
        FailedOrderSummary.fromJson,
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<FailureOrderDetail> getFailureDetail(String orderId) async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.failedOrderDetail(orderId),
      );
      if (response.data is! Map) {
        throw const FormatException('Expected a failed order detail response.');
      }
      return FailureOrderDetail.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<OrderDecision> reportFailure(
    String orderId,
    String failureType,
    String reason,
  ) async {
    try {
      final response = await _dio.post<dynamic>(
        ApiEndpoints.reportDeliveryFailure(orderId),
        data: ReportFailureRequest(
          failureType: failureType,
          reason: reason,
        ).toJson(),
      );
      if (response.data is! Map) {
        throw const FormatException('Expected an order decision response.');
      }
      return OrderDecision.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<OrderDecision> resolveFailure(
    String orderId,
    FailureResolutionRequest request,
  ) async {
    try {
      final response = await _dio.post<dynamic>(
        ApiEndpoints.resolveDeliveryFailure(orderId),
        data: request.toJson(),
      );
      if (response.data is! Map) {
        throw const FormatException('Expected an order decision response.');
      }
      return OrderDecision.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }
}
