import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/customer_address.dart';
import 'address_notifier.dart';

class AddEditAddressPage extends ConsumerStatefulWidget {
  const AddEditAddressPage({super.key, this.address});

  final CustomerAddress? address;

  @override
  ConsumerState<AddEditAddressPage> createState() => _AddEditAddressPageState();
}

class _AddEditAddressPageState extends ConsumerState<AddEditAddressPage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _labelController;
  late final TextEditingController _nameController;
  late final TextEditingController _phoneController;
  late final TextEditingController _streetController;
  late final TextEditingController _latitudeController;
  late final TextEditingController _longitudeController;
  late bool _isDefault;

  @override
  void initState() {
    super.initState();
    final address = widget.address;
    _labelController = TextEditingController(text: address?.addressLabel ?? '');
    _nameController = TextEditingController(text: address?.recipientName ?? '');
    _phoneController = TextEditingController(
      text: address?.recipientPhone ?? '',
    );
    _streetController = TextEditingController(
      text: address?.streetAddress ?? '',
    );
    _latitudeController = TextEditingController(
      text: address?.latitude?.toString() ?? '',
    );
    _longitudeController = TextEditingController(
      text: address?.longitude?.toString() ?? '',
    );
    _isDefault = address?.isDefault ?? false;
  }

  @override
  void dispose() {
    _labelController.dispose();
    _nameController.dispose();
    _phoneController.dispose();
    _streetController.dispose();
    _latitudeController.dispose();
    _longitudeController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isLoading = ref.watch(addressNotifierProvider).isLoading;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.address == null ? 'Add address' : 'Edit address'),
      ),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(24),
            children: [
              TextFormField(
                controller: _labelController,
                maxLength: 50,
                textCapitalization: TextCapitalization.words,
                decoration: const InputDecoration(labelText: 'Label'),
                validator: (value) =>
                    _requiredMaxLength(value, 50, fieldName: 'Label'),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _nameController,
                maxLength: 150,
                textCapitalization: TextCapitalization.words,
                decoration: const InputDecoration(labelText: 'Recipient name'),
                validator: (value) =>
                    _requiredMaxLength(value, 150, fieldName: 'Recipient name'),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _phoneController,
                keyboardType: TextInputType.phone,
                maxLength: 20,
                decoration: const InputDecoration(labelText: 'Phone'),
                validator: _validatePhone,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _streetController,
                minLines: 2,
                maxLines: 3,
                textCapitalization: TextCapitalization.sentences,
                decoration: const InputDecoration(labelText: 'Address line'),
                validator: (value) =>
                    _required(value, fieldName: 'Address line'),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _latitudeController,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                  signed: true,
                ),
                inputFormatters: [
                  FilteringTextInputFormatter.allow(RegExp(r'^-?\d*\.?\d*')),
                ],
                decoration: const InputDecoration(labelText: 'Latitude'),
                validator: (value) => _validateCoordinate(
                  value,
                  otherValue: _longitudeController.text,
                  minimum: -90,
                  maximum: 90,
                  label: 'Latitude',
                  otherLabel: 'Longitude',
                ),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _longitudeController,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                  signed: true,
                ),
                inputFormatters: [
                  FilteringTextInputFormatter.allow(RegExp(r'^-?\d*\.?\d*')),
                ],
                decoration: const InputDecoration(labelText: 'Longitude'),
                validator: (value) => _validateCoordinate(
                  value,
                  otherValue: _latitudeController.text,
                  minimum: -180,
                  maximum: 180,
                  label: 'Longitude',
                  otherLabel: 'Latitude',
                ),
              ),
              const SizedBox(height: 8),
              CheckboxListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Set as default address'),
                value: _isDefault,
                onChanged: (value) =>
                    setState(() => _isDefault = value ?? false),
                controlAffinity: ListTileControlAffinity.leading,
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: isLoading ? null : _submit,
                child: isLoading
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(
                        widget.address == null
                            ? 'Save address'
                            : 'Save changes',
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  String? _requiredMaxLength(
    String? value,
    int maxLength, {
    required String fieldName,
  }) {
    final text = value?.trim() ?? '';
    if (text.isEmpty) return '$fieldName is required.';
    if (text.length > maxLength) {
      return '$fieldName must be at most $maxLength characters.';
    }
    return null;
  }

  String? _required(String? value, {required String fieldName}) =>
      value?.trim().isNotEmpty == true ? null : '$fieldName is required.';

  String? _validatePhone(String? value) {
    final phone = value?.trim() ?? '';
    if (phone.isEmpty) return 'Phone is required.';
    if (!RegExp(r'^\d{9,11}$').hasMatch(phone)) {
      return 'Phone number must contain 9 to 11 digits.';
    }
    return null;
  }

  String? _validateCoordinate(
    String? value, {
    required String otherValue,
    required double minimum,
    required double maximum,
    required String label,
    required String otherLabel,
  }) {
    final text = value?.trim() ?? '';
    final otherText = otherValue.trim();
    if (text.isEmpty && otherText.isEmpty) return null;
    if (text.isEmpty) return '$label is required when $otherLabel is provided.';

    final coordinate = double.tryParse(text);
    if (coordinate == null ||
        !coordinate.isFinite ||
        coordinate < minimum ||
        coordinate > maximum) {
      return '$label must be between $minimum and $maximum.';
    }
    return null;
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    final request = CustomerAddressRequest(
      addressLabel: _labelController.text.trim(),
      recipientName: _nameController.text.trim(),
      recipientPhone: _phoneController.text.trim(),
      streetAddress: _streetController.text.trim(),
      latitude: _coordinateOrNull(_latitudeController.text),
      longitude: _coordinateOrNull(_longitudeController.text),
      isDefault: _isDefault,
    );
    final notifier = ref.read(addressNotifierProvider.notifier);
    if (widget.address == null) {
      await notifier.createAddress(request);
    } else {
      await notifier.updateAddress(widget.address!.id, request);
    }

    if (!mounted) return;
    final result = ref.read(addressNotifierProvider);
    if (result.hasError) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(errorMessage(result.error!))));
      return;
    }
    Navigator.of(context).pop(true);
  }

  double? _coordinateOrNull(String value) =>
      value.trim().isEmpty ? null : double.parse(value.trim());
}
