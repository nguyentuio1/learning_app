import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/create_delivery_order_dto.dart';
import '../../../data/models/customer_address.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/models/package_model.dart';
import '../../../data/providers/delivery_order_providers.dart';
import 'my_orders_provider.dart';

final _guidPattern = RegExp(
  r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
);

class CreateOrderState {
  const CreateOrderState({
    this.package,
    this.originStationId = '',
    this.destinationStationId = '',
    this.address,
    this.recipientName = '',
    this.recipientPhone = '',
    this.isSubmitting = false,
    this.error,
    this.result,
  });

  final Package? package;
  final String originStationId;
  final String destinationStationId;
  final CustomerAddress? address;
  final String recipientName;
  final String recipientPhone;
  final bool isSubmitting;
  final ApiException? error;
  final DeliveryOrderDetailResponseDto? result;

  CreateOrderState copyWith({
    Package? package,
    String? originStationId,
    String? destinationStationId,
    CustomerAddress? address,
    bool clearAddress = false,
    String? recipientName,
    String? recipientPhone,
    bool? isSubmitting,
    ApiException? error,
    bool clearError = false,
    DeliveryOrderDetailResponseDto? result,
    bool clearResult = false,
  }) => CreateOrderState(
    package: package ?? this.package,
    originStationId: originStationId ?? this.originStationId,
    destinationStationId: destinationStationId ?? this.destinationStationId,
    address: clearAddress ? null : address ?? this.address,
    recipientName: recipientName ?? this.recipientName,
    recipientPhone: recipientPhone ?? this.recipientPhone,
    isSubmitting: isSubmitting ?? this.isSubmitting,
    error: clearError ? null : error ?? this.error,
    result: clearResult ? null : result ?? this.result,
  );
}

class CreateOrderNotifier extends Notifier<CreateOrderState> {
  @override
  CreateOrderState build() => const CreateOrderState();

  void selectPackage(Package? package) {
    state = state.copyWith(package: package, clearError: true);
  }

  void setOriginStationId(String value) {
    state = state.copyWith(originStationId: value.trim(), clearError: true);
  }

  void setDestinationStationId(String value) {
    state = state.copyWith(
      destinationStationId: value.trim(),
      clearError: true,
    );
  }

  void selectAddress(CustomerAddress? address) {
    state = state.copyWith(
      address: address,
      clearAddress: address == null,
      clearError: true,
    );
  }

  void setRecipientName(String value) {
    state = state.copyWith(recipientName: value, clearError: true);
  }

  void setRecipientPhone(String value) {
    state = state.copyWith(recipientPhone: value, clearError: true);
  }

  ApiException? validate() {
    final order = state;
    if (order.package == null) {
      return const ApiException(message: 'Select a package.');
    }
    if (!_withinCapacity(order.package!)) {
      return const ApiException(
        message: "This package exceeds the selected drone's capacity.",
        statusCode: 400,
        code: 'MSG-P-CO-10',
      );
    }
    if (!_guidPattern.hasMatch(order.originStationId) ||
        !_guidPattern.hasMatch(order.destinationStationId)) {
      return const ApiException(
        message: 'Select valid origin and destination stations.',
      );
    }
    if (order.originStationId.toLowerCase() ==
        order.destinationStationId.toLowerCase()) {
      return const ApiException(
        message: 'Select different origin and destination stations.',
        code: 'MSG-P-CO-14',
      );
    }
    if (order.recipientName.trim().isEmpty) {
      return const ApiException(message: 'Recipient name is required.');
    }
    if (order.recipientName.trim().length > 150) {
      return const ApiException(
        message: 'Recipient name must be at most 150 characters.',
      );
    }
    final phone = order.recipientPhone.trim();
    if (phone.isEmpty) {
      return const ApiException(
        message: 'Phone number is required.',
        code: 'MSG-API-009',
      );
    }
    if (!RegExp(r'^\d{9,11}$').hasMatch(phone)) {
      return const ApiException(
        message: 'Phone number must be 9 to 11 digits.',
        code: 'MSG-API-010',
      );
    }
    return null;
  }

