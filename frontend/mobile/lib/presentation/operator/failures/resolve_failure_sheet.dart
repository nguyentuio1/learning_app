import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter/services.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/failure_models.dart';
import 'resolve_failure_controller.dart';

class ResolveFailureSheet extends ConsumerStatefulWidget {
  const ResolveFailureSheet({required this.detail, super.key});

  final FailureOrderDetail detail;

  @override
  ConsumerState<ResolveFailureSheet> createState() =>
      _ResolveFailureSheetState();
}

class _ResolveFailureSheetState extends ConsumerState<ResolveFailureSheet> {
  final _formKey = GlobalKey<FormState>();
  final _noteController = TextEditingController();
  String _action = 'RESCHEDULE';
  bool _packageVerified = false;
  bool _attemptedSubmit = false;

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final request = ref.watch(resolveFailureControllerProvider);
    final submitting = request.isLoading;
    final insets = MediaQuery.viewInsetsOf(context);

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(20, 20, 20, insets.bottom + 20),
        child: Form(
          key: _formKey,
          child: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  'Resolve delivery failure',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 12),
                _FailureSummaryCard(
                  failure: widget.detail.latestFailure,
                  location: widget.detail.parcelLocation,
                ),
                const SizedBox(height: 16),
                Text(
                  'Resolution',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                RadioGroup<String>(
                  groupValue: _action,
                  onChanged: (value) {
                    if (submitting || value == null) return;
                    setState(() {
                      _action = value;
                      _attemptedSubmit = false;
                    });
                  },
                  child: Column(
                    children: [
                      for (final option in _resolutionOptions)
                        RadioListTile<String>(
                          value: option.$1,
                          title: Text(option.$2),
                          subtitle: Text(option.$3),
                          contentPadding: EdgeInsets.zero,
                          dense: true,
                        ),
                    ],
                  ),
                ),
                if (_action == 'RESCHEDULE') ...[
                  CheckboxListTile(
                    contentPadding: EdgeInsets.zero,
                    value: _packageVerified,
                    onChanged: submitting
                        ? null
                        : (value) =>
                              setState(() => _packageVerified = value ?? false),
                    title: const Text(
                      'Kiện hàng đã được kiểm tra nguyên vẹn tại trạm',
                    ),
                    controlAffinity: ListTileControlAffinity.leading,
                  ),
                  if (_attemptedSubmit && !_packageVerified)
                    Padding(
                      padding: const EdgeInsets.only(left: 12),
                      child: Text(
                        'Verify the package before rescheduling.',
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                    ),
                ],
                const SizedBox(height: 8),
                TextFormField(
                  controller: _noteController,
                  enabled: !submitting,
                  maxLines: 3,
                  maxLength: 500,
                  maxLengthEnforcement: MaxLengthEnforcement.none,
                  decoration: const InputDecoration(
                    labelText: 'Resolution note',
                    hintText: 'Add a note for the order history',
                    alignLabelWithHint: true,
                  ),
                  validator: (value) {
                    final note = value?.trim() ?? '';
                    if (note.isEmpty) return 'Enter a resolution note.';
                    if (note.length > 500) {
                      return 'Resolution note must be at most 500 characters.';
                    }
                    return null;
                  },
                ),
                if (request.hasError) ...[
                  const SizedBox(height: 4),
                  Text(
                    errorMessage(request.error!),
                    key: const Key('resolve-failure-error'),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ],
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    key: const Key('resolve-failure-submit'),
                    onPressed: submitting ? null : _submit,
                    child: submitting
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Text('Submit resolution'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _submit() async {
    setState(() => _attemptedSubmit = true);
    final valid = _formKey.currentState?.validate() ?? false;
    if (!valid || (_action == 'RESCHEDULE' && !_packageVerified)) return;

    final decision = await ref
        .read(resolveFailureControllerProvider.notifier)
        .resolveFailure(
          orderId: widget.detail.orderId,
          action: _action,
          note: _noteController.text,
          packageVerified: _action == 'RESCHEDULE' && _packageVerified,
        );
    if (decision != null && mounted) {
      Navigator.of(context).pop(_action);
    }
  }
}

class _FailureSummaryCard extends StatelessWidget {
  const _FailureSummaryCard({required this.failure, required this.location});

  final FailureEpisode failure;
  final ParcelLocation? location;

  @override
  Widget build(BuildContext context) => Card(
    color: Theme.of(context).colorScheme.errorContainer.withValues(alpha: 0.5),
    child: Padding(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Failure summary · ${failure.failureType}',
            style: Theme.of(context).textTheme.titleSmall,
          ),
          const SizedBox(height: 6),
          Text(failure.failureReason ?? 'No failure reason provided.'),
          const SizedBox(height: 6),
          Text('Failed: ${_formatDate(failure.failedAt)}'),
          Text(
            'Parcel location: ${location?.stationId ?? 'Unknown station'}'
            '${location?.slotId == null ? '' : ' · Slot ${location!.slotId}'}',
          ),
        ],
      ),
    ),
  );
}

const _resolutionOptions = [
  (
    'RESCHEDULE',
    'Xếp lịch giao lại',
    'Order returns to APPROVED for scheduling.',
  ),
  (
    'RETURN_TO_ORIGIN',
    'Hoàn hàng về trạm đi',
    'Awaiting receipt at the origin station.',
  ),
  ('CANCEL', 'Hủy đơn hàng', 'Cancel this order.'),
];

String _formatDate(DateTime value) =>
    value.toLocal().toString().split('.').first;
