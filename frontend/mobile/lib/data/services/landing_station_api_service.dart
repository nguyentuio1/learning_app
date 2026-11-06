import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/error_interceptor.dart';
import '../models/landing_station.dart';
import '../models/station_availability.dart';
import '../models/station_detail.dart';

class LandingStationApiService {
  LandingStationApiService(this._dio);

  final Dio _dio;

  Future<List<LandingStation>> getOperationalStations() async =>
      getStations(status: 'OPERATIONAL');

  Future<List<LandingStation>> getStations({
    String? status,
    String? search,
  }) async {
    try {
      final queryParams = <String, dynamic>{};
      if (status != null && status.isNotEmpty) {
        queryParams['status'] = status;
      }
      if (search != null && search.isNotEmpty) {
        queryParams['search'] = search;
      }

      final response = await _dio.get<dynamic>(
        ApiEndpoints.landingStations,
        queryParameters: queryParams.isEmpty ? null : queryParams,
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
            return LandingStation.fromJson(
              Map<String, dynamic>.from(item as Map<dynamic, dynamic>),
            );
          })
          .toList(growable: false);
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<StationAvailability> getStationAvailability(String stationId) async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.landingStationAvailability(stationId),
      );
      if (response.data is! Map) {
        throw const FormatException(
          'Expected a station availability object response.',
        );
      }
      return StationAvailability.fromJson(
        Map<String, dynamic>.from(response.data as Map<dynamic, dynamic>),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<StationDetail> getStationDetail(String stationId) async {
    try {
      final response = await _dio.get<dynamic>(
        ApiEndpoints.landingStation(stationId),
      );
      if (response.data is! Map) {
        throw const FormatException(
          'Expected a station detail object response.',
        );
      }
      return StationDetail.fromJson(
        Map<String, dynamic>.from(response.data as Map<dynamic, dynamic>),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }
}
