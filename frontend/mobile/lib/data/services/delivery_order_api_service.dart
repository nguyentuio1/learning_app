import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/error/api_exception.dart';
import '../../core/network/error_interceptor.dart';
import '../models/cancel_delivery_order_dto.dart';
import '../models/delivery_order.dart';
import '../models/delivery_order_detail_response_dto.dart';
import '../models/delivery_order_filter_params.dart';
import '../models/mark_failed_delivery_request_dto.dart';
import '../models/paged_result.dart';
import '../models/resolve_failed_delivery_request_dto.dart';
import '../models/update_delivery_order_dto.dart';

class DeliveryOrderApiService {
  DeliveryOrderApiService(this._dio);

  final Dio _dio;

  Future<PagedResult<DeliveryOrder>> getOrders(
    DeliveryOrderFilterParams params,
  ) async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.deliveryOrders,
        queryParameters: params.toQueryParameters(),
      );
      final data = response.data;
      if (data is Map) {
        return PagedResult<DeliveryOrder>.fromJson(
          Map<String, dynamic>.from(data),
          DeliveryOrder.fromJson,
        );
      }
      if (data is List) {
        final orders = data
            .map((item) {
              if (item is! Map) {
                throw const FormatException(
                  'Expected a delivery order object response.',
                );
              }
              return DeliveryOrder.fromJson(Map<String, dynamic>.from(item));
            })
            .toList(growable: false);
        return PagedResult(
          items: List.unmodifiable(orders),
          page: params.page,
          pageSize: params.pageSize,
          totalCount: orders.length,
          totalPages: orders.isEmpty ? 0 : 1,
        );
      }
      throw const FormatException('Expected a paged delivery order response.');
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<List<DeliveryOrder>> getMyOrders() async {
    return (await getOrders(const DeliveryOrderFilterParams())).items;
  }

  Future<DeliveryOrderDetailResponseDto> getOrderDetail(String id) async {
    try {
      final response = await _dio.get<dynamic>(
        '${ApiEndpoints.deliveryOrders}/$id',
      );
      if (response.data is! Map) {
        throw const FormatException(
          'Expected a delivery order detail object response.',
        );
      }
      return DeliveryOrderDetailResponseDto.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      final apiError = error.apiException;
      if (apiError.statusCode == 404) {
        throw const ApiException(
          message: 'Order not found or unavailable to your account.',
          statusCode: 404,
          code: 'MSG-P-CO-25',
        );
      }
      throw apiError;
    }
  }

  Future<CreateDeliveryOrderResponseDto> createOrder(
    CreateDeliveryOrderDto dto,
  ) async {
    try {
      final response = await _dio.post<dynamic>(
        ApiEndpoints.deliveryOrders,
        data: dto.toJson(),
      );
      if (response.data is! Map) {
        throw const FormatException(
          'Expected a delivery order creation response object.',
        );
      }
      final result = CreateDeliveryOrderResponseDto.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      if (!RegExp(r'^\d{6}$').hasMatch(result.pickupSecureCode)) {
        throw const FormatException('Expected a six-digit pickup secure code.');
      }
      return result;
    } on DioException catch (error) {
      final apiError = error.apiException;
      if (apiError.statusCode == 409) {
        throw const ApiException(
          message: 'This package is already linked to an order.',
          statusCode: 409,
          code: 'MSG-P-CO-11',
        );
      }
      throw apiError;
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
        data: {'cancellationReason': cancellation.cancellationReason.trim()},
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<void> markFailed(
    String id,
    MarkFailedDeliveryRequestDto request,
  ) async {
    try {
      await _dio.post<void>(
        '${ApiEndpoints.deliveryOrders}/$id/fail',
        data: request.toJson(),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<void> resolveFailedDelivery(
    String id,
    ResolveFailedDeliveryRequestDto request,
  ) async {
    try {
      await _dio.post<void>(
        '${ApiEndpoints.deliveryOrders}/$id/resolve-failure',
        data: request.toJson(),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }
}
