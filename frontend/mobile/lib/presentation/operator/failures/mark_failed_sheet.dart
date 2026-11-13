import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/error_message.dart';
import 'mark_failure_controller.dart';

class MarkFailedSheet extends ConsumerStatefulWidget {
  const MarkFailedSheet({super.key});

  @override
  ConsumerState<MarkFailedSheet> createState() => _MarkFailedSheetState();
}

class _MarkFailedSheetState extends ConsumerState<MarkFailedSheet> {
  final _formKey = GlobalKey<FormState>();
  final _orderIdController = TextEditingController();
  final _reasonController = TextEditingController();
  String _failureType = reportableFailureTypes.first;

  @override
  void dispose() {
    _orderIdController.dispose();
    _reasonController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final request = ref.watch(markFailureControllerProvider);
    final submitting = request.isLoading;

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          20,
          20,
          20,
          MediaQuery.viewInsetsOf(context).bottom + 20,
        ),
        child: Form(
          key: _formKey,
          child: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  'Report delivery failure',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _orderIdController,
                  enabled: !submitting,
                  decoration: const InputDecoration(
                    labelText: 'Order ID',
                    hintText: 'Enter the delivery order ID',
                  ),
                  validator: (value) => value?.trim().isNotEmpty == true
                      ? null
                      : 'Enter an order ID.',
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  initialValue: _failureType,
                  decoration: const InputDecoration(labelText: 'Failure type'),
                  items: [
                    for (final type in reportableFailureTypes)
                      DropdownMenuItem(value: type, child: Text(type)),
                  ],
                  onChanged: submitting
                      ? null
                      : (value) {
                          if (value != null) {
                            setState(() => _failureType = value);
                          }
                        },
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _reasonController,
                  enabled: !submitting,
                  maxLines: 3,
                  maxLength: 500,
                  maxLengthEnforcement: MaxLengthEnforcement.none,
                  decoration: const InputDecoration(
                    labelText: 'Failure reason',
                    alignLabelWithHint: true,
                  ),
                  validator: (value) {
                    final reason = value?.trim() ?? '';
                    if (reason.isEmpty) return 'Enter a failure reason.';
                    if (reason.length > 500) {
                      return 'Failure reason must be at most 500 characters.';
                    }
                    return null;
                  },
                ),
                if (request.hasError) ...[
                  const SizedBox(height: 4),
                  Text(
                    errorMessage(request.error!),
                    key: const Key('mark-failure-error'),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ],
                const SizedBox(height: 12),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    key: const Key('mark-failure-submit'),
                    onPressed: submitting ? null : _submit,
                    child: submitting
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Text('Mark as failed'),
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
    if (!(_formKey.currentState?.validate() ?? false)) return;
    final decision = await ref
        .read(markFailureControllerProvider.notifier)
        .reportFailure(
          orderId: _orderIdController.text,
          failureType: _failureType,
          reason: _reasonController.text,
        );
    if (decision != null && mounted) {
      Navigator.of(context).pop(true);
    }
  }
}
