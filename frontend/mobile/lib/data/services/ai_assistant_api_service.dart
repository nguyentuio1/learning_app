import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/error/api_exception.dart';
import '../../core/network/error_interceptor.dart';
import '../../core/network/logging_interceptor.dart';
import '../models/ai_assistant_models.dart';

class AiAssistantApiService {
  AiAssistantApiService(this._dio);

  final Dio _dio;

  static const int maxMessageRunes = 2000;
  static const int defaultPageSize = 20;
  static final Options _noBodyLogOptions = Options(
    extra: const {AppLogInterceptor.suppressBodyLogKey: true},
  );

  Future<AiAssistantSessionsPage> getSessions({
    int page = 1,
    int pageSize = defaultPageSize,
  }) async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.aiAssistantSessions,
        queryParameters: {'page': page, 'pageSize': pageSize},
        options: _noBodyLogOptions,
      );
      if (response.data is! Map) {
        throw const FormatException('Expected sessions page object.');
      }
      return AiAssistantSessionsPage.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<AiAssistantMessagesPage> getSessionMessages(
    String sessionId, {
    int page = 1,
    int pageSize = defaultPageSize,
  }) async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.aiAssistantSessionMessages(sessionId),
        queryParameters: {'page': page, 'pageSize': pageSize},
        options: _noBodyLogOptions,
      );
      if (response.data is! Map) {
        throw const FormatException('Expected messages page object.');
      }
      final result = AiAssistantMessagesPage.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      if (result.sessionId.toLowerCase() != sessionId.toLowerCase()) {
        throw const FormatException('Unexpected conversation history.');
      }
      return result;
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<AiAssistantSendResponse> sendMessage(
    AiAssistantSendRequest request,
  ) async {
    final trimmed = request.message.trim();
    final runeLength = trimmed.runes.length;
    if (runeLength == 0 || runeLength > maxMessageRunes) {
      throw const ApiException(
        message: 'Message must be between 1 and 2,000 characters.',
        statusCode: 400,
      );
    }

    final normalizedRequest = AiAssistantSendRequest(
      sessionId: request.sessionId,
      orderId: request.orderId,
      message: trimmed,
    );

    try {
      final response = await _dio.post<dynamic>(
        ApiEndpoints.aiAssistantMessages,
        data: normalizedRequest.toJson(),
        options: _noBodyLogOptions,
      );
      if (response.data is! Map) {
        throw const FormatException('Expected assistant response object.');
      }
      final result = AiAssistantSendResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
      if (result.outcome == AiAssistantOutcome.answered &&
          normalizedRequest.sessionId != null &&
          result.sessionId!.toLowerCase() !=
              normalizedRequest.sessionId!.toLowerCase()) {
        throw const FormatException('Unexpected assistant conversation.');
      }
      return result;
    } on DioException catch (error) {
      throw error.apiException;
    }
  }
}
