import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/expired_parcel.dart';
import '../../../data/providers/expired_parcel_providers.dart';
import 'clear_expired_parcel_notifier.dart';

class ExpiredParcelScreen extends ConsumerWidget {
  const ExpiredParcelScreen({required this.stationId, super.key});

  final String stationId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final parcels = ref.watch(expiredParcelsProvider(stationId));
    return Scaffold(
      appBar: AppBar(title: const Text('Expired parcels')),
      body: parcels.when(
        loading: () => const Center(
          child: CircularProgressIndicator(key: Key('expired-parcels-loading')),
        ),
        error: (error, _) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(
                  Icons.error_outline,
                  size: 48,
                  color: Theme.of(context).colorScheme.error,
                ),
                const SizedBox(height: 16),
                Text(
                  errorMessage(error),
                  key: const Key('expired-parcels-error'),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 16),
                FilledButton.icon(
                  onPressed: () =>
                      ref.invalidate(expiredParcelsProvider(stationId)),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Retry'),
                ),
              ],
            ),
          ),
        ),
        data: (items) => items.isEmpty
            ? const Center(
                child: Padding(
                  padding: EdgeInsets.all(24),
                  child: Text(
                    'No expired parcels at this station.',
                    key: Key('expired-parcels-empty'),
                    textAlign: TextAlign.center,
                  ),
                ),
              )
            : ListView.separated(
                padding: const EdgeInsets.all(16),
                itemCount: items.length,
                separatorBuilder: (_, _) => const SizedBox(height: 12),
                itemBuilder: (context, index) => _ExpiredParcelCard(
                  stationId: stationId,
                  parcel: items[index],
                ),
              ),
      ),
    );
  }
}

class _ExpiredParcelCard extends StatelessWidget {
  const _ExpiredParcelCard({required this.stationId, required this.parcel});

  final String stationId;
  final ExpiredParcel parcel;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final failedColor = theme.colorScheme.error;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    parcel.trackingNumber,
                    key: const Key('expired-parcel-tracking-number'),
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                DecoratedBox(
                  decoration: BoxDecoration(
                    color: failedColor.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 6,
                    ),
                    child: Text(
                      'FAILED',
                      style: theme.textTheme.labelMedium?.copyWith(
                        color: failedColor,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text('Slot ${parcel.slotNumber} · ${parcel.slotId}'),
            const SizedBox(height: 8),
            Text(
              parcel.cancelReason,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: failedColor,
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: 12),
            Text('Arrived: ${_formatDateTime(parcel.arrivedAt)}'),
            Text('Time in slot: ${_formatDuration(parcel.elapsedDuration)}'),
            const SizedBox(height: 16),
            FilledButton.icon(
              key: Key('clear-expired-parcel-${parcel.slotId}'),
              onPressed: () => _confirmClear(context, parcel, stationId),
              icon: const Icon(Icons.inventory_2_outlined),
              label: const Text('Clear parcel from slot'),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _confirmClear(
    BuildContext context,
    ExpiredParcel parcel,
    String stationId,
  ) async {
    final cleared = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) =>
          _ClearExpiredParcelSheet(stationId: stationId, parcel: parcel),
    );
    if (cleared == true && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'Parcel cleared. Slot ${parcel.slotNumber} is now EMPTY.',
          ),
        ),
      );
    }
  }
}

class _ClearExpiredParcelSheet extends ConsumerStatefulWidget {
  const _ClearExpiredParcelSheet({
    required this.stationId,
    required this.parcel,
  });

  final String stationId;
  final ExpiredParcel parcel;

  @override
  ConsumerState<_ClearExpiredParcelSheet> createState() =>
      _ClearExpiredParcelSheetState();
}

class _ClearExpiredParcelSheetState
    extends ConsumerState<_ClearExpiredParcelSheet> {
  final _formKey = GlobalKey<FormState>();
  final _reasonController = TextEditingController();

  @override
  void dispose() {
    _reasonController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final clearState = ref.watch(clearExpiredParcelNotifierProvider);
    final isLoading = clearState.isLoading;
    final error = clearState.hasError ? errorMessage(clearState.error!) : null;

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          24,
          24,
          24,
          24 + MediaQuery.of(context).viewInsets.bottom,
        ),
        child: Form(
          key: _formKey,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Clear expired parcel?',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 8),
                Text(
                  'Confirm that tracking number ${widget.parcel.trackingNumber} '
                  'has been removed from slot ${widget.parcel.slotNumber}.',
                ),
                const SizedBox(height: 16),
                TextFormField(
                  key: const Key('clear-expired-parcel-reason'),
                  controller: _reasonController,
                  enabled: !isLoading,
                  maxLines: 3,
                  autovalidateMode: AutovalidateMode.onUserInteraction,
                  decoration: const InputDecoration(
                    labelText: 'Reason for clearing',
                    alignLabelWithHint: true,
                  ),
                  validator: _validateReason,
                ),
                if (error != null) ...[
                  const SizedBox(height: 8),
                  Text(
                    error,
                    key: const Key('clear-expired-parcel-error'),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ],
                const SizedBox(height: 16),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    FilledButton(
                      key: const Key('clear-expired-parcel-submit'),
                      onPressed: isLoading ? null : _submit,
                      child: isLoading
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('Confirm clear'),
                    ),
                    TextButton(
                      key: const Key('clear-expired-parcel-cancel'),
                      onPressed: isLoading
                          ? null
                          : () => Navigator.of(context).pop(),
                      child: const Text('Cancel'),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  String? _validateReason(String? value) {
    final length = value?.trim().length ?? 0;
    if (length < 5) return 'Reason must be at least 5 characters.';
    if (length > 500) return 'Reason must be no more than 500 characters.';
    return null;
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    final request = ClearExpiredParcelRequest(
      slotId: widget.parcel.slotId,
      reason: _reasonController.text,
    );
    await ref
        .read(clearExpiredParcelNotifierProvider.notifier)
        .clearExpiredParcel(stationId: widget.stationId, request: request);

    if (!mounted) return;
    if (ref.read(clearExpiredParcelNotifierProvider).hasError) return;
    Navigator.of(context).pop(true);
  }
}

String _formatDateTime(DateTime value) {
  final local = value.toLocal();
  final month = local.month.toString().padLeft(2, '0');
  final day = local.day.toString().padLeft(2, '0');
  final hour = local.hour.toString().padLeft(2, '0');
  final minute = local.minute.toString().padLeft(2, '0');
  return '$day/$month/${local.year} $hour:$minute';
}

String _formatDuration(Duration duration) {
  final days = duration.inDays;
  final hours = duration.inHours.remainder(24);
  final minutes = duration.inMinutes.remainder(60);
  if (days > 0) return '${days}d ${hours}h ${minutes}m';
  if (duration.inHours > 0) return '${duration.inHours}h ${minutes}m';
  return '${duration.inMinutes}m';
}
