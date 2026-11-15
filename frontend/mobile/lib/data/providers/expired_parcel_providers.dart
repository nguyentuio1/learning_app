import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../models/expired_parcel.dart';
import '../services/expired_parcel_api_service.dart';

final expiredParcelApiServiceProvider = Provider<ExpiredParcelApiService>(
  (ref) => ExpiredParcelApiService(ref.watch(dioProvider)),
  name: 'expiredParcelApiService',
);

final expiredParcelsProvider = FutureProvider.autoDispose
    .family<List<ExpiredParcel>, String>(
      (ref, stationId) => ref
          .watch(expiredParcelApiServiceProvider)
          .getExpiredParcels(stationId),
      name: 'expiredParcels',
    );

final pickupTimeoutAlertsProvider = FutureProvider.autoDispose
    .family<List<PickupTimeoutAlert>, String>((ref, stationId) async {
      final parcels = await ref.watch(expiredParcelsProvider(stationId).future);
      return parcels
          .map(PickupTimeoutAlert.fromExpiredParcel)
          .toList(growable: false);
    }, name: 'pickupTimeoutAlerts');
