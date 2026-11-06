import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/storage/storage_providers.dart';
import '../../../data/models/landing_station.dart';
import '../../../data/models/station_availability.dart';
import '../../../data/models/station_detail.dart';
import '../../../data/models/station_slot.dart';
import '../../../data/providers/landing_station_providers.dart';

class StationAvailabilityState {
  const StationAvailabilityState({
    required this.stations,
    required this.selectedStationId,
    required this.availability,
    required this.detail,
    this.slotFilter = 'ALL',
    this.isRefreshing = false,
  });

  final List<LandingStation> stations;
  final String? selectedStationId;
  final StationAvailability? availability;
  final StationDetail? detail;
  final String slotFilter;
  final bool isRefreshing;

  LandingStation? get currentStation {
    if (selectedStationId == null) return null;
    for (final s in stations) {
      if (s.id == selectedStationId) return s;
    }
    return null;
  }

  List<StationSlot> get filteredSlots {
    final allSlots = detail?.slots ?? const <StationSlot>[];
    if (slotFilter == 'ALL') return allSlots;
    return allSlots.where((s) => s.status == slotFilter).toList(growable: false);
  }

  StationAvailabilityState copyWith({
    List<LandingStation>? stations,
    String? selectedStationId,
    StationAvailability? availability,
    StationDetail? detail,
    String? slotFilter,
    bool? isRefreshing,
  }) =>
      StationAvailabilityState(
        stations: stations ?? this.stations,
        selectedStationId: selectedStationId ?? this.selectedStationId,
        availability: availability ?? this.availability,
        detail: detail ?? this.detail,
        slotFilter: slotFilter ?? this.slotFilter,
        isRefreshing: isRefreshing ?? this.isRefreshing,
      );
}

class StationAvailabilityNotifier
    extends AsyncNotifier<StationAvailabilityState> {
  @override
  FutureOr<StationAvailabilityState> build() async {
    final service = ref.watch(landingStationApiServiceProvider);
    final userStorage = ref.watch(userStorageProvider);
    final user = await userStorage.read();

    final stations = await service.getStations();
    if (stations.isEmpty) {
      return const StationAvailabilityState(
        stations: [],
        selectedStationId: null,
        availability: null,
        detail: null,
      );
    }

    LandingStation? assigned;
    if (user != null) {
      for (final s in stations) {
        if (s.operatorId == user.id) {
          assigned = s;
          break;
        }
      }
    }
    final initialStationId = assigned?.id ?? stations.first.id;

    final availability = await service.getStationAvailability(initialStationId);
    StationDetail? detail;
    try {
      detail = await service.getStationDetail(initialStationId);
    } catch (_) {}

    return StationAvailabilityState(
      stations: stations,
      selectedStationId: initialStationId,
      availability: availability,
      detail: detail,
    );
  }

  Future<void> selectStation(String stationId) async {
    final current = state.value;
    if (current == null) return;
    if (current.selectedStationId == stationId) return;

    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final service = ref.read(landingStationApiServiceProvider);
      final availability = await service.getStationAvailability(stationId);
      StationDetail? detail;
      try {
        detail = await service.getStationDetail(stationId);
      } catch (_) {}

      return current.copyWith(
        selectedStationId: stationId,
        availability: availability,
        detail: detail,
      );
    });
  }

  void setSlotFilter(String filter) {
    final current = state.value;
    if (current == null) return;
    state = AsyncData(current.copyWith(slotFilter: filter));
  }

  Future<void> refresh() async {
    final current = state.value;
    if (current == null || current.selectedStationId == null) {
      ref.invalidateSelf();
      return;
    }

    final stationId = current.selectedStationId!;
    final service = ref.read(landingStationApiServiceProvider);

    try {
      final stations = await service.getStations();
      final availability = await service.getStationAvailability(stationId);
      StationDetail? detail;
      try {
        detail = await service.getStationDetail(stationId);
      } catch (_) {}

      state = AsyncData(
        current.copyWith(
          stations: stations,
          availability: availability,
          detail: detail,
        ),
      );
    } catch (e, st) {
      state = AsyncError(e, st);
    }
  }
}

final stationAvailabilityNotifierProvider = AsyncNotifierProvider.autoDispose<
    StationAvailabilityNotifier, StationAvailabilityState>(
  StationAvailabilityNotifier.new,
  name: 'stationAvailabilityNotifier',
);
