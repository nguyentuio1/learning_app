import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/delivery_order.dart';
import '../../../routes/route_paths.dart';
import '../../common/widgets/empty_state_view.dart';
import 'widgets/order_status_badge.dart';
import 'my_orders_provider.dart';

const _visibleStatusOptions = <String, String>{
  'ALL': 'All',
  'PENDING': 'Pending',
  'APPROVED': 'Approved',
  'READY_FOR_TAKEOFF': 'Ready for takeoff',
  'IN_TRANSIT': 'In transit',
  'ARRIVED': 'Arrived',
  'DELIVERED': 'Delivered',
  'CANCELLED': 'Cancelled',
  'FAILED': 'Failed',
};

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
      resizeToAvoidBottomInset: true,
      appBar: AppBar(
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
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'My Orders',
                  style: Theme.of(context).textTheme.headlineMedium
                      ?.copyWith(fontWeight: FontWeight.w700),
                ),
                const SizedBox(height: 4),
                Text(
                  'Track and manage your deliveries.',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: Theme.of(context).colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
            child: TextField(
              controller: _searchController,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                hintText: 'Search tracking number',
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
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final entry in _visibleStatusOptions.entries)
                  ChoiceChip(
                    label: Text(entry.value),
                    selected: filters.status == entry.key,
                    onSelected: (_) => ref
                        .read(deliveryOrderFilterParamsProvider.notifier)
                        .setStatus(entry.key),
                  ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Wrap(
                spacing: 8,
                runSpacing: 8,
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
                      height: 280,
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
          SizedBox(
            height: 280,
            child: EmptyStateView(
              title: 'No orders found.',
              icon: Icons.inventory_2_outlined,
            ),
          ),
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
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (context.mounted) {
              notifier.loadNextPage();
            }
          });
        }
        final order = items[index];
        final colorScheme = Theme.of(context).colorScheme;
        return Card(
          child: InkWell(
            borderRadius: BorderRadius.circular(16),
            onTap: () => _openOrder(order),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          order.trackingNumber ?? 'Order ${order.id}',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: Theme.of(context).textTheme.titleSmall
                              ?.copyWith(fontWeight: FontWeight.w700),
                        ),
                      ),
                      OrderStatusBadge(status: order.status),
                      const SizedBox(width: 4),
                      Icon(
                        Icons.chevron_right,
                        color: colorScheme.onSurfaceVariant,
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(
                    order.packageName?.trim().isNotEmpty == true
                        ? order.packageName!
                        : 'Delivery package',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodyMedium
                        ?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${_stationLabel(order.originStationName, 'Origin station')}'
                    ' → '
                    '${_stationLabel(order.destinationStationName, 'Destination station')}',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodySmall
                        ?.copyWith(color: colorScheme.onSurfaceVariant),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    _orderSummaryTime(order),
                    style: Theme.of(context).textTheme.bodySmall
                        ?.copyWith(color: colorScheme.onSurfaceVariant),
                  ),
                ],
              ),
            ),
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
      RoutePaths.customerOrderTrackingFor(order.id),
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
    child: SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 420),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.error_outline,
              size: 40,
              color: Theme.of(context).colorScheme.error,
            ),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 16),
            FilledButton(
              style: FilledButton.styleFrom(minimumSize: const Size(120, 48)),
              onPressed: onRetry,
              child: const Text('Retry'),
            ),
          ],
        ),
      ),
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

String _stationLabel(String? value, String fallback) =>
    value?.trim().isNotEmpty == true ? value! : fallback;

String _orderSummaryTime(DeliveryOrder order) {
  final status = order.status.toUpperCase();
  if (status == 'ARRIVED' ||
      status == 'DELIVERED' ||
      status == 'CANCELLED' ||
      status == 'FAILED') {
    return status[0] + status.substring(1).toLowerCase();
  }
  final eta = order.estimatedDeliveryTime;
  if (eta != null) {
    return 'ETA ${_displayDate(eta.toLocal())} ${_displayClock(eta.toLocal())}';
  }
  if (status == 'APPROVED' || status == 'IN_TRANSIT') {
    return 'ETA unavailable';
  }
  final createdAt = order.createdAt;
  return createdAt == null
      ? 'Time unavailable'
      : 'Created ${_displayDate(createdAt.toLocal())} '
            '${_displayClock(createdAt.toLocal())}';
}

String _displayClock(DateTime date) =>
    '${date.hour.toString().padLeft(2, '0')}:'
    '${date.minute.toString().padLeft(2, '0')}';
