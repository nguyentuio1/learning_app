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
    bool clearDetail = false,
    String? slotFilter,
    bool? isRefreshing,
  }) =>
      StationAvailabilityState(
        stations: stations ?? this.stations,
        selectedStationId: selectedStationId ?? this.selectedStationId,
        availability: availability ?? this.availability,
        detail: clearDetail ? null : (detail ?? this.detail),
        slotFilter: slotFilter ?? this.slotFilter,
        isRefreshing: isRefreshing ?? this.isRefreshing,
      );
}

class StationAvailabilityNotifier
    extends AsyncNotifier<StationAvailabilityState> {
  Timer? _pollingTimer;

  static const Duration pollingInterval = Duration(seconds: 15);

  @override
  FutureOr<StationAvailabilityState> build() async {
    ref.onDispose(() {
      _pollingTimer?.cancel();
    });

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

    _startPolling();

    return StationAvailabilityState(
      stations: stations,
      selectedStationId: initialStationId,
      availability: availability,
      detail: detail,
    );
  }

  void _startPolling() {
    _pollingTimer?.cancel();
    _pollingTimer = Timer.periodic(pollingInterval, (_) {
      _pollSilently();
    });
  }

  Future<void> _pollSilently() async {
    final current = state.value;
    if (current == null || current.selectedStationId == null) return;

    final stationId = current.selectedStationId!;
    final service = ref.read(landingStationApiServiceProvider);

    try {
      final availability = await service.getStationAvailability(stationId);
      StationDetail? detail;
      try {
        detail = await service.getStationDetail(stationId);
      } catch (_) {}

      // Keep existing detail if new detail failed, or update if successful
      final updatedDetail = detail ?? current.detail;
      state = AsyncData(
        current.copyWith(
          availability: availability,
          detail: updatedDetail,
        ),
      );
    } catch (_) {
      // Background silent polling error is ignored to not disrupt UI
    }
  }

  Future<void> selectStation(String stationId) async {
    final current = state.value;
    if (current == null) return;
    if (current.selectedStationId == stationId) return;

    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final service = ref.read(landingStationApiServiceProvider);
      // Fetch availability for new station
      final availability = await service.getStationAvailability(stationId);
      StationDetail? detail;
      try {
        detail = await service.getStationDetail(stationId);
      } catch (_) {
        // detail may be null if 403 or network error
      }

      // Explicitly clear old detail if new detail is null
      return current.copyWith(
        selectedStationId: stationId,
        availability: availability,
        detail: detail,
        clearDetail: detail == null,
      );
    });

    _startPolling();
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
          clearDetail: detail == null,
        ),
      );
    } catch (e, st) {
      state = AsyncError(e, st);
    }

    _startPolling();
  }
}

final stationAvailabilityNotifierProvider = AsyncNotifierProvider.autoDispose<
    StationAvailabilityNotifier, StationAvailabilityState>(
  StationAvailabilityNotifier.new,
  name: 'stationAvailabilityNotifier',
);
