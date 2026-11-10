import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/models/delivery_order.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/models/delivery_order_filter_params.dart';
import '../../../data/providers/delivery_order_providers.dart';

class DeliveryOrderFilterNotifier extends Notifier<DeliveryOrderFilterParams> {
  @override
  DeliveryOrderFilterParams build() => const DeliveryOrderFilterParams();

  void setStatus(String status) {
    state = state.copyWith(page: 1, status: status);
  }

  void setSearchKeyword(String keyword) {
    final normalized = keyword.trim();
    state = state.copyWith(
      page: 1,
      searchKeyword: normalized.isEmpty ? null : normalized,
      clearSearchKeyword: normalized.isEmpty,
    );
  }

  void setDateRange(DateTime? fromDate, DateTime? toDate) {
    state = state.copyWith(
      page: 1,
      fromDate: fromDate?.toIso8601String(),
      clearFromDate: fromDate == null,
      toDate: toDate?.toIso8601String(),
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
  int _generation = 0;
  Object? _loadMoreError;

  bool get hasMore => _hasMore;
  bool get isLoadingMore => _loadingMore;
  Object? get loadMoreError => _loadMoreError;

  @override
  Future<List<DeliveryOrder>> build() async {
    final generation = ++_generation;
    final params = ref.watch(deliveryOrderFilterParamsProvider);
    _currentPage = params.page;
    _hasMore = true;
    _loadingMore = false;
    _loadMoreError = null;
    final page = await ref
        .watch(deliveryOrderApiServiceProvider)
        .getOrders(params);
    if (generation == _generation) {
      _currentPage = page.page;
      _hasMore = page.hasMore;
    }
    return page.items;
  }

  Future<void> loadNextPage() async {
    final currentItems = state.value;
    if (state.isLoading || !_hasMore || _loadingMore || currentItems == null) {
      return;
    }

    final params = ref.read(deliveryOrderFilterParamsProvider);
    final generation = _generation;
    final nextPage = _currentPage + 1;
    _loadingMore = true;
    _loadMoreError = null;
    state = AsyncData([...currentItems]);
    try {
      final page = await ref
          .read(deliveryOrderApiServiceProvider)
          .getOrders(params.copyWith(page: nextPage));
      if (!ref.mounted || generation != _generation) return;
      _currentPage = page.page;
      _hasMore = page.hasMore;
      state = AsyncData([...currentItems, ...page.items]);
    } catch (error) {
      if (!ref.mounted || generation != _generation) return;
      _loadMoreError = error;
      state = AsyncData([...currentItems]);
    } finally {
      _loadingMore = false;
    }
  }
}

final myOrdersProvider =
    AsyncNotifierProvider.autoDispose<MyOrdersNotifier, List<DeliveryOrder>>(
      MyOrdersNotifier.new,
      name: 'myOrders',
    );

class OrderDetailNotifier
    extends AsyncNotifier<DeliveryOrderDetailResponseDto> {
  OrderDetailNotifier(this.orderId);

  final String orderId;

  @override
  Future<DeliveryOrderDetailResponseDto> build() =>
      ref.watch(deliveryOrderApiServiceProvider).getOrderDetail(orderId);

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(deliveryOrderApiServiceProvider).getOrderDetail(orderId),
    );
  }
}

final orderDetailProvider = AsyncNotifierProvider.autoDispose
    .family<OrderDetailNotifier, DeliveryOrderDetailResponseDto, String>(
      OrderDetailNotifier.new,
      name: 'orderDetail',
    );
