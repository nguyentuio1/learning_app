import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/customer_address.dart';
import '../../../data/models/delivery_order.dart';
import '../../../data/models/package_model.dart';
import '../../../data/providers/delivery_order_providers.dart';

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
  });

  final Package? package;
  final String originStationId;
  final String destinationStationId;
  final CustomerAddress? address;
  final String recipientName;
  final String recipientPhone;
  final bool isSubmitting;
  final ApiException? error;

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
  }) => CreateOrderState(
    package: package ?? this.package,
    originStationId: originStationId ?? this.originStationId,
    destinationStationId: destinationStationId ?? this.destinationStationId,
    address: clearAddress ? null : address ?? this.address,
    recipientName: recipientName ?? this.recipientName,
    recipientPhone: recipientPhone ?? this.recipientPhone,
    isSubmitting: isSubmitting ?? this.isSubmitting,
    error: clearError ? null : error ?? this.error,
  );
}

class CreateOrderNotifier extends Notifier<CreateOrderState> {
  @override
  CreateOrderState build() => const CreateOrderState();

  void selectPackage(Package? package) =>
      state = state.copyWith(package: package, clearError: true);

  void setOriginStationId(String value) =>
      state = state.copyWith(originStationId: value.trim(), clearError: true);

  void setDestinationStationId(String value) => state = state.copyWith(
    destinationStationId: value.trim(),
    clearError: true,
  );

  void selectAddress(CustomerAddress? address) => state = state.copyWith(
    address: address,
    clearAddress: address == null,
    clearError: true,
  );

  void setRecipientName(String value) =>
      state = state.copyWith(recipientName: value, clearError: true);

  void setRecipientPhone(String value) =>
      state = state.copyWith(recipientPhone: value, clearError: true);

  ApiException? validate() {
    final order = state;
    if (order.package == null || !_guidPattern.hasMatch(order.package!.id)) {
      return const ApiException(message: 'Select a package.');
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
    final name = order.recipientName.trim();
    if (name.isEmpty) {
      return const ApiException(message: 'Recipient name is required.');
    }
    if (name.length > 150) {
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

  Future<CreateDeliveryOrderResponseDto?> submit() async {
    final validationError = validate();
    if (validationError != null) {
      state = state.copyWith(error: validationError);
      return null;
    }

    state = state.copyWith(isSubmitting: true, clearError: true);
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
          .createOrder(dto);
      ref.invalidate(myOrdersProvider);
      state = state.copyWith(isSubmitting: false);
      return result;
    } on ApiException catch (error) {
      state = state.copyWith(isSubmitting: false, error: _mapError(error));
      return null;
    } catch (_) {
      state = state.copyWith(isSubmitting: false);
      rethrow;
    }
  }

  ApiException _mapError(ApiException error) {
    if (error.code == 'MSG-P-CO-11' || error.statusCode == 409) {
      return const ApiException(
        message: 'This package is already linked to an order.',
        statusCode: 409,
        code: 'MSG-P-CO-11',
      );
    }
    return error;
  }
}

final createOrderNotifierProvider =
    NotifierProvider.autoDispose<CreateOrderNotifier, CreateOrderState>(
      CreateOrderNotifier.new,
      name: 'createOrder',
    );
