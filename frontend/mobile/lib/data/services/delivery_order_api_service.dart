import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/error/api_exception.dart';
import '../../core/network/error_interceptor.dart';
import '../models/cancel_delivery_order_dto.dart';
import '../models/delivery_order.dart';
import '../models/update_delivery_order_dto.dart';

class DeliveryOrderApiService {
  DeliveryOrderApiService(this._dio);

  final Dio _dio;

  Future<List<DeliveryOrder>> getMyOrders() async {
    try {
      final response = await _dio.get<dynamic>(ApiEndpoints.deliveryOrders);
      final data = response.data;
      final items = data is List
          ? data
          : data is Map && data['items'] is List
          ? data['items'] as List
          : null;
      if (items == null) {
        throw const FormatException('Expected a delivery order list response.');
      }
      return items
          .map((item) {
            if (item is! Map) {
              throw const FormatException(
                'Expected a delivery order object response.',
              );
            }
            return DeliveryOrder.fromJson(Map<String, dynamic>.from(item));
          })
          .toList(growable: false);
    } on DioException catch (error) {
      throw error.apiException;
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
        data: cancellation.toJson(),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }
}
