import 'package:dio/dio.dart';

import '../../../core/constants/api_endpoints.dart';
import '../../../core/network/error_interceptor.dart';
import '../../models/tracking_snapshot_model.dart';

class TrackingRemoteDataSource {
  const TrackingRemoteDataSource(this._dio);

  final Dio _dio;

  Future<TrackingSnapshotModel> getSnapshot(String orderId) async {
    try {
      final response = await _dio.get<dynamic>(
        '${ApiEndpoints.deliveryOrders}/${Uri.encodeComponent(orderId)}/tracking',
      );
      if (response.data is! Map) {
        throw const FormatException('Expected a tracking snapshot object.');
      }
      return TrackingSnapshotModel.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }
}
