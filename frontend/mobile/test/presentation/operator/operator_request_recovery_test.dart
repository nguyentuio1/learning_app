import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/station_operation_models.dart';
import 'package:smart_drone_delivery/data/services/station_operations_api_service.dart';
import 'package:smart_drone_delivery/presentation/operator/intake/intake_controller.dart';
import 'package:smart_drone_delivery/presentation/operator/pickup/pickup_controller.dart';

StationOrderInfo _order({String role = 'ORIGIN', String tracking = 'DD-ONE'}) =>
    StationOrderInfo(
      orderId: tracking,
      trackingNumber: tracking,
      status: role == 'ORIGIN' ? 'APPROVED' : 'ARRIVED',
      role: role,
      packageName: 'Test parcel',
      declaredWeightGram: 500,
      declaredLengthCm: 20,
      declaredWidthCm: 15,
      declaredHeightCm: 10,
      isFragile: false,
      originStationId: 'origin',
      destinationStationId: 'destination',
    );

class _Operations extends StationOperationsApiService {
  _Operations() : super(Dio());
  StationOrderInfo info = _order();
  Completer<StationOrderInfo>? lookup;
  Completer<PackageIntakeResponse>? intake;
  Completer<ConfirmPickupResponse>? pickup;
  Completer<RejectIntakeResponse>? reject;
  int intakeCalls = 0;
  int pickupCalls = 0;

  @override
  Future<StationOrderInfo> lookupOrder({
    required String stationId,
    required String trackingNumber,
  }) async => lookup?.future ?? info;
  @override
  Future<PackageIntakeResponse> confirmIntake({
    required String stationId,
    required PackageIntakeRequest request,
  }) async {
    intakeCalls++;
    return intake?.future ?? intakeResult;
  }

  @override
  Future<ConfirmPickupResponse> confirmPickup({
    required String stationId,
    required ConfirmPickupRequest request,
  }) async {
    pickupCalls++;
    return pickup?.future ?? pickupResult;
  }

  @override
  Future<RejectIntakeResponse> rejectIntake({
    required String stationId,
    required RejectIntakeRequest request,
  }) async =>
      reject?.future ??
      const RejectIntakeResponse(
        orderId: 'DD-ONE',
        trackingNumber: 'DD-ONE',
        status: 'FAILED',
      );
}

final intakeResult = PackageIntakeResponse(
  orderId: 'DD-ONE',
  trackingNumber: 'DD-ONE',
  status: 'READY_FOR_TAKEOFF',
  slotId: 'slot-1',
  slotNumber: 1,
  confirmedAt: DateTime.utc(2026),
);
final pickupResult = ConfirmPickupResponse(
  orderId: 'DD-ONE',
  trackingNumber: 'DD-ONE',
  status: 'DELIVERED',
  actualDeliveryTime: DateTime.utc(2026),
  slotId: 'slot-1',
  slotReleased: true,
);

ProviderContainer _container(_Operations api) {
  final c = ProviderContainer(
    overrides: [stationOperationsApiServiceProvider.overrideWithValue(api)],
  );
  addTearDown(c.dispose);
  final a = c.listen(intakeControllerProvider, (_, _) {});
  final b = c.listen(pickupControllerProvider, (_, _) {});
  addTearDown(a.close);
  addTearDown(b.close);
  return c;
}

Future<bool> _confirmIntake(
  IntakeController controller, {
  bool inspected = true,
}) => controller.confirmIntake(
  stationId: 'origin',
  trackingNumber: 'DD-ONE',
  slotId: 'slot-1',
  measuredWeightGram: 505,
  measuredLengthCm: 21,
  measuredWidthCm: 16,
  measuredHeightCm: 11,
  visualInspectionPassed: inspected,
);
Future<bool> _confirmPickup(PickupController controller) =>
    controller.confirmPickup(
      stationId: 'destination',
      trackingNumber: 'DD-ONE',
      secureCode: '123456',
    );

