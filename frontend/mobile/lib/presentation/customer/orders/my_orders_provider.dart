import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/models/delivery_order.dart';
import '../../../data/models/delivery_order_filter_params.dart';
import '../../../data/providers/delivery_order_providers.dart';

class DeliveryOrderFilterNotifier extends Notifier<DeliveryOrderFilterParams> {
  @override
  DeliveryOrderFilterParams build() => const DeliveryOrderFilterParams();

  void setStatus(String status) {
    if (!deliveryOrderStatuses.contains(status)) {
      throw ArgumentError.value(status, 'status', 'Unsupported order status.');
    }
    state = state.copyWith(page: 1, status: status);
  }

  void setSearchKeyword(String keyword) {
    final normalized = keyword.trim();
    if (state.searchKeyword == (normalized.isEmpty ? null : normalized) &&
        state.page == 1) {
      return;
    }
    state = state.copyWith(
      page: 1,
      searchKeyword: normalized.isEmpty ? null : normalized,
      clearSearchKeyword: normalized.isEmpty,
    );
  }

  void setDateRange({DateTime? fromDate, DateTime? toDate}) {
    state = state.copyWith(
      page: 1,
      fromDate: fromDate?.toUtc().toIso8601String(),
      clearFromDate: fromDate == null,
      toDate: toDate?.toUtc().toIso8601String(),
      clearToDate: toDate == null,
    );
  }
}

final deliveryOrderFilterParamsProvider =
    NotifierProvider<DeliveryOrderFilterNotifier, DeliveryOrderFilterParams>(
      DeliveryOrderFilterNotifier.new,
      name: 'deliveryOrderFilterParams',
    );

class MyOrdersNotifier extends AsyncNotifier<List<DeliveryOrder>> {
  bool _hasMore = true;
  bool _loadingMore = false;
  int _currentPage = 1;
  int _queryGeneration = 0;
  Object? _loadMoreError;

  bool get hasMore => _hasMore;

  bool get isLoadingMore => _loadingMore;

  Object? get loadMoreError => _loadMoreError;

  @override
  Future<List<DeliveryOrder>> build() async {
    _queryGeneration++;
    _loadMoreError = null;
    final params = ref.watch(deliveryOrderFilterParamsProvider);
    _currentPage = params.page;
    _hasMore = true;
    _loadingMore = false;
    final response = await ref
        .watch(deliveryOrderApiServiceProvider)
        .getMyOrders(params);
    _currentPage = response.page;
    _hasMore = response.hasMore;
    return response.items;
  }

  Future<void> loadNextPage() async {
    final existingItems = state.value;
    if (!_hasMore || _loadingMore || existingItems == null) return;
    final params = ref.read(deliveryOrderFilterParamsProvider);
    final nextPage = _currentPage + 1;
    final requestGeneration = _queryGeneration;
    _loadingMore = true;
    _loadMoreError = null;

    try {
      final response = await ref
          .read(deliveryOrderApiServiceProvider)
          .getMyOrders(params.copyWith(page: nextPage));
      if (!ref.mounted || requestGeneration != _queryGeneration) return;
      _currentPage = response.page;
      _hasMore = response.hasMore;
      state = AsyncData([...existingItems, ...response.items]);
    } catch (error) {
      if (!ref.mounted || requestGeneration != _queryGeneration) return;
      _loadMoreError = error;
      state = AsyncData([...existingItems]);
    } finally {
      if (requestGeneration == _queryGeneration) _loadingMore = false;
    }
  }
}

final myOrdersProvider =
    AsyncNotifierProvider.autoDispose<MyOrdersNotifier, List<DeliveryOrder>>(
      MyOrdersNotifier.new,
      name: 'myOrders',
    );
