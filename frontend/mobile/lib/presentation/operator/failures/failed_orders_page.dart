import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/models/failure_models.dart';
import '../../../data/providers/failure_providers.dart';
import '../../../routes/route_paths.dart';
import 'mark_failed_sheet.dart';

class FailedOrdersPage extends ConsumerStatefulWidget {
  const FailedOrdersPage({super.key});

  @override
  ConsumerState<FailedOrdersPage> createState() => _FailedOrdersPageState();
}

class _FailedOrdersPageState extends ConsumerState<FailedOrdersPage> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final query = ref.watch(failureOrderQueryProvider);
    final orders = ref.watch(failedOrdersProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Failed deliveries'),
        actions: [
          IconButton(
            key: const Key('report-failure-action'),
            tooltip: 'Report Failure',
            icon: const Icon(Icons.report_gmailerrorred_outlined),
            onPressed: _openMarkFailedSheet,
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
            child: Column(
              children: [
                TextField(
                  controller: _searchController,
                  textInputAction: TextInputAction.search,
                  onSubmitted: (value) => _updateSearch(value),
                  decoration: InputDecoration(
                    labelText: 'Search tracking or customer',
                    prefixIcon: const Icon(Icons.search),
                    suffixIcon: IconButton(
                      tooltip: 'Apply search',
                      onPressed: () => _updateSearch(_searchController.text),
                      icon: const Icon(Icons.check),
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 12,
                  runSpacing: 8,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    SizedBox(
                      width: 175,
                      child: DropdownButtonFormField<String>(
                        initialValue: query.failureType,
                        decoration: const InputDecoration(
                          labelText: 'Failure type',
                          isDense: true,
                        ),
                        items: [
                          const DropdownMenuItem(child: Text('All types')),
                          for (final type in _failureTypes)
                            DropdownMenuItem(value: type, child: Text(type)),
                        ],
                        onChanged: (value) => ref
                            .read(failureOrderQueryProvider.notifier)
                            .update(
                              failureType: value,
                              clearFailureType: value == null,
                            ),
                      ),
                    ),
                    SizedBox(
                      width: 180,
                      child: DropdownButtonFormField<String>(
                        initialValue: query.resolutionStatus,
                        decoration: const InputDecoration(
                          labelText: 'Resolution',
                          isDense: true,
                        ),
                        items: [
                          const DropdownMenuItem(child: Text('All statuses')),
                          for (final status in _resolutionStatuses)
                            DropdownMenuItem(
                              value: status,
                              child: Text(status),
                            ),
                        ],
                        onChanged: (value) => ref
                            .read(failureOrderQueryProvider.notifier)
                            .update(
                              resolutionStatus: value,
                              clearResolutionStatus: value == null,
                            ),
                      ),
                    ),
                    OutlinedButton.icon(
                      onPressed: () => _selectDateRange(query),
                      icon: const Icon(Icons.date_range),
                      label: Text(
                        query.fromDate == null
                            ? 'Date range'
                            : '${_shortDate(query.fromDate!)} – '
                                  '${_shortDate(query.toDate!)}',
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const Divider(height: 1),
          Expanded(
            child: orders.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => _LoadError(
                message: errorMessage(error),
                onRetry: () => ref.invalidate(failedOrdersProvider),
              ),
              data: (page) => page.items.isEmpty
                  ? const Center(child: Text('No failed deliveries found.'))
                  : RefreshIndicator(
                      onRefresh: () async =>
                          ref.invalidate(failedOrdersProvider),
                      child: ListView.separated(
                        padding: const EdgeInsets.all(16),
                        itemCount: page.items.length + 1,
                        separatorBuilder: (_, _) => const SizedBox(height: 8),
                        itemBuilder: (context, index) {
                          if (index == page.items.length) {
                            return _Pagination(
                              page: page,
                              query: query,
                              onPageChanged: (number) => ref
                                  .read(failureOrderQueryProvider.notifier)
                                  .update(page: number),
                            );
                          }
                          final order = page.items[index];
                          return _FailedOrderCard(
                            order: order,
                            onTap: () => context.push(
                              RoutePaths.operatorFailedOrderDetailFor(
                                order.orderId,
                              ),
                            ),
                          );
                        },
                      ),
                    ),
            ),
          ),
        ],
      ),
    );
  }

  void _updateSearch(String value) {
    ref
        .read(failureOrderQueryProvider.notifier)
        .update(
          searchKeyword: value.trim(),
          clearSearchKeyword: value.trim().isEmpty,
        );
  }

  Future<void> _openMarkFailedSheet() async {
    final reported = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (_) => const MarkFailedSheet(),
    );
    if (!mounted || reported != true) return;
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(const SnackBar(content: Text('Delivery marked as failed.')));
  }

  Future<void> _selectDateRange(FailureOrderQuery query) async {
    final initialStart = query.fromDate == null
        ? null
        : DateTime.tryParse(query.fromDate!);
    final initialEnd = query.toDate == null
        ? null
        : DateTime.tryParse(query.toDate!);
    final range = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 365)),
      initialDateRange: initialStart != null && initialEnd != null
          ? DateTimeRange(start: initialStart, end: initialEnd)
          : null,
    );
    if (range == null || !mounted) return;
    ref
        .read(failureOrderQueryProvider.notifier)
        .update(
          fromDate: DateTime(
            range.start.year,
            range.start.month,
            range.start.day,
          ).toIso8601String(),
          toDate: DateTime(
            range.end.year,
            range.end.month,
            range.end.day,
            23,
            59,
            59,
          ).toIso8601String(),
        );
  }

  String _shortDate(String value) {
    final date = DateTime.tryParse(value);
    return date == null
        ? value
        : '${date.year}-${date.month.toString().padLeft(2, '0')}-'
              '${date.day.toString().padLeft(2, '0')}';
  }
}

