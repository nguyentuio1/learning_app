import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/error_interceptor.dart';
import '../models/landing_station.dart';

class LandingStationApiService {
  LandingStationApiService(this._dio);

  final Dio _dio;

  Future<List<LandingStation>> getOperationalStations() async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.landingStations,
        queryParameters: const {'status': 'OPERATIONAL'},
      );
      if (response.data is! List) {
        throw const FormatException(
          'Expected a landing station list response.',
        );
      }
      return (response.data as List)
          .map((item) {
            if (item is! Map) {
              throw const FormatException(
                'Expected a landing station object response.',
              );
            }
            return LandingStation.fromJson(Map<String, dynamic>.from(item));
          })
          .where((station) => station.status == 'OPERATIONAL')
          .toList(growable: false);
    } on DioException catch (error) {
      throw error.apiException;
    }
  }
}
