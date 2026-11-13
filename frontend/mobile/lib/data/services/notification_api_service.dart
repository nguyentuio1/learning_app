import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/error_interceptor.dart';
import '../models/notification_model.dart';
import '../models/paged_result.dart';

class NotificationApiService {
  NotificationApiService(this._dio);

  final Dio _dio;

  Future<PagedResult<NotificationModel>> getNotifications({
    bool? isRead,
    int page = 1,
    int pageSize = 20,
  }) async {
    try {
      final query = <String, dynamic>{
        'page': page,
        'pageSize': pageSize,
      };
      if (isRead != null) {
        query['isRead'] = isRead;
      }

      final response = await _dio.get<dynamic>(
        ApiEndpoints.notifications,
        queryParameters: query,
      );
      final data = response.data;
      if (data is Map) {
        return PagedResult<NotificationModel>.fromJson(
          Map<String, dynamic>.from(data),
          NotificationModel.fromJson,
        );
      }
      throw const FormatException('Expected a paged notification response.');
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<int> getUnreadCount() async {
    try {
      final response = await _dio.get<dynamic>(ApiEndpoints.notificationsUnreadCount);
      final data = response.data;
      if (data is Map && data['unreadCount'] is num) {
        return (data['unreadCount'] as num).toInt();
      }
      return 0;
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<NotificationModel> markAsRead(String id) async {
    try {
      final response = await _dio.put<dynamic>(ApiEndpoints.notificationRead(id));
      final data = response.data;
      if (data is Map) {
        return NotificationModel.fromJson(Map<String, dynamic>.from(data));
      }
      throw const FormatException('Expected notification response.');
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<int> markAllAsRead() async {
    try {
      final response = await _dio.put<dynamic>(ApiEndpoints.notificationsReadAll);
      final data = response.data;
      if (data is Map && data['updatedCount'] is num) {
        return (data['updatedCount'] as num).toInt();
      }
      return 0;
    } on DioException catch (error) {
      throw error.apiException;
    }
  }
}
