import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/delivery_order.dart';
import '../../../data/models/delivery_order_filter_params.dart';
import '../../../routes/route_paths.dart';
import 'my_orders_provider.dart';

class MyOrdersPage extends ConsumerStatefulWidget {
  const MyOrdersPage({super.key});

  @override
  ConsumerState<MyOrdersPage> createState() => _MyOrdersPageState();
}

class _MyOrdersPageState extends ConsumerState<MyOrdersPage> {
  final _searchController = TextEditingController();
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_loadMoreWhenNearEnd);
  }

  @override
  void dispose() {
    _scrollController
      ..removeListener(_loadMoreWhenNearEnd)
      ..dispose();
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final orders = ref.watch(myOrdersProvider);
    final filters = ref.watch(deliveryOrderFilterParamsProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('My orders'),
        actions: [
          IconButton(
            key: const Key('create-order-open'),
            tooltip: 'Create delivery order',
            onPressed: () => context.push(RoutePaths.customerOrdersCreate),
            icon: const Icon(Icons.add),
          ),
        ],
      ),
      body: Column(
        children: [
          SizedBox(
            height: 56,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 12),
              children: [
                for (final status in deliveryOrderStatuses)
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4),
                    child: ChoiceChip(
                      label: Text(_statusLabel(status)),
                      selected: filters.status == status,
                      onSelected: (_) => ref
                          .read(deliveryOrderFilterParamsProvider.notifier)
                          .setStatus(status),
                    ),
                  ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
            child: TextField(
              controller: _searchController,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                labelText: 'Search tracking number',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: IconButton(
                  tooltip: 'Clear search',
                  onPressed: () {
                    _searchController.clear();
                    ref
                        .read(deliveryOrderFilterParamsProvider.notifier)
                        .setSearchKeyword('');
                  },
                  icon: const Icon(Icons.clear),
                ),
                border: const OutlineInputBorder(),
              ),
              onChanged: (keyword) => ref
                  .read(deliveryOrderFilterParamsProvider.notifier)
                  .setSearchKeyword(keyword),
            ),
          ),
          _DateRangeFilter(
            filters: filters,
            onPickFrom: () => _pickDate(isFromDate: true, filters: filters),
            onPickTo: () => _pickDate(isFromDate: false, filters: filters),
            onClear: _clearDateRange,
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _refreshOrders,
              child: orders.when(
                loading: () => ListView(
                  controller: _scrollController,
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: const [
                    SizedBox(
                      height: 240,
                      child: Center(child: CircularProgressIndicator()),
                    ),
                  ],
                ),
                error: (error, _) => ListView(
                  controller: _scrollController,
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: [
                    SizedBox(
                      height: 240,
                      child: _OrderLoadError(
                        message: _listErrorMessage(error),
                        onRetry: () => ref.invalidate(myOrdersProvider),
                      ),
                    ),
                  ],
                ),
                data: (items) => _buildOrderList(items, orders),
                skipError: true,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildOrderList(
    List<DeliveryOrder> items,
    AsyncValue<List<DeliveryOrder>> orders,
  ) {
    if (items.isEmpty) {
      return ListView(
        controller: _scrollController,
        physics: const AlwaysScrollableScrollPhysics(),
        children: const [
          SizedBox(height: 240, child: Center(child: Text('No orders found.'))),
        ],
      );
    }

    final notifier = ref.read(myOrdersProvider.notifier);
    final showFooter = notifier.hasMore || notifier.loadMoreError != null;
    return ListView.separated(
      controller: _scrollController,
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(16),
      itemCount: items.length + (showFooter ? 1 : 0),
      separatorBuilder: (_, _) => const SizedBox(height: 8),
      itemBuilder: (context, index) {
        if (index >= items.length) {
          if (notifier.loadMoreError != null) {
            return _OrderLoadError(
              message: _listErrorMessage(notifier.loadMoreError!),
              onRetry: notifier.loadNextPage,
            );
          }
          return const Padding(
            padding: EdgeInsets.all(16),
            child: Center(child: CircularProgressIndicator()),
          );
        }
        if (index >= items.length - 3 && notifier.hasMore) {
          notifier.loadNextPage();
        }
        final order = items[index];
        return Card(
          child: ListTile(
            title: Text(
              order.trackingNumber ?? 'Order ${order.id}',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
            subtitle: Text('${order.recipientName} · ${order.status}'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => _openOrder(order),
          ),
        );
      },
    );
  }

  void _loadMoreWhenNearEnd() {
    if (!_scrollController.hasClients) return;
    if (_scrollController.position.extentAfter < 300) {
      ref.read(myOrdersProvider.notifier).loadNextPage();
    }
  }

  Future<void> _refreshOrders() async {
    ref.invalidate(myOrdersProvider);
    try {
      await ref.read(myOrdersProvider.future);
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(_listErrorMessage(error))));
    }
  }

  Future<void> _openOrder(DeliveryOrder order) async {
    await context.push(
      RoutePaths.customerOrderDetails.replaceFirst(
        ':orderId',
        Uri.encodeComponent(order.id),
      ),
      extra: order,
    );
    if (!mounted) return;
    ref.invalidate(myOrdersProvider);
  }

  Future<void> _pickDate({
    required bool isFromDate,
    required DeliveryOrderFilterParams filters,
  }) async {
    final currentDate = DateTime.now();
    final initialDate = DateTime.tryParse(
      (isFromDate ? filters.fromDate : filters.toDate) ?? '',
    )?.toLocal();
    final date = await showDatePicker(
      context: context,
      initialDate: initialDate ?? currentDate,
      firstDate: DateTime(2000),
      lastDate: DateTime(currentDate.year + 10),
    );
    if (!mounted || date == null) return;
    final parsedFromDate = DateTime.tryParse(filters.fromDate ?? '')?.toLocal();
    final parsedToDate = DateTime.tryParse(filters.toDate ?? '')?.toLocal();
    ref
        .read(deliveryOrderFilterParamsProvider.notifier)
        .setDateRange(
          fromDate: isFromDate ? date : parsedFromDate,
          toDate: isFromDate ? parsedToDate : date,
        );
  }

  void _clearDateRange() {
    ref.read(deliveryOrderFilterParamsProvider.notifier).setDateRange();
  }
}

class _DateRangeFilter extends StatelessWidget {
  const _DateRangeFilter({
    required this.filters,
    required this.onPickFrom,
    required this.onPickTo,
    required this.onClear,
  });

  final DeliveryOrderFilterParams filters;
  final VoidCallback onPickFrom;
  final VoidCallback onPickTo;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(horizontal: 12),
    child: Wrap(
      spacing: 4,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        TextButton(
          onPressed: onPickFrom,
          child: Text(
            filters.fromDate == null
                ? 'From date'
                : 'From ${_dateLabel(filters.fromDate!)}',
          ),
        ),
        TextButton(
          onPressed: onPickTo,
          child: Text(
            filters.toDate == null
                ? 'To date'
                : 'To ${_dateLabel(filters.toDate!)}',
          ),
        ),
        if (filters.fromDate != null || filters.toDate != null)
          IconButton(
            tooltip: 'Clear date range',
            onPressed: onClear,
            icon: const Icon(Icons.clear),
          ),
      ],
    ),
  );
}

String _dateLabel(String isoDate) =>
    DateTime.parse(isoDate).toLocal().toString().substring(0, 10);

class _OrderLoadError extends StatelessWidget {
  const _OrderLoadError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
    child: Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(message, textAlign: TextAlign.center),
        const SizedBox(height: 12),
        FilledButton(onPressed: onRetry, child: const Text('Retry')),
      ],
    ),
  );
}

String _listErrorMessage(Object error) {
  if (error is ApiException &&
      (error.code == 'MSG-P-CO-23' || error.code == 'MSG-P-CO-24')) {
    return error.message;
  }
  return 'Unable to load orders. Please try again.';
}

String _statusLabel(String status) =>
    status == 'ALL' ? 'All' : status.replaceAll('_', ' ');
