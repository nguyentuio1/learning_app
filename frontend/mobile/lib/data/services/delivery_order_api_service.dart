import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/error_interceptor.dart';
import '../models/cancel_delivery_order_dto.dart';
import '../models/delivery_order.dart';
import '../models/order_station.dart';
import '../models/update_delivery_order_dto.dart';

class DeliveryOrderApiService {
  DeliveryOrderApiService(this._dio);

  final Dio _dio;

  Future<List<OrderStation>> getOperationalStations() async {
    try {
      final response = await _dio.get<dynamic>(
        '/landing-stations',
        queryParameters: const {'status': 'OPERATIONAL'},
      );
      final data = response.data;
      if (data is! List) {
        throw const FormatException('Expected a station list response.');
      }
      return data
          .map((item) {
            if (item is! Map) {
              throw const FormatException(
                'Expected a station object response.',
              );
            }
            return OrderStation.fromJson(Map<String, dynamic>.from(item));
          })
          .where((station) => station.status == 'OPERATIONAL')
          .toList(growable: false);
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

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