void main() {
  test('intake releases protected lookup state when the page stops listening', () async {
    final api = _Operations();
    final c = ProviderContainer(overrides: [stationOperationsApiServiceProvider.overrideWithValue(api)]);
    addTearDown(c.dispose);
    final first = c.listen(intakeControllerProvider, (_, _) {});
    await c.read(intakeControllerProvider.notifier).lookupOrder(stationId: 'origin', trackingNumber: 'DD-ONE');
    first.close();
    await c.pump();
    final next = c.listen(intakeControllerProvider, (_, _) {});
    addTearDown(next.close);
    expect(c.read(intakeControllerProvider).orderInfo, isNull);
  });

  for (final actor in ['intake', 'pickup']) {
    test('$actor discards a lookup that completes after reset', () async {
      final api = _Operations()..lookup = Completer<StationOrderInfo>();
      final c = _container(api);
      final controller = actor == 'intake'
          ? c.read(intakeControllerProvider.notifier)
          : c.read(pickupControllerProvider.notifier);
      final request = actor == 'intake'
          ? (controller as IntakeController).lookupOrder(
              stationId: 'origin',
              trackingNumber: 'DD-ONE',
            )
          : (controller as PickupController).lookupOrder(
              stationId: 'destination',
              trackingNumber: 'DD-ONE',
            );
      if (controller is IntakeController) {
        controller.reset();
      } else {
        (controller as PickupController).reset();
      }
      api.lookup!.complete(_order());
      await request;
      expect(
        actor == 'intake'
            ? c.read(intakeControllerProvider).orderInfo
            : c.read(pickupControllerProvider).orderInfo,
        isNull,
      );
    });
    test(
      '$actor keeps a newer lookup when an older lookup completes late',
      () async {
        final pending = Completer<StationOrderInfo>();
        final api = _Operations()..lookup = pending;
        final c = _container(api);
        Future<void> lookup(String tracking) => actor == 'intake'
            ? c
                  .read(intakeControllerProvider.notifier)
                  .lookupOrder(stationId: 'origin', trackingNumber: tracking)
            : c
                  .read(pickupControllerProvider.notifier)
                  .lookupOrder(
                    stationId: 'destination',
                    trackingNumber: tracking,
                  );
        final first = lookup('DD-ONE');
        api.lookup = null;
        api.info = _order(tracking: 'DD-TWO');
        await lookup('DD-TWO');
        pending.complete(_order());
        await first;
        expect(
          (actor == 'intake'
                  ? c.read(intakeControllerProvider).orderInfo
                  : c.read(pickupControllerProvider).orderInfo)
              ?.trackingNumber,
          'DD-TWO',
        );
      },
    );
  }

  test('intake refuses an unchecked inspection before sending', () async {
    final api = _Operations();
    final c = _container(api);
    final controller = c.read(intakeControllerProvider.notifier);
    await controller.lookupOrder(stationId: 'origin', trackingNumber: 'DD-ONE');
    expect(await _confirmIntake(controller, inspected: false), isFalse);
    expect(api.intakeCalls, 0);
  });
  test('intake sends once while pending and after completion', () async {
    final api = _Operations()..intake = Completer<PackageIntakeResponse>();
    final c = _container(api);
    final controller = c.read(intakeControllerProvider.notifier);
    await controller.lookupOrder(stationId: 'origin', trackingNumber: 'DD-ONE');
    final first = _confirmIntake(controller);
    final second = _confirmIntake(controller);
    api.intake!.complete(intakeResult);
    expect(await first, isTrue);
    expect(await second, isFalse);
    expect(await _confirmIntake(controller), isFalse);
    expect(api.intakeCalls, 1);
  });
  test('pickup sends once while pending and after completion', () async {
    final api = _Operations()
      ..info = _order(role: 'DESTINATION')
      ..pickup = Completer<ConfirmPickupResponse>();
    final c = _container(api);
    final controller = c.read(pickupControllerProvider.notifier);
    await controller.lookupOrder(
      stationId: 'destination',
      trackingNumber: 'DD-ONE',
    );
    final first = _confirmPickup(controller);
    final second = _confirmPickup(controller);
    api.pickup!.complete(pickupResult);
    expect(await first, isTrue);
    expect(await second, isFalse);
    expect(await _confirmPickup(controller), isFalse);
    expect(api.pickupCalls, 1);
  });
  test('intake ignores a mutation response after reset', () async {
    final api = _Operations()..intake = Completer<PackageIntakeResponse>();
    final c = _container(api);
    final controller = c.read(intakeControllerProvider.notifier);
    await controller.lookupOrder(stationId: 'origin', trackingNumber: 'DD-ONE');
    final first = _confirmIntake(controller);
    controller.reset();
    api.intake!.complete(intakeResult);
    expect(await first, isFalse);
    expect(c.read(intakeControllerProvider).completedIntake, isNull);
  });
  test('pickup ignores a mutation response after reset', () async {
    final api = _Operations()
      ..info = _order(role: 'DESTINATION')
      ..pickup = Completer<ConfirmPickupResponse>();
    final c = _container(api);
    final controller = c.read(pickupControllerProvider.notifier);
    await controller.lookupOrder(
      stationId: 'destination',
      trackingNumber: 'DD-ONE',
    );
    final first = _confirmPickup(controller);
    controller.reset();
    api.pickup!.complete(pickupResult);
    expect(await first, isFalse);
    expect(c.read(pickupControllerProvider).completedPickup, isNull);
  });
  test('intake rejection ignores a response after reset', () async {
    final api = _Operations()..reject = Completer<RejectIntakeResponse>();
    final c = _container(api);
    final controller = c.read(intakeControllerProvider.notifier);
    await controller.lookupOrder(stationId: 'origin', trackingNumber: 'DD-ONE');
    final first = controller.rejectIntake(
      stationId: 'origin',
      trackingNumber: 'DD-ONE',
      reason: 'Damaged package',
    );
    controller.reset();
    api.reject!.complete(
      const RejectIntakeResponse(
        orderId: 'DD-ONE',
        trackingNumber: 'DD-ONE',
        status: 'FAILED',
      ),
    );
    expect(await first, isFalse);
    expect(c.read(intakeControllerProvider).completedRejection, isNull);
  });
}
