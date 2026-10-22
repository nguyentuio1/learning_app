import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/error/api_exception.dart';
import '../../core/network/error_interceptor.dart';
import '../models/cancel_delivery_order_dto.dart';
import '../models/delivery_order.dart';
import '../models/delivery_order_detail_response_dto.dart';
import '../models/delivery_order_filter_params.dart';
import '../models/delivery_order_page_response.dart';
import '../models/update_delivery_order_dto.dart';

class DeliveryOrderApiService {
  DeliveryOrderApiService(this._dio);

  final Dio _dio;

  Future<DeliveryOrderPageResponse> getMyOrders([
    DeliveryOrderFilterParams params = const DeliveryOrderFilterParams(),
  ]) async {
    _validateListParams(params);
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.deliveryOrders,
        queryParameters: params.toQueryParameters(),
      );
      final data = response.data;
      final items = data is List
          ? data
          : data is Map && data['items'] is List
          ? data['items'] as List
          : null;
      if (items == null) {
        throw const FormatException('Expected a delivery order list response.');
      }
      final orders = items
          .map((item) {
            if (item is! Map) {
              throw const FormatException(
                'Expected a delivery order object response.',
              );
            }
            return DeliveryOrder.fromJson(Map<String, dynamic>.from(item));
          })
          .toList(growable: false);
      return DeliveryOrderPageResponse(
        items: List.unmodifiable(orders),
        page: data is Map ? data['page'] as int? ?? params.page : params.page,
        pageSize: data is Map
            ? data['pageSize'] as int? ?? params.pageSize
            : params.pageSize,
        totalCount: data is Map ? data['totalCount'] as int? : null,
        totalPages: data is Map ? data['totalPages'] as int? : null,
      );
    } on DioException catch (error) {
      throw _orderRequestException(error, params: params);
    }
  }

  Future<DeliveryOrderDetailResponseDto> getOrderDetail(String id) async {
    try {
      final response = await _dio.get<dynamic>(
        '${ApiEndpoints.deliveryOrders}/$id',
      );
      if (response.data is! Map) {
        throw const FormatException(
          'Expected a delivery order object response.',
        );
      }
      return DeliveryOrderDetailResponseDto.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw _orderRequestException(error);
    }
  }

  Future<void> updateOrder(String id, UpdateDeliveryOrderDto update) async {
    try {
      await _dio.put<void>(
        '${ApiEndpoints.deliveryOrders}/$id',
        data: update.toJson(),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<void> cancelOrder(
    String id,
    CancelDeliveryOrderDto cancellation,
  ) async {
    try {
      await _dio.post<void>(
        '${ApiEndpoints.deliveryOrders}/$id/cancel',
        data: cancellation.toJson(),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  ApiException _orderRequestException(
    DioException error, {
    DeliveryOrderFilterParams? params,
  }) {
    final apiError = error.apiException;
    if (apiError.statusCode == 404) {
      return const ApiException(
        message: 'Order not found or unavailable to your account.',
        statusCode: 404,
        code: 'MSG-P-CO-25',
      );
    }
    if (apiError.statusCode == 400 && params != null) {
      final invalidStatus =
          params.status != null &&
          params.status != 'ALL' &&
          !deliveryOrderStatuses.contains(params.status);
      if (invalidStatus) {
        return const ApiException(
          message: 'Select a valid order status.',
          statusCode: 400,
          code: 'MSG-P-CO-23',
        );
      }
      if (params.page < 1 || params.pageSize < 1 || params.pageSize > 100) {
        return const ApiException(
          message: 'Page must be at least 1 and page size must be within the allowed range.',
          statusCode: 400,
          code: 'MSG-P-CO-24',
        );
      }
    }
    return apiError;
  }

  void _validateListParams(DeliveryOrderFilterParams params) {
    if (params.status != null &&
        !deliveryOrderStatuses.contains(params.status)) {
      throw const ApiException(
        message: 'Select a valid order status.',
        statusCode: 400,
        code: 'MSG-P-CO-23',
      );
    }
    if (params.page < 1 || params.pageSize < 1 || params.pageSize > 100) {
      throw const ApiException(
        message: 'Page must be at least 1 and page size must be within the allowed range.',
        statusCode: 400,
        code: 'MSG-P-CO-24',
      );
    }
  }
}
