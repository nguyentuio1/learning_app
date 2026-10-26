import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/error_interceptor.dart';
import '../models/customer_address.dart';

class AddressApiService {
  AddressApiService(this._dio);

  final Dio _dio;

  Future<List<CustomerAddress>> getAddresses() async {
    try {
      final response = await _dio.get<dynamic>(ApiEndpoints.customerAddresses);
      final data = response.data;
      if (data is! List) {
        throw const FormatException('Expected an address list response.');
      }
      return data.map(_addressFromData).toList(growable: false);
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<CustomerAddress> getAddress(String id) async {
    try {
      final response = await _dio.get<dynamic>(
        '${ApiEndpoints.customerAddresses}/$id',
      );
      return _addressFromData(response.data);
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<void> createAddress(CustomerAddressRequest address) async {
    try {
      await _dio.post<void>(
        ApiEndpoints.customerAddresses,
        data: address.toJson(),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<void> updateAddress(String id, CustomerAddressRequest address) async {
    try {
      await _dio.put<void>(
        '${ApiEndpoints.customerAddresses}/$id',
        data: address.toJson(),
      );
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<void> deleteAddress(String id) async {
    try {
      await _dio.delete<void>('${ApiEndpoints.customerAddresses}/$id');
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  Future<void> setDefaultAddress(String id) async {
    try {
      await _dio.put<void>('${ApiEndpoints.customerAddresses}/$id/default');
    } on DioException catch (error) {
      throw error.apiException;
    }
  }

  CustomerAddress _addressFromData(dynamic data) {
    if (data is! Map) {
      throw const FormatException('Expected an address object response.');
    }
    return CustomerAddress.fromJson(Map<String, dynamic>.from(data));
  }
}
