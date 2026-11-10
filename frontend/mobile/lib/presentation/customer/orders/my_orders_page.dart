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
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            if (context.canPop()) {
              context.pop();
            } else {
              context.go(RoutePaths.customerHome);
            }
          },
        ),
        actions: [
          IconButton(
            key: const Key('create-order'),
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
                      label: Text(
                        status == 'ALL' ? 'All' : status.replaceAll('_', ' '),
                      ),
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
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Wrap(
                spacing: 8,
                children: [
                  OutlinedButton.icon(
                    key: const Key('order-date-range-button'),
                    onPressed: _selectDateRange,
                    icon: const Icon(Icons.date_range),
                    label: const Text('Date range'),
                  ),
                  if (filters.fromDate != null && filters.toDate != null)
                    InputChip(
                      avatar: const Icon(Icons.calendar_today, size: 18),
                      deleteIcon: const Icon(Icons.close),
                      deleteButtonTooltipMessage: 'Clear date range',
                      label: Text(
                        '${_displayDate(DateTime.parse(filters.fromDate!))} - '
                        '${_displayDate(DateTime.parse(filters.toDate!))}',
                      ),
                      onDeleted: () => ref
                          .read(deliveryOrderFilterParamsProvider.notifier)
                          .setDateRange(null, null),
                    ),
                ],
              ),
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
              onSubmitted: (keyword) => ref
                  .read(deliveryOrderFilterParamsProvider.notifier)
                  .setSearchKeyword(keyword),
            ),
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
                      child: _LoadError(
                        message: _listErrorMessage(error),
                        onRetry: () => ref.invalidate(myOrdersProvider),
                      ),
                    ),
                  ],
                ),
                data: _buildOrderList,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildOrderList(List<DeliveryOrder> items) {
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
            return _LoadError(
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

  Future<void> _selectDateRange() async {
    final filters = ref.read(deliveryOrderFilterParamsProvider);
    final fromDate = filters.fromDate == null
        ? null
        : DateTime.parse(filters.fromDate!);
    final toDate = filters.toDate == null
        ? null
        : DateTime.parse(filters.toDate!);
    final initialDateRange = fromDate != null && toDate != null
        ? DateTimeRange(start: fromDate, end: toDate)
        : null;
    final now = DateTime.now();
    final selected = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2000),
      lastDate: DateTime(now.year + 10),
      initialDateRange: initialDateRange,
      helpText: 'Select order date range',
    );
    if (!mounted || selected == null) return;
    ref
        .read(deliveryOrderFilterParamsProvider.notifier)
        .setDateRange(selected.start, selected.end);
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
}

class _LoadError extends StatelessWidget {
  const _LoadError({required this.message, required this.onRetry});

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
  if (error is ApiException && error.statusCode == 400) return error.message;
  return 'Unable to load orders. Please try again.';
}

String _displayDate(DateTime date) =>
    '${date.day.toString().padLeft(2, '0')}/'
    '${date.month.toString().padLeft(2, '0')}/${date.year}';
