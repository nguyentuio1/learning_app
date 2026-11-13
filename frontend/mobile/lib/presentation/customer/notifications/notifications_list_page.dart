import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/models/notification_model.dart';
import '../../../routes/route_paths.dart';
import 'notification_notifier.dart';

class NotificationsListPage extends ConsumerWidget {
  const NotificationsListPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final notificationsAsync = ref.watch(customerNotificationsProvider);
    final onlyUnread = ref.watch(notificationsFilterProvider);

    ref.listen<AsyncValue<void>>(notificationActionsNotifierProvider, (_, next) {
      if (next.hasError) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Operation failed: ${next.error}')),
        );
      }
    });

    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
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
          TextButton(
            onPressed: () {
              ref
                  .read(notificationActionsNotifierProvider.notifier)
                  .markAllAsRead();
            },
            child: const Text('Mark all read'),
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: SegmentedButton<bool>(
              segments: const [
                ButtonSegment<bool>(
                  value: false,
                  label: Text('All'),
                  icon: Icon(Icons.notifications_none),
                ),
                ButtonSegment<bool>(
                  value: true,
                  label: Text('Unread'),
                  icon: Icon(Icons.mark_email_unread_outlined),
                ),
              ],
              selected: {onlyUnread},
              onSelectionChanged: (selection) {
                ref.read(notificationsFilterProvider.notifier).state =
                    selection.first;
              },
            ),
          ),
          Expanded(
            child: notificationsAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text('Failed to load notifications: $error'),
                    const SizedBox(height: 12),
                    FilledButton(
                      onPressed: () =>
                          ref.invalidate(customerNotificationsProvider),
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
              data: (page) {
                final items = page.items;
                if (items.isEmpty) {
                  return const Center(child: Text('No notifications'));
                }
                return RefreshIndicator(
                  onRefresh: () async {
                    ref.invalidate(customerNotificationsProvider);
                  },
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: items.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 12),
                    itemBuilder: (context, index) {
                      final item = items[index];
                      return _NotificationCard(item: item);
                    },
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _NotificationCard extends ConsumerWidget {
  const _NotificationCard({required this.item});

  final NotificationModel item;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Card(
      elevation: item.isRead ? 0 : 2,
      color: item.isRead
          ? Theme.of(context).cardColor
          : Theme.of(context).colorScheme.primaryContainer.withOpacity(0.15),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(
          color: item.isRead
              ? Colors.grey.shade300
              : Theme.of(context).colorScheme.primary.withOpacity(0.5),
        ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () {
          if (!item.isRead) {
            ref
                .read(notificationActionsNotifierProvider.notifier)
                .markAsRead(item.id);
          }
          if (item.orderId != null && item.orderId!.isNotEmpty) {
            context.push(
              RoutePaths.customerOrderDetails.replaceFirst(
                ':orderId',
                item.orderId!,
              ),
            );
          }
        },
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (!item.isRead) ...[
                    Container(
                      width: 8,
                      height: 8,
                      margin: const EdgeInsets.only(top: 6, right: 8),
                      decoration: BoxDecoration(
                        color: Theme.of(context).colorScheme.primary,
                        shape: BoxShape.circle,
                      ),
                    ),
                  ],
                  Expanded(
                    child: Text(
                      item.title,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: item.isRead
                                ? FontWeight.w500
                                : FontWeight.bold,
                          ),
                    ),
                  ),
                  Text(
                    _formatDate(item.createdAt),
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: Colors.grey.shade600,
                        ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                item.message,
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: 12),
              Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  if (!item.isRead)
                    TextButton(
                      onPressed: () {
                        ref
                            .read(notificationActionsNotifierProvider.notifier)
                            .markAsRead(item.id);
                      },
                      child: const Text('Mark as read'),
                    ),
                  if (item.orderId != null && item.orderId!.isNotEmpty) ...[
                    const SizedBox(width: 8),
                    FilledButton.tonal(
                      onPressed: () {
                        context.push(
                          RoutePaths.customerOrderDetails.replaceFirst(
                            ':orderId',
                            item.orderId!,
                          ),
                        );
                      },
                      child: const Text('View order'),
                    ),
                  ],
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  String _formatDate(DateTime date) {
    return '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')} ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }
}
