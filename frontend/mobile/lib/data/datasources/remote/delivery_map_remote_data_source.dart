import 'package:dio/dio.dart';

import '../../../core/network/error_interceptor.dart';
import '../../../domain/entities/delivery_map_snapshot.dart';
import '../../models/delivery_map_snapshot_model.dart';

class DeliveryMapRemoteDataSource {
  const DeliveryMapRemoteDataSource(this._dio);

  final Dio _dio;

  Future<DeliveryMapSnapshot> getMapSnapshot(String orderId) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        '/delivery-orders/$orderId/tracking',
      );
      final data = response.data;
      if (data == null) {
        throw const FormatException('Tracking snapshot response is empty.');
      }
      return DeliveryMapSnapshotModel.fromJson(data);
    } on DioException catch (error) {
      throw error.apiException;
    }
  }
}
