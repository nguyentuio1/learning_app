import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
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
    this.isUnassignedOperator = false,
    this.errorMessage,
  });

  final List<LandingStation> stations;
  final String? selectedStationId;
  final StationAvailability? availability;
  final StationDetail? detail;
  final String slotFilter;
  final bool isRefreshing;
  final bool isUnassignedOperator;
  final String? errorMessage;

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
    bool? isUnassignedOperator,
    String? errorMessage,
    bool clearErrorMessage = false,
  }) =>
      StationAvailabilityState(
        stations: stations ?? this.stations,
        selectedStationId: selectedStationId ?? this.selectedStationId,
        availability: availability ?? this.availability,
        detail: clearDetail ? null : (detail ?? this.detail),
        slotFilter: slotFilter ?? this.slotFilter,
        isRefreshing: isRefreshing ?? this.isRefreshing,
        isUnassignedOperator: isUnassignedOperator ?? this.isUnassignedOperator,
        errorMessage: clearErrorMessage ? null : (errorMessage ?? this.errorMessage),
      );
}

class StationAvailabilityNotifier
    extends AsyncNotifier<StationAvailabilityState> {
  Timer? _pollingTimer;
  bool _isDisposed = false;

  static const Duration pollingInterval = Duration(seconds: 15);

  @override
  FutureOr<StationAvailabilityState> build() async {
    _isDisposed = false;
    ref.onDispose(() {
      _isDisposed = true;
      _pollingTimer?.cancel();
    });

    final service = ref.watch(landingStationApiServiceProvider);
    final userStorage = ref.watch(userStorageProvider);
    final user = await userStorage.read();

    final allStations = await service.getStations();
    if (_isDisposed) {
      return const StationAvailabilityState(
        stations: [],
        selectedStationId: null,
        availability: null,
        detail: null,
      );
    }

    if (allStations.isEmpty) {
      return const StationAvailabilityState(
        stations: [],
        selectedStationId: null,
        availability: null,
        detail: null,
      );
    }

    final isOperator = user?.roleCode == 'STATION_OPERATOR';
    List<LandingStation> selectableStations = allStations;
    LandingStation? assigned;

    if (user != null) {
      for (final s in allStations) {
        if (s.operatorId == user.id) {
          assigned = s;
          break;
        }
      }
    }

    if (isOperator) {
      if (assigned == null) {
        // Station Operator without assigned station -> explicit unassigned state
        return const StationAvailabilityState(
          stations: [],
          selectedStationId: null,
          availability: null,
          detail: null,
          isUnassignedOperator: true,
        );
      }
      // Station Operator dropdown is limited to their assigned station
      selectableStations = [assigned];
    }

    final initialStationId = assigned?.id ?? allStations.first.id;

    final availability = await service.getStationAvailability(initialStationId);
    if (_isDisposed) {
      return const StationAvailabilityState(
        stations: [],
        selectedStationId: null,
        availability: null,
        detail: null,
      );
    }

    StationDetail? detail;
    try {
      detail = await service.getStationDetail(initialStationId);
    } catch (_) {}

    if (!_isDisposed) {
      _startPolling();
    }

    return StationAvailabilityState(
      stations: selectableStations,
      selectedStationId: initialStationId,
      availability: availability,
      detail: detail,
    );
  }

  void _startPolling() {
    _pollingTimer?.cancel();
    if (_isDisposed) return;
    _pollingTimer = Timer.periodic(pollingInterval, (_) {
      if (!_isDisposed) {
        _pollSilently();
      }
    });
  }

  Future<void> _pollSilently() async {
    if (_isDisposed) return;
    final current = state.value;
    if (current == null || current.selectedStationId == null) return;

    final stationId = current.selectedStationId!;
    final service = ref.read(landingStationApiServiceProvider);

    try {
      final availability = await service.getStationAvailability(stationId);
      if (_isDisposed) return;

      StationDetail? detail;
      try {
        detail = await service.getStationDetail(stationId);
      } catch (_) {}

      if (_isDisposed) return;

      // Re-read state.value after network awaits.
      // If user switched stations or state was reset, drop stale poll result.
      final latest = state.value;
      if (latest == null || latest.selectedStationId != stationId) {
        return;
      }

      final updatedDetail = detail ?? latest.detail;
      state = AsyncData(
        latest.copyWith(
          availability: availability,
          detail: updatedDetail,
        ),
      );
    } catch (_) {
      // Background silent polling error is ignored to not disrupt UI
    }
  }

  Future<void> selectStation(String stationId) async {
    if (_isDisposed) return;
    final current = state.value;
    if (current == null) return;
    if (current.selectedStationId == stationId) return;

    // Use isRefreshing flag and keep existing data rather than showing a full-screen spinner
    state = AsyncData(
      current.copyWith(
        isRefreshing: true,
        clearErrorMessage: true,
      ),
    );

    final service = ref.read(landingStationApiServiceProvider);

    try {
      final availability = await service.getStationAvailability(stationId);
      if (_isDisposed) return;

      StationDetail? detail;
      try {
        detail = await service.getStationDetail(stationId);
      } catch (_) {
        // detail may be 403 or network error
      }

      if (_isDisposed) return;

      final latest = state.value ?? current;
      state = AsyncData(
        latest.copyWith(
          selectedStationId: stationId,
          availability: availability,
          detail: detail,
          clearDetail: detail == null,
          isRefreshing: false,
          clearErrorMessage: true,
        ),
      );
    } catch (e) {
      if (_isDisposed) return;
      final latest = state.value ?? current;
      final msg = e is ApiException ? e.message : 'Failed to switch station.';
      state = AsyncData(
        latest.copyWith(
          isRefreshing: false,
          errorMessage: msg,
        ),
      );
    }

    if (!_isDisposed) {
      _startPolling();
    }
  }

  void setSlotFilter(String filter) {
    final current = state.value;
    if (current == null) return;
    state = AsyncData(current.copyWith(slotFilter: filter));
  }

  Future<void> refresh() async {
    if (_isDisposed) return;
    final current = state.value;
    if (current == null || current.selectedStationId == null) {
      ref.invalidateSelf();
      return;
    }

    final stationId = current.selectedStationId!;
    final service = ref.read(landingStationApiServiceProvider);

    state = AsyncData(
      current.copyWith(
        isRefreshing: true,
        clearErrorMessage: true,
      ),
    );

    try {
      final stations = await service.getStations();
      if (_isDisposed) return;

      final availability = await service.getStationAvailability(stationId);
      if (_isDisposed) return;

      StationDetail? detail;
      try {
        detail = await service.getStationDetail(stationId);
      } catch (_) {}

      if (_isDisposed) return;

      final latest = state.value ?? current;
      // Stale check: if station selection changed during refresh, discard old station data
      if (latest.selectedStationId != stationId) {
        return;
      }

      state = AsyncData(
        latest.copyWith(
          stations: stations,
          availability: availability,
          detail: detail,
          clearDetail: detail == null,
          isRefreshing: false,
          clearErrorMessage: true,
        ),
      );
    } catch (e) {
      if (_isDisposed) return;
      final latest = state.value ?? current;
      state = AsyncData(
        latest.copyWith(
          isRefreshing: false,
          errorMessage: e is ApiException ? e.message : 'Refresh failed.',
        ),
      );
    }

    if (!_isDisposed) {
      _startPolling();
    }
  }
}

final stationAvailabilityNotifierProvider = AsyncNotifierProvider.autoDispose<
    StationAvailabilityNotifier, StationAvailabilityState>(
  StationAvailabilityNotifier.new,
  name: 'stationAvailabilityNotifier',
);
