import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'cancel_order_controller.dart';

class CancelOrderSheet extends ConsumerStatefulWidget {
  const CancelOrderSheet({required this.orderId, super.key});

  final String orderId;

  @override
  ConsumerState<CancelOrderSheet> createState() => _CancelOrderSheetState();
}

class _CancelOrderSheetState extends ConsumerState<CancelOrderSheet> {
  final _formKey = GlobalKey<FormState>();
  final _reasonController = TextEditingController();

  @override
  void dispose() {
    _reasonController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isLoading = ref.watch(cancelOrderControllerProvider).isLoading;
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          24,
          24,
          24,
          24 + MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: Form(
          key: _formKey,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Cancel this order?',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _reasonController,
                  maxLength: 500,
                  maxLines: 3,
                  enabled: !isLoading,
                  decoration: const InputDecoration(
                    labelText: 'Cancellation reason',
                    alignLabelWithHint: true,
                  ),
                  validator: (value) => value == null || value.trim().isEmpty
                      ? 'Enter a cancellation reason.'
                      : null,
                ),
                const SizedBox(height: 8),
                Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    TextButton(
                      onPressed: isLoading
                          ? null
                          : () => Navigator.of(context).pop(),
                      child: const Text('Keep order'),
                    ),
                    const SizedBox(width: 8),
                    FilledButton(
                      onPressed: isLoading ? null : _submit,
                      child: isLoading
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('Confirm cancellation'),
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

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    ref
        .read(cancelOrderControllerProvider.notifier)
        .cancelOrder(widget.orderId, _reasonController.text);
  }
}
