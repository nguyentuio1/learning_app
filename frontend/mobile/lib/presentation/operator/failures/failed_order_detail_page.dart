import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/models/failure_models.dart';
import '../../../data/providers/failure_providers.dart';
import 'resolve_failure_sheet.dart';

class FailedOrderDetailPage extends ConsumerWidget {
  const FailedOrderDetailPage({required this.orderId, super.key});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(failedOrderDetailProvider(orderId));
    return Scaffold(
      appBar: AppBar(title: const Text('Failed delivery details')),
      body: detail.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(errorMessage(error), textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton(
                  onPressed: () =>
                      ref.invalidate(failedOrderDetailProvider(orderId)),
                  child: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (order) => _buildDetail(context, ref, order),
      ),
    );
  }

  Widget _buildDetail(
    BuildContext context,
    WidgetRef ref,
    FailureOrderDetail order,
  ) {
    final canResolve =
        order.status == 'FAILED' &&
        order.latestFailure.resolutionStatus == 'UNRESOLVED';
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Text(
          order.trackingNumber,
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 12),
        _detailRow('Status', order.status),
        _detailRow('Package', order.package?.packageName ?? 'Unknown package'),
        _detailRow('Origin', _stationLabel(order.originStation)),
        _detailRow('Destination', _stationLabel(order.destinationStation)),
        _detailRow('Return to origin', order.returnToOriginStatus),
        const SizedBox(height: 8),
        _FailureHistory(episodes: order.failureHistory),
        const SizedBox(height: 12),
        _ResolutionHistory(
          events: order.resolutionHistory,
          complete: order.resolutionHistoryComplete,
        ),
        if (canResolve) ...[
          const SizedBox(height: 20),
          FilledButton.icon(
            onPressed: () => _openResolution(context, order),
            icon: const Icon(Icons.rule_folder_outlined),
            label: const Text('Resolve failure'),
          ),
        ],
      ],
    );
  }

  Future<void> _openResolution(
    BuildContext context,
    FailureOrderDetail order,
  ) async {
    final action = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (_) => ResolveFailureSheet(detail: order),
    );
    if (!context.mounted || action == null) return;
    final (messageId, message) = switch (action) {
      'RESCHEDULE' => ('MSG-P-DW-16', 'Delivery rescheduled successfully.'),
      'RETURN_TO_ORIGIN' => (
        'MSG-P-DW-17',
        'Return requested. Waiting for receipt at the origin station.',
      ),
      'CANCEL' => ('MSG-P-DW-18', 'Order cancelled successfully.'),
      _ => ('failure-resolution-saved', 'Failure resolution saved.'),
    };
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(key: ValueKey(messageId), content: Text(message)));
  }
}

class _FailureHistory extends StatelessWidget {
  const _FailureHistory({required this.episodes});

  final List<FailureEpisode> episodes;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Failure history',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: 8),
          if (episodes.isEmpty)
            const Text('No failure history available.')
          else
            for (final episode in episodes)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: Icon(
                  Icons.error_outline,
                  color: Theme.of(context).colorScheme.error,
                ),
                title: Text(
                  '${episode.failureType} · ${episode.resolutionStatus}',
                ),
                subtitle: Text(
                  '${episode.failureReason ?? 'No reason provided.'}\n'
                  '${_formatDate(episode.failedAt)}',
                ),
                isThreeLine: true,
              ),
        ],
      ),
    ),
  );
}

class _ResolutionHistory extends StatelessWidget {
  const _ResolutionHistory({required this.events, required this.complete});

  final List<FailureHandlingEvent> events;
  final bool complete;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Resolution history',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          if (!complete) ...[
            const SizedBox(height: 6),
            const Text('Some older resolution events are not shown.'),
          ],
          const SizedBox(height: 8),
          if (events.isEmpty)
            const Text('No resolution decisions recorded.')
          else
            for (final event in events)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.history),
                title: Text(event.action),
                subtitle: Text(
                  '${event.note ?? 'No note provided.'}\n'
                  '${_formatDate(event.createdAt)}',
                ),
                isThreeLine: true,
              ),
        ],
      ),
    ),
  );
}

Widget _detailRow(String label, String value) => Padding(
  padding: const EdgeInsets.only(bottom: 12),
  child: Row(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      SizedBox(width: 140, child: Text(label)),
      Expanded(child: SelectableText(value.isEmpty ? '—' : value)),
    ],
  ),
);

String _stationLabel(OrderStationDto? station) => station == null
    ? 'Unknown station'
    : [
        station.name,
        station.stationCode,
        station.address,
      ].whereType<String>().where((value) => value.isNotEmpty).join(' · ');

String _formatDate(DateTime value) =>
    value.toLocal().toString().split('.').first;