class _FailedOrderCard extends StatelessWidget {
  const _FailedOrderCard({required this.order, required this.onTap});

  final FailedOrderSummary order;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      order.trackingNumber,
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                  _StatusBadge(label: 'FAILED', color: theme.colorScheme.error),
                  const SizedBox(width: 6),
                  _StatusBadge(
                    label: order.failureType,
                    color: Colors.deepOrange.shade700,
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(order.failureReason ?? 'No failure reason provided.'),
              const SizedBox(height: 8),
              Text('Failed: ${_formatDate(order.failedAt)}'),
              const SizedBox(height: 8),
              Text('Package: ${order.packageName}'),
              Text('Origin: ${_stationLabel(order.originStation)}'),
              Text('Destination: ${_stationLabel(order.destinationStation)}'),
              const SizedBox(height: 8),
              _StatusBadge(
                label: order.resolutionStatus,
                color: _resolutionColor(order.resolutionStatus),
              ),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton.icon(
                  onPressed: onTap,
                  icon: const Icon(Icons.chevron_right),
                  label: const Text('View details'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Pagination extends StatelessWidget {
  const _Pagination({
    required this.page,
    required this.query,
    required this.onPageChanged,
  });

  final PagedFailedOrders page;
  final FailureOrderQuery query;
  final ValueChanged<int> onPageChanged;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisAlignment: MainAxisAlignment.center,
    children: [
      IconButton(
        tooltip: 'Previous page',
        onPressed: query.page > 1 ? () => onPageChanged(query.page - 1) : null,
        icon: const Icon(Icons.chevron_left),
      ),
      Text(
        'Page ${page.page} of ${page.totalPages} · ${page.totalCount} orders',
      ),
      IconButton(
        tooltip: 'Next page',
        onPressed: page.hasMore ? () => onPageChanged(query.page + 1) : null,
        icon: const Icon(Icons.chevron_right),
      ),
    ],
  );
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
    decoration: BoxDecoration(
      color: color.withValues(alpha: 0.12),
      borderRadius: BorderRadius.circular(20),
    ),
    child: Text(
      label,
      style: TextStyle(color: color, fontWeight: FontWeight.bold, fontSize: 11),
    ),
  );
}

class _LoadError extends StatelessWidget {
  const _LoadError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(message, textAlign: TextAlign.center),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('Retry'),
          ),
        ],
      ),
    ),
  );
}

const _failureTypes = [
  'MISSION_ABORTED',
  'EMERGENCY_LANDING',
  'HARDWARE_FAULT',
];
const _resolutionStatuses = ['UNRESOLVED', 'RETURN_REQUESTED', 'RESOLVED'];

String _stationLabel(OrderStationDto? station) => station == null
    ? 'Unknown station'
    : [
        station.name,
        station.stationCode,
      ].whereType<String>().where((value) => value.isNotEmpty).join(' · ');

Color _resolutionColor(String status) => switch (status) {
  'UNRESOLVED' => Colors.orange.shade800,
  'RETURN_REQUESTED' => Colors.blue.shade700,
  'RESOLVED' => Colors.green.shade700,
  _ => Colors.grey,
};

String _formatDate(DateTime value) =>
    value.toLocal().toString().split('.').first;
