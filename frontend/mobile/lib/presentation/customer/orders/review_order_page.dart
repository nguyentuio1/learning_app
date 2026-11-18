import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_colors.dart';
import '../../../data/providers/delivery_order_providers.dart';
import '../../../routes/route_paths.dart';
import 'create_order_notifier.dart';
import 'eta_estimation_controller.dart';
import 'widgets/eta_estimation_card.dart';

class ReviewOrderPage extends ConsumerWidget {
  const ReviewOrderPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final order = ref.watch(createOrderNotifierProvider);
    final stations = ref.watch(orderStationsProvider);
    final routePreview = ref.watch(routeEtaPreviewProvider);

    String stationValue(String id) => stations.when(
      data: (items) {
        for (final station in items) {
          if (station.id == id) {
            return '${station.stationCode} · ${station.name}';
          }
        }
        return id;
      },
      loading: () => id,
      error: (_, _) => id,
    );

    final package = order.package;
    final packageRows = package == null
        ? const <(String, String)>[]
        : <(String, String)>[
            ('Package', package.packageName),
            (
              'Weight',
              '${_formatNumber(package.weightGram / 1000)} kg '
                  '(${_formatNumber(package.weightGram)} g)',
            ),
            (
              'Dimensions',
              '${_formatNumber(package.lengthCm)} × '
                  '${_formatNumber(package.widthCm)} × '
                  '${_formatNumber(package.heightCm)} cm',
            ),
            ('Fragile', package.isFragile ? 'Yes' : 'No'),
          ];

    final recipientRows = <(String, String)>[
      ('Recipient', order.recipientName),
      ('Phone', order.recipientPhone),
      if (order.address != null) ('Address', order.address!.addressLine),
    ];

    void returnToEdit() => context.pop();

    return Scaffold(
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 24),
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(
                  width: 52,
                  height: 52,
                  child: OutlinedButton(
                    key: const Key('review-back'),
                    onPressed: returnToEdit,
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColors.customerTextPrimary,
                      backgroundColor: AppColors.customerSurface,
                      side: const BorderSide(color: AppColors.customerBorder),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                      padding: EdgeInsets.zero,
                    ),
                    child: const Icon(Icons.arrow_back),
                  ),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Review Delivery',
                        style: Theme.of(context).textTheme.headlineSmall
                            ?.copyWith(
                              color: AppColors.customerTextPrimary,
                              fontWeight: FontWeight.w700,
                            ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Check your information before submitting.',
                        style: Theme.of(context).textTheme.bodyLarge
                            ?.copyWith(color: AppColors.customerTextSecondary),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 28),
            _ReviewSection(
              title: 'Package Details',
              rows: packageRows,
              onEdit: returnToEdit,
            ),
            const SizedBox(height: 24),
            _ReviewSection(
              title: 'Delivery Route',
              rows: [
                ('Origin Station', stationValue(order.originStationId)),
                (
                  'Destination Station',
                  stationValue(order.destinationStationId),
                ),
              ],
              onEdit: returnToEdit,
            ),
            const SizedBox(height: 24),
            _ReviewSection(
              title: 'Recipient Information',
              rows: recipientRows,
              onEdit: returnToEdit,
            ),
            const SizedBox(height: 24),
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 8, 24, 12),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (routePreview != null) ...[
                RouteEtaPreviewCard(preview: routePreview),
                const SizedBox(height: 12),
              ],
              if (order.error != null) ...[
                Text(
                  order.error!.message,
                  key: const Key('order-error'),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
                const SizedBox(height: 8),
              ],
              Text(
                'A dispatcher reviews your request next. You can edit or cancel it until it is approved.',
                style: Theme.of(context).textTheme.bodySmall
                    ?.copyWith(color: AppColors.customerTextSecondary),
              ),
              const SizedBox(height: 12),
              FilledButton(
                key: const Key('order-submit'),
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.customerSeed,
                  minimumSize: const Size.fromHeight(52),
                ),
                onPressed: order.isSubmitting
                    ? null
                    : () async {
                        final result = await ref
                            .read(createOrderNotifierProvider.notifier)
                            .submit();
                        if (result != null && context.mounted) {
                          context.push(
                            RoutePaths.customerOrderSubmitted,
                            extra: result,
                          );
                        }
                      },
                child: order.isSubmitting
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Submit Delivery Request'),
              ),
              TextButton(
                key: const Key('review-back-to-edit'),
                onPressed: returnToEdit,
                style: TextButton.styleFrom(
                  foregroundColor: AppColors.customerSeed,
                ),
                child: const Text('Back to Edit'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ReviewSection extends StatelessWidget {
  const _ReviewSection({required this.title, required this.rows, this.onEdit});

  final String title;
  final List<(String, String)> rows;
  final VoidCallback? onEdit;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Expanded(
            child: Text(
              title,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: AppColors.customerTextPrimary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          if (onEdit != null)
            TextButton(
              onPressed: onEdit,
              style: TextButton.styleFrom(
                foregroundColor: AppColors.customerSeed,
              ),
              child: const Text('Edit'),
            ),
        ],
      ),
      const SizedBox(height: 8),
      _ReviewCard(
        child: Column(
          children: [
            for (var index = 0; index < rows.length; index++) ...[
              if (index > 0) const Divider(height: 1, color: Color(0xFFF1F5F9)),
              _ReviewRow(label: rows[index].$1, value: rows[index].$2),
            ],
          ],
        ),
      ),
    ],
  );
}

class _ReviewCard extends StatelessWidget {
  const _ReviewCard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
    decoration: BoxDecoration(
      color: AppColors.customerSurface,
      border: Border.all(color: AppColors.customerBorder),
      borderRadius: BorderRadius.circular(16),
    ),
    child: child,
  );
}

class _ReviewRow extends StatelessWidget {
  const _ReviewRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 12),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Text(
            label,
            style: Theme.of(context).textTheme.bodyMedium
                ?.copyWith(color: AppColors.customerTextSecondary),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            value,
            textAlign: TextAlign.right,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: AppColors.customerTextPrimary,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ],
    ),
  );
}

String _formatNumber(double value) {
  final text = value.toString();
  return text.endsWith('.0') ? text.substring(0, text.length - 2) : text;
}
