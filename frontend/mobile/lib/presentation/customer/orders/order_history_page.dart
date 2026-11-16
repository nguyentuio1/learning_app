import 'package:flutter/material.dart';

import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../common/widgets/empty_state_view.dart';

/// C10 â€” Order History & Audit Log
///
/// Shows a vertical timeline of all status transitions for a single delivery
/// order. Data is passed in as [statusLogs] from the caller (C06/C07/C08
/// order detail) so this page requires no additional API call.
class OrderHistoryPage extends StatelessWidget {
  const OrderHistoryPage({
    super.key,
    required this.trackingNumber,
    required this.statusLogs,
  });

  final String trackingNumber;
  final List<DeliveryOrderStatusLogDto> statusLogs;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Order History'),
        leading: IconButton(
          tooltip: 'Back',
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            if (Navigator.of(context).canPop()) {
              Navigator.of(context).pop();
            }
          },
        ),
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 16, 24, 0),
            child: Text(
              trackingNumber,
              style: Theme.of(context).textTheme.titleMedium
                  ?.copyWith(fontWeight: FontWeight.bold),
            ),
          ),
          const SizedBox(height: 8),
          Expanded(
            child: statusLogs.isEmpty
                ? const EmptyStateView(
                    key: Key('order-history-empty'),
                    title: 'No order history found.',
                    icon: Icons.history_outlined,
                  )
                : ListView.builder(
                    key: const Key('order-history-timeline'),
                    padding: const EdgeInsets.fromLTRB(24, 8, 24, 24),
                    itemCount: statusLogs.length,
                    itemBuilder: (context, index) {
                      final log = statusLogs[index];
                      final hasNext = index < statusLogs.length - 1;
                      return _TimelineEntry(
                        log: log,
                        hasNext: hasNext,
                        isFirst: index == 0,
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }
}

class _TimelineEntry extends StatelessWidget {
  const _TimelineEntry({
    required this.log,
    required this.hasNext,
    required this.isFirst,
  });

  final DeliveryOrderStatusLogDto log;
  final bool hasNext;
  final bool isFirst;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final dotColor = isFirst
        ? colorScheme.primary
        : colorScheme.onSurfaceVariant;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Timeline spine
          SizedBox(
            width: 24,
            child: Column(
              children: [
                Icon(Icons.circle, size: 12, color: dotColor),
                if (hasNext)
                  Expanded(
                    child: Center(
                      child: Container(
                        width: 2,
                        color: colorScheme.outlineVariant,
                      ),
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          // Content
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(bottom: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Status transition
                  Text(
                    _statusLabel(log),
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: isFirst ? FontWeight.bold : FontWeight.w500,
                    ),
                  ),
                  const SizedBox(height: 4),
                  // Timestamp
                  Text(
                    _formatTimestamp(log.createdAt),
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: colorScheme.onSurfaceVariant,
                    ),
                  ),
                  // Remarks
                  if (log.remarks != null && log.remarks!.isNotEmpty) ...[
                    const SizedBox(height: 4),
                    Text(log.remarks!, style: theme.textTheme.bodySmall),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  String _statusLabel(DeliveryOrderStatusLogDto log) {
    if (log.fromStatus != null && log.fromStatus!.isNotEmpty) {
      return '${log.fromStatus} \u2192 ${log.toStatus}';
    }
    return log.toStatus;
  }

  String _formatTimestamp(DateTime value) {
    final local = value.toLocal();
    final date =
        '${local.year}-${local.month.toString().padLeft(2, '0')}-${local.day.toString().padLeft(2, '0')}';
    final time =
        '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}:${local.second.toString().padLeft(2, '0')}';
    return '$date $time';
  }
}