  Future<DeliveryOrderDetailResponseDto?> submit() async {
    final validationError = validate();
    if (validationError != null) {
      state = state.copyWith(error: validationError, clearResult: true);
      return null;
    }

    state = state.copyWith(
      isSubmitting: true,
      clearError: true,
      clearResult: true,
    );
    final order = state;
    final dto = CreateDeliveryOrderDto(
      packageId: order.package!.id,
      originStationId: order.originStationId,
      destinationStationId: order.destinationStationId,
      deliveryAddressId: order.address?.id,
      recipientName: order.recipientName.trim(),
      recipientPhone: order.recipientPhone.trim(),
    );

    try {
      final result = await ref
          .read(deliveryOrderApiServiceProvider)
          .createDeliveryOrder(dto);
      if (!ref.mounted) return null;
      ref.invalidate(myOrdersProvider);
      state = state.copyWith(isSubmitting: false, result: result);
      return result;
    } on ApiException catch (error) {
      if (!ref.mounted) return null;
      final mappedError = _mapCreateOrderError(error);
      state = state.copyWith(isSubmitting: false, error: mappedError);
      return null;
    } catch (_) {
      if (!ref.mounted) return null;
      state = state.copyWith(isSubmitting: false);
      rethrow;
    }
  }

  void reset() {
    state = const CreateOrderState();
  }

  bool _withinCapacity(Package package) =>
      package.weightGram <= 3000 &&
      package.lengthCm <= 30 &&
      package.widthCm <= 20 &&
      package.heightCm <= 15;

  ApiException _mapCreateOrderError(ApiException error) {
    final code = error.code;
    if (code == 'MSG-P-CO-10' ||
        code == 'MSG-P-CO-11' ||
        code == 'MSG-P-CO-14' ||
        code == 'MSG-P-CO-15' ||
        code == 'MSG-API-009' ||
        code == 'MSG-API-010') {
      return ApiException(
        message: _createOrderMessage(code!),
        code: code,
        statusCode: error.statusCode,
      );
    }
    final message = error.message.toLowerCase();
    if (error.statusCode == 409) {
      return const ApiException(
        message: 'This package is already linked to an order.',
        statusCode: 409,
        code: 'MSG-P-CO-11',
      );
    }
    if (error.statusCode == 400) {
      if (message.contains('station') &&
          (message.contains('operational') ||
              message.contains('unavailable') ||
              message.contains('maintenance'))) {
        return const ApiException(
          message:
              'The selected station is unavailable. Choose another station.',
          statusCode: 400,
          code: 'MSG-P-CO-15',
        );
      }
      if (message.contains('same station') ||
          message.contains('different station')) {
        return const ApiException(
          message: 'Select different origin and destination stations.',
          statusCode: 400,
          code: 'MSG-P-CO-14',
        );
      }
      if (message.contains('capacity') ||
          message.contains('dimension') ||
          message.contains('weight')) {
        return const ApiException(
          message: "This package exceeds the selected drone's capacity.",
          statusCode: 400,
          code: 'MSG-P-CO-10',
        );
      }
      if (message.contains('phone')) {
        final code = message.contains('required')
            ? 'MSG-API-009'
            : 'MSG-API-010';
        return ApiException(
          message: _createOrderMessage(code),
          statusCode: 400,
          code: code,
        );
      }
    }
    return error;
  }

  String _createOrderMessage(String code) => switch (code) {
    'MSG-P-CO-10' => "This package exceeds the selected drone's capacity.",
    'MSG-P-CO-11' => 'This package is already linked to an order.',
    'MSG-P-CO-14' => 'Select different origin and destination stations.',
    'MSG-P-CO-15' =>
      'The selected station is unavailable. Choose another station.',
    'MSG-API-009' => 'Phone number is required.',
    'MSG-API-010' => 'Phone number must be 9 to 11 digits.',
    _ => 'Unable to create delivery order.',
  };
}

final createOrderNotifierProvider =
    NotifierProvider.autoDispose<CreateOrderNotifier, CreateOrderState>(
      CreateOrderNotifier.new,
      name: 'createOrder',
    );
