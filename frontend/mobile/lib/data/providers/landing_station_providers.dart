import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/network/network_providers.dart';
import '../models/landing_station.dart';
import '../models/station_availability.dart';
import '../models/station_detail.dart';
import '../services/landing_station_api_service.dart';

final landingStationApiServiceProvider = Provider<LandingStationApiService>(
  (ref) => LandingStationApiService(ref.watch(dioProvider)),
  name: 'landingStationApiService',
);

final allLandingStationsProvider =
    FutureProvider.autoDispose<List<LandingStation>>(
      (ref) => ref.watch(landingStationApiServiceProvider).getStations(),
      name: 'allLandingStations',
    );

final landingStationAvailabilityProvider = FutureProvider.autoDispose
    .family<StationAvailability, String>(
      (ref, stationId) => ref
          .watch(landingStationApiServiceProvider)
          .getStationAvailability(stationId),
      name: 'landingStationAvailability',
    );

final landingStationDetailProvider =
    FutureProvider.autoDispose.family<StationDetail, String>(
      (ref, stationId) => ref
          .watch(landingStationApiServiceProvider)
          .getStationDetail(stationId),
      name: 'landingStationDetail',
    );
