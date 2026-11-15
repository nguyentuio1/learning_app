import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/models/expired_parcel.dart';
import '../../../data/providers/expired_parcel_providers.dart';
import '../../../data/providers/landing_station_providers.dart';
import '../home/station_availability_providers.dart';

class ClearExpiredParcelNotifier extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<void> clearExpiredParcel({
    required String stationId,
    required ClearExpiredParcelRequest request,
  }) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      await ref
          .read(expiredParcelApiServiceProvider)
          .clearExpiredParcel(stationId: stationId, request: request);
      ref.invalidate(expiredParcelsProvider(stationId));
      ref.invalidate(pickupTimeoutAlertsProvider(stationId));
      ref.invalidate(landingStationDetailProvider(stationId));
      ref.invalidate(stationAvailabilityNotifierProvider);
    });
  }
}

final clearExpiredParcelNotifierProvider =
    AsyncNotifierProvider.autoDispose<ClearExpiredParcelNotifier, void>(
      ClearExpiredParcelNotifier.new,
      name: 'clearExpiredParcelNotifier',
    );
