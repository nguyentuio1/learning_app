import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/error_message.dart';
import '../../../data/models/package_model.dart';
import '../../../data/providers/package_providers.dart';
import 'package_notifier.dart';

class EditPackageRoute extends ConsumerWidget {
  const EditPackageRoute({super.key, required this.packageId});

  final String packageId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (packageId.isEmpty) {
      return const Scaffold(body: Center(child: Text('Package not found.')));
    }

    return ref
        .watch(packageByIdProvider(packageId))
        .when(
          data: (package) => AddEditPackagePage(package: package),
          loading: () => Scaffold(
            appBar: AppBar(title: const Text('Edit package')),
            body: const Center(child: CircularProgressIndicator()),
          ),
          error: (error, _) => Scaffold(
            appBar: AppBar(title: const Text('Edit package')),
            body: Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(errorMessage(error)),
                  TextButton(
                    onPressed: () =>
                        ref.invalidate(packageByIdProvider(packageId)),
                    child: const Text('Retry'),
                  ),
                ],
              ),
            ),
          ),
        );
  }
}

class AddEditPackagePage extends ConsumerStatefulWidget {
  const AddEditPackagePage({super.key, this.package});

  final Package? package;

  @override
  ConsumerState<AddEditPackagePage> createState() => _AddEditPackagePageState();
}

class _AddEditPackagePageState extends ConsumerState<AddEditPackagePage> {
  static const _capacityMessage =
      "This package exceeds the selected drone's capacity.";
  static const _positiveMessage =
      'Weight and dimensions must be greater than zero.';

  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _weightController;
  late final TextEditingController _lengthController;
  late final TextEditingController _widthController;
  late final TextEditingController _heightController;
  late final TextEditingController _imageUrlController;
  late bool _isFragile;

  @override
  void initState() {
    super.initState();
    final package = widget.package;
    _nameController = TextEditingController(text: package?.packageName ?? '');
    _weightController = TextEditingController(
      text: package?.weightGram.toString() ?? '',
    );
    _lengthController = TextEditingController(
      text: package?.lengthCm.toString() ?? '',
    );
    _widthController = TextEditingController(
      text: package?.widthCm.toString() ?? '',
    );
    _heightController = TextEditingController(
      text: package?.heightCm.toString() ?? '',
    );
    _imageUrlController = TextEditingController(
      text: package?.packageImageUrl ?? '',
    );
    _isFragile = package?.isFragile ?? false;
  }

  @override
  void dispose() {
    _nameController.dispose();
    _weightController.dispose();
    _lengthController.dispose();
    _widthController.dispose();
    _heightController.dispose();
    _imageUrlController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isLoading = ref.watch(packageNotifierProvider).isLoading;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.package == null ? 'Add package' : 'Edit package'),
      ),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(24),
            children: [
              TextFormField(
                controller: _nameController,
                maxLength: 200,
                textCapitalization: TextCapitalization.sentences,
                decoration: const InputDecoration(labelText: 'Package name'),
                validator: _validateName,
              ),
              const SizedBox(height: 12),
              _numberField(
                controller: _weightController,
                label: 'Weight (g)',
                maximum: 3000,
              ),
              const SizedBox(height: 12),
              _numberField(
                controller: _lengthController,
                label: 'Length (cm)',
                maximum: 30,
              ),
              const SizedBox(height: 12),
              _numberField(
                controller: _widthController,
                label: 'Width (cm)',
                maximum: 20,
              ),
              const SizedBox(height: 12),
              _numberField(
                controller: _heightController,
                label: 'Height (cm)',
                maximum: 15,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _imageUrlController,
                keyboardType: TextInputType.url,
                decoration: const InputDecoration(
                  labelText: 'Package image URL (optional)',
                ),
                validator: _validateImageUrl,
              ),
              const SizedBox(height: 8),
              CheckboxListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Fragile'),
                value: _isFragile,
                onChanged: (value) =>
                    setState(() => _isFragile = value ?? false),
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
                        widget.package == null
                            ? 'Save package'
                            : 'Save changes',
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  TextFormField _numberField({
    required TextEditingController controller,
    required String label,
    required double maximum,
  }) => TextFormField(
    controller: controller,
    keyboardType: const TextInputType.numberWithOptions(decimal: true),
    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.-]'))],
    decoration: InputDecoration(labelText: label),
    validator: (value) => _validateMeasurement(value, maximum),
  );

  String? _validateName(String? value) {
    final name = value?.trim() ?? '';
    if (name.isEmpty) return 'Package name is required.';
    if (name.length > 200) {
      return 'Package name must be at most 200 characters.';
    }
    return null;
  }

  String? _validateMeasurement(String? value, double maximum) {
    final text = value?.trim() ?? '';
    final measurement = double.tryParse(text);
    if (measurement == null || !measurement.isFinite || measurement <= 0) {
      return _positiveMessage;
    }
    if (measurement < 0.01) {
      return 'Weight and dimensions must be at least 0.01.';
    }
    final decimals = text.split('.');
    if (decimals.length > 1 && decimals.last.length > 2) {
      return 'Use at most 2 decimal places.';
    }
    if (measurement > maximum) return _capacityMessage;
    return null;
  }

  String? _validateImageUrl(String? value) {
    final text = value?.trim() ?? '';
    if (text.isEmpty) return null;
    final uri = Uri.tryParse(text);
    if (uri == null ||
        !uri.hasScheme ||
        !const {'http', 'https'}.contains(uri.scheme) ||
        uri.host.isEmpty) {
      return 'Enter a valid public image URL.';
    }
    return null;
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    final weight = double.tryParse(_weightController.text.trim());
    final length = double.tryParse(_lengthController.text.trim());
    final width = double.tryParse(_widthController.text.trim());
    final height = double.tryParse(_heightController.text.trim());
    if (weight == null || length == null || width == null || height == null) {
      throw StateError('Validated package measurements must be parseable.');
    }

    final request = PackageRequest(
      packageName: _nameController.text.trim(),
      weightGram: weight,
      lengthCm: length,
      widthCm: width,
      heightCm: height,
      packageImageUrl: _imageUrlController.text.trim().isEmpty
          ? null
          : _imageUrlController.text.trim(),
      isFragile: _isFragile,
    );
    final notifier = ref.read(packageNotifierProvider.notifier);
    if (widget.package == null) {
      await notifier.createPackage(request);
    } else {
      await notifier.updatePackage(widget.package!.id, request);
    }

    if (!mounted) return;
    final result = ref.read(packageNotifierProvider);
    if (result.hasError) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(errorMessage(result.error!))));
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Package saved successfully.')),
    );
    Navigator.of(context).pop(true);
  }
}
