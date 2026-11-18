import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../core/storage/storage_providers.dart';
import '../../../data/models/landing_station.dart';
import '../../../data/models/station_availability.dart';
import '../../../data/models/station_detail.dart';
import '../../../data/models/station_slot.dart';
import '../../../data/providers/expired_parcel_providers.dart';
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
  int _generation = 0;

  bool _isCurrent(int generation) =>
      !_isDisposed && ref.mounted && generation == _generation;

  // Riverpod may still publish a pending build result after a manual refresh.
  // A superseded build must return the latest snapshot, never its old scope.
  StationAvailabilityState _supersededBuildState() {
    if (ref.mounted) {
      final latest = state.value;
      if (latest != null) return latest;
    }
    return const StationAvailabilityState(
      stations: [],
      selectedStationId: null,
      availability: null,
      detail: null,
    );
  }

  List<LandingStation> _allowedStations(
    List<LandingStation> stations,
    String? userId,
    bool isOperator,
  ) {
    if (!isOperator) return stations;
    for (final station in stations) {
      if (userId != null && station.operatorId == userId) return [station];
    }
    return const [];
  }

  static const Duration pollingInterval = Duration(seconds: 15);

  @override
  FutureOr<StationAvailabilityState> build() async {
    _isDisposed = false;
    final generation = ++_generation;
    ref.onDispose(() {
      _isDisposed = true;
      _generation++;
      _pollingTimer?.cancel();
    });

    final service = ref.watch(landingStationApiServiceProvider);
    final userStorage = ref.watch(userStorageProvider);
    final user = await userStorage.read();
    if (!_isCurrent(generation)) return _supersededBuildState();

    final allStations = await service.getStations();
    if (!_isCurrent(generation)) {
      return _supersededBuildState();
    }

    final isOperator = user?.roleCode.toUpperCase() == 'STATION_OPERATOR';
    final selectableStations = _allowedStations(
      allStations,
      user?.id,
      isOperator,
    );
    if (selectableStations.isEmpty) {
      return StationAvailabilityState(
        stations: const [],
        selectedStationId: null,
        availability: null,
        detail: null,
        isUnassignedOperator: isOperator,
      );
    }
    final initialStationId = selectableStations
        .firstWhere(
          (s) => user != null && s.operatorId == user.id,
          orElse: () => selectableStations.first,
        )
        .id;

    final availability = await service.getStationAvailability(initialStationId);
    if (!_isCurrent(generation)) {
      return _supersededBuildState();
    }

    StationDetail? detail;
    try {
      detail = await service.getStationDetail(initialStationId);
    } catch (_) {}

    if (!_isCurrent(generation)) return _supersededBuildState();
    _startPolling();

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
    if (_isDisposed || !ref.mounted) return;
    final current = state.value;
    if (current == null || current.selectedStationId == null) return;

    if (current.isRefreshing) return;
    final generation = _generation;
    final stationId = current.selectedStationId!;
    ref.invalidate(expiredParcelsProvider(stationId));
    final service = ref.read(landingStationApiServiceProvider);

    try {
      final availability = await service.getStationAvailability(stationId);
      if (!_isCurrent(generation)) return;

      StationDetail? detail;
      try {
        detail = await service.getStationDetail(stationId);
      } catch (_) {}

      if (!_isCurrent(generation)) return;

      // Re-read state.value after network awaits.
      // If user switched stations or state was reset, drop stale poll result.
      final latest = state.value;
      if (latest == null || latest.selectedStationId != stationId) {
        return;
      }

      final updatedDetail = detail ?? latest.detail;
      state = AsyncData(
        latest.copyWith(availability: availability, detail: updatedDetail),
      );
    } catch (_) {
      // Background silent polling error is ignored to not disrupt UI
    }
  }

  Future<void> selectStation(String stationId) async {
    if (_isDisposed || !ref.mounted) return;
    final current = state.value;
    if (current == null) return;
    if (current.selectedStationId == stationId) return;
    if (!current.stations.any((station) => station.id == stationId)) return;
    final generation = ++_generation;

    // Use isRefreshing flag and keep existing data rather than showing a full-screen spinner
    state = AsyncData(
      current.copyWith(isRefreshing: true, clearErrorMessage: true),
    );

    final service = ref.read(landingStationApiServiceProvider);

    try {
      final availability = await service.getStationAvailability(stationId);
      if (!_isCurrent(generation)) return;

      StationDetail? detail;
      try {
        detail = await service.getStationDetail(stationId);
      } catch (_) {
        // detail may be 403 or network error
      }

      if (!_isCurrent(generation)) return;

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
      if (!_isCurrent(generation)) return;
      final latest = state.value ?? current;
      final msg = e is ApiException ? e.message : 'Failed to switch station.';
      state = AsyncData(
        latest.copyWith(isRefreshing: false, errorMessage: msg),
      );
    }

    if (_isCurrent(generation)) {
      _startPolling();
    }
  }

  void setSlotFilter(String filter) {
    final current = state.value;
    if (current == null) return;
    state = AsyncData(current.copyWith(slotFilter: filter));
  }

  Future<void> refresh() async {
    if (_isDisposed || !ref.mounted) return;
    final current = state.value;
    if (current == null) {
      ref.invalidateSelf();
      return;
    }
    final generation = ++_generation;
    final service = ref.read(landingStationApiServiceProvider);
    state = AsyncData(
      current.copyWith(isRefreshing: true, clearErrorMessage: true),
    );

    try {
      final user = await ref.read(userStorageProvider).read();
      if (!_isCurrent(generation)) return;
      final allStations = await service.getStations();
      if (!_isCurrent(generation)) return;
      final isOperator = user?.roleCode.toUpperCase() == 'STATION_OPERATOR';
      final stations = _allowedStations(allStations, user?.id, isOperator);
      final selectedId = isOperator
          ? (stations.isEmpty ? null : stations.first.id)
          : stations.any((s) => s.id == current.selectedStationId)
          ? current.selectedStationId
          : (stations.isEmpty ? null : stations.first.id);
      final sameStation =
          selectedId != null && selectedId == current.selectedStationId;
      // Reconcile scope before fetching detail, so reassignment/removal never
      // leaves the previous station's data visible during a slow or failed request.
      state = AsyncData(
        StationAvailabilityState(
          stations: stations,
          selectedStationId: selectedId,
          availability: sameStation ? state.value?.availability : null,
          detail: sameStation ? state.value?.detail : null,
          slotFilter: sameStation ? state.value!.slotFilter : 'ALL',
          isRefreshing: selectedId != null,
          isUnassignedOperator: isOperator && stations.isEmpty,
        ),
      );
      if (current.selectedStationId != null) {
        ref.invalidate(expiredParcelsProvider(current.selectedStationId!));
      }
      if (selectedId == null) {
        _pollingTimer?.cancel();
        return;
      }
      ref.invalidate(expiredParcelsProvider(selectedId));
      final availability = await service.getStationAvailability(selectedId);
      if (!_isCurrent(generation)) return;
      StationDetail? detail;
      try {
        detail = await service.getStationDetail(selectedId);
      } catch (_) {}
      if (!_isCurrent(generation)) return;
      final latest = state.value;
      if (latest == null || latest.selectedStationId != selectedId) return;
      state = AsyncData(
        latest.copyWith(
          availability: availability,
          detail: detail,
          clearDetail: detail == null,
          isRefreshing: false,
          clearErrorMessage: true,
        ),
      );
    } catch (error, stack) {
      if (!_isCurrent(generation)) return;
      if (error is ApiException &&
          (error.statusCode == 401 || error.statusCode == 403)) {
        state = AsyncError(error, stack);
        _pollingTimer?.cancel();
        return;
      }
      final latest = state.value;
      if (latest != null) {
        state = AsyncData(
          latest.copyWith(
            isRefreshing: false,
            errorMessage: error is ApiException
                ? error.message
                : 'Refresh failed.',
          ),
        );
      }
    }
    if (_isCurrent(generation) && state.value?.selectedStationId != null) {
      _startPolling();
    }
  }
}

final stationAvailabilityNotifierProvider =
    AsyncNotifierProvider.autoDispose<
      StationAvailabilityNotifier,
      StationAvailabilityState
    >(StationAvailabilityNotifier.new, name: 'stationAvailabilityNotifier');
