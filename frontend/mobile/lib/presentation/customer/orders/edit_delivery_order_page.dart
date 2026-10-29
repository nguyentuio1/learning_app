import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/models/delivery_order.dart';
import '../../../data/models/update_delivery_order_dto.dart';
import 'order_messages.dart';
import 'order_notifier.dart';

class EditDeliveryOrderPage extends ConsumerStatefulWidget {
  const EditDeliveryOrderPage({required this.order, super.key});

  final DeliveryOrder order;

  @override
  ConsumerState<EditDeliveryOrderPage> createState() =>
      _EditDeliveryOrderPageState();
}

class _EditDeliveryOrderPageState extends ConsumerState<EditDeliveryOrderPage> {
  static final _guidPattern = RegExp(
    r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
  );
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _phoneController;
  late final TextEditingController _destinationController;
  late final TextEditingController _addressController;
  bool _submitting = false;
  UpdateDeliveryOrderDto? _submittedUpdate;

  @override
  void initState() {
    super.initState();
    _nameController = TextEditingController(text: widget.order.recipientName);
    _phoneController = TextEditingController(text: widget.order.recipientPhone);
    _destinationController = TextEditingController(
      text: widget.order.destinationStationId,
    );
    _addressController = TextEditingController(
      text: widget.order.deliveryAddressId ?? '',
    );
  }

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    _destinationController.dispose();
    _addressController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (widget.order.status != 'PENDING') {
      return Scaffold(
        appBar: AppBar(title: Text('Edit Order')),
        body: Center(child: Text('Only pending orders can be edited.')),
      );
    }

    ref.listen(orderNotifierProvider, (previous, next) {
      if (!_submitting) return;
      if (next.hasError) {
        _submitting = false;
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              orderOperationErrorMessage(next.error!, cancelling: false),
            ),
          ),
        );
      } else if (previous?.isLoading == true && next.hasValue) {
        _submitting = false;
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Order updated successfully.')),
        );
        if (context.canPop()) context.pop(_submittedUpdate);
      }
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Edit Order')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            TextFormField(
              controller: _nameController,
              maxLength: 150,
              textCapitalization: TextCapitalization.words,
              decoration: const InputDecoration(labelText: 'Recipient name'),
              validator: _validateName,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _phoneController,
              keyboardType: TextInputType.phone,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: const InputDecoration(labelText: 'Recipient phone'),
              validator: _validatePhone,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _destinationController,
              decoration: const InputDecoration(
                labelText: 'Destination station ID',
              ),
              validator: _validateDestination,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _addressController,
              decoration: const InputDecoration(
                labelText: 'Delivery address ID (optional)',
              ),
              validator: _validateAddress,
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: _submitting ? null : _submit,
              child: _submitting
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Save changes'),
            ),
          ],
        ),
      ),
    );
  }

  String? _validateName(String? value) {
    final name = value?.trim() ?? '';
    if (name.isEmpty) return 'Recipient name is required.';
    if (name.length > 150) {
      return 'Recipient name must be at most 150 characters.';
    }
    return null;
  }

  String? _validatePhone(String? value) {
    final phone = value?.trim() ?? '';
    if (!RegExp(r'^\d{9,11}$').hasMatch(phone)) {
      return 'Phone number must be 9 to 11 digits.';
    }
    return null;
  }

  String? _validateDestination(String? value) {
    final destination = value?.trim() ?? '';
    if (!_guidPattern.hasMatch(destination)) {
      return 'Enter a valid destination station ID.';
    }
    if (destination.toLowerCase() ==
        widget.order.originStationId.toLowerCase()) {
      return 'Destination station must differ from origin station.';
    }
    return null;
  }

  String? _validateAddress(String? value) {
    final address = value?.trim() ?? '';
    if (address.isNotEmpty && !_guidPattern.hasMatch(address)) {
      return 'Enter a valid delivery address ID.';
    }
    return null;
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    final update = UpdateDeliveryOrderDto(
      recipientName: _nameController.text.trim(),
      recipientPhone: _phoneController.text.trim(),
      destinationStationId: _destinationController.text.trim(),
      deliveryAddressId: _addressController.text.trim().isEmpty
          ? null
          : _addressController.text.trim(),
    );
    _submittedUpdate = update;
    setState(() => _submitting = true);
    await ref
        .read(orderNotifierProvider.notifier)
        .updateOrder(
          widget.order.id,
          update,
          originStationId: widget.order.originStationId,
          currentStatus: widget.order.status,
        );
  }
}
