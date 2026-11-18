import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/error/error_message.dart';
import '../../../core/theme/app_colors.dart';
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

  final PackageModel? package;

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
  String? _savedPackageId;
  bool _uploadingPhoto = false;

  @override
  void initState() {
    super.initState();
    final package = widget.package;
    _savedPackageId = package?.id;
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
    final isLoading =
        ref.watch(packageNotifierProvider).isLoading || _uploadingPhoto;

    return Scaffold(
      appBar: AppBar(
        toolbarHeight: 84,
        leading: Padding(
          padding: const EdgeInsets.only(left: 8),
          child: Center(
            child: SizedBox(
              width: 40,
              height: 40,
              child: Material(
                color: AppColors.customerSurface,
                borderRadius: BorderRadius.circular(12),
                child: InkWell(
                  borderRadius: BorderRadius.circular(12),
                  onTap: () => Navigator.of(context).maybePop(),
                  child: Container(
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: AppColors.customerBorder),
                    ),
                    child: const Icon(Icons.arrow_back, size: 20),
                  ),
                ),
              ),
            ),
          ),
        ),
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              widget.package == null ? 'Add Package' : 'Edit Package',
              style: Theme.of(context).textTheme.titleLarge
                  ?.copyWith(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 2),
            Text(
              'Enter your package details.',
              style: Theme.of(context).textTheme.bodySmall
                  ?.copyWith(color: AppColors.customerTextSecondary),
            ),
          ],
        ),
      ),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
            children: [
              _labeledField(
                'Package name',
                TextFormField(
                  controller: _nameController,
                  inputFormatters: [LengthLimitingTextInputFormatter(200)],
                  textCapitalization: TextCapitalization.sentences,
                  decoration: const InputDecoration(),
                  validator: _validateName,
                ),
              ),
              const SizedBox(height: 12),
              _labeledField(
                'Weight (g)',
                _numberField(controller: _weightController, maximum: 3000),
              ),
              const SizedBox(height: 12),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: _labeledField(
                      'Length (cm)',
                      _numberField(controller: _lengthController, maximum: 30),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: _labeledField(
                      'Width (cm)',
                      _numberField(controller: _widthController, maximum: 20),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: _labeledField(
                      'Height (cm)',
                      _numberField(controller: _heightController, maximum: 15),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              if (_imageUrlController.text.isNotEmpty) ...[
                Image.network(
                  _imageUrlController.text,
                  key: const Key('package-photo-preview'),
                  height: 150,
                  fit: BoxFit.contain,
                  errorBuilder: (_, _, _) => const Text(
                    'Photo could not load. Check your connection or replace it.',
                  ),
                ),
                const SizedBox(height: 12),
              ],
              OutlinedButton.icon(
                key: const Key('package-upload-photo'),
                onPressed: isLoading ? null : _uploadPhoto,
                icon: _uploadingPhoto
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.photo_library_outlined),
                label: Text(
                  _uploadingPhoto
                      ? 'Uploading photo...'
                      : 'Upload package photo',
                ),
              ),
              const Text(
                'JPEG, PNG or WebP, up to 5 MB. Uploading saves a new package first.',
              ),
              const SizedBox(height: 8),
              InkWell(
                onTap: isLoading ? null : _editImageUrl,
                borderRadius: BorderRadius.circular(12),
                child: CustomPaint(
                  foregroundPainter: const _DashedBorderPainter(
                    color: AppColors.customerSeed,
                    radius: 12,
                  ),
                  child: Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: const Color(0xFFEFF6FF),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Row(
                      children: [
                        const Icon(
                          Icons.camera_alt_outlined,
                          color: AppColors.customerSeed,
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Text(
                            _imageUrlController.text.isEmpty
                                ? 'Add package photo (optional)'
                                : _imageUrlController.text,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: AppColors.customerSeed,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                        const Icon(
                          Icons.add_circle_outline,
                          color: AppColors.customerSeed,
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              CheckboxListTile(
                contentPadding: const EdgeInsets.symmetric(horizontal: 4),
                value: _isFragile,
                onChanged: (value) =>
                    setState(() => _isFragile = value ?? false),
                controlAffinity: ListTileControlAffinity.leading,
                title: const Text(
                  'Fragile package',
                  style: TextStyle(fontWeight: FontWeight.w700),
                ),
                subtitle: const Text('Handle with extra care.'),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                  side: const BorderSide(color: AppColors.customerBorder),
                ),
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
                            ? 'Save Package'
                            : 'Save changes',
                      ),
              ),
              TextButton(
                onPressed: isLoading
                    ? null
                    : () => Navigator.of(context).maybePop(),
                child: const Text('Cancel'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  TextFormField _numberField({
    required TextEditingController controller,
    required double maximum,
  }) => TextFormField(
    controller: controller,
    keyboardType: const TextInputType.numberWithOptions(decimal: true),
    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.-]'))],
    decoration: const InputDecoration(),
    validator: (value) => _validateMeasurement(value, maximum),
  );

  Widget _labeledField(String label, Widget field) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Padding(
        padding: const EdgeInsets.only(bottom: 8, left: 2),
        child: Text(label, style: const TextStyle(fontWeight: FontWeight.w600)),
      ),
      field,
    ],
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

  Future<void> _editImageUrl() async {
    final result = await showDialog<String>(
      context: context,
      builder: (_) => _PackageImageUrlDialog(
        initialValue: _imageUrlController.text,
        validator: _validateImageUrl,
      ),
    );
    if (result != null && mounted) {
      setState(() => _imageUrlController.text = result);
    }
  }

  Future<void> _submit() async {
    if (_uploadingPhoto) return;
    if (!_formKey.currentState!.validate()) return;
    final imageUrlError = _validateImageUrl(_imageUrlController.text);
    if (imageUrlError != null) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(imageUrlError)));
      return;
    }

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
    if (_savedPackageId == null) {
      await notifier.createPackage(request);
    } else {
      await notifier.updatePackage(_savedPackageId!, request);
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

  Future<void> _uploadPhoto() async {
    if (_uploadingPhoto || !_formKey.currentState!.validate()) return;
    setState(() => _uploadingPhoto = true);
    try {
      final selected = await ref
          .read(packageImagePickerProvider)
          .pickImage(source: ImageSource.gallery);
      if (!mounted || selected == null) return;
      if (await selected.length() > 5 * 1024 * 1024) {
        throw const FormatException(
          'Photo exceeds 5 MB. Choose a smaller image.',
        );
      }
      final bytes = await selected.readAsBytes();
      if (!mounted) return;
      final service = ref.read(packageApiServiceProvider);
      if (_savedPackageId == null) {
        final record = await service.createPackageRecord(
          PackageRequest(
            packageName: _nameController.text.trim(),
            weightGram: double.parse(_weightController.text.trim()),
            lengthCm: double.parse(_lengthController.text.trim()),
            widthCm: double.parse(_widthController.text.trim()),
            heightCm: double.parse(_heightController.text.trim()),
            isFragile: _isFragile,
            packageImageUrl: _imageUrlController.text.isEmpty
                ? null
                : _imageUrlController.text,
          ),
        );
        if (!mounted) return;
        _savedPackageId = record.id;
      }
      final imageUrl = await service.uploadImage(
        _savedPackageId!,
        bytes: bytes,
        filename: selected.name,
      );
      if (!mounted) return;
      setState(() => _imageUrlController.text = imageUrl);
      await ref.read(packageNotifierProvider.notifier).refresh();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Photo uploaded successfully.')),
      );
    } catch (error) {
      if (!mounted) return;
      final prefix = widget.package == null && _savedPackageId != null
          ? 'Package saved; photo upload failed. Retry without creating another package. '
          : '';
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text('$prefix${errorMessage(error)}')));
    } finally {
      if (mounted) setState(() => _uploadingPhoto = false);
    }
  }
}

class _DashedBorderPainter extends CustomPainter {
  const _DashedBorderPainter({required this.color, required this.radius});

  final Color color;
  final double radius;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1;
    final path = Path()
      ..addRRect(
        RRect.fromRectAndRadius(Offset.zero & size, Radius.circular(radius)),
      );
    for (final metric in path.computeMetrics()) {
      var distance = 0.0;
      while (distance < metric.length) {
        final next = (distance + 6).clamp(0, metric.length).toDouble();
        canvas.drawPath(metric.extractPath(distance, next), paint);
        distance = next + 4;
      }
    }
  }

  @override
  bool shouldRepaint(_DashedBorderPainter oldDelegate) =>
      oldDelegate.color != color || oldDelegate.radius != radius;
}

class _PackageImageUrlDialog extends StatefulWidget {
  const _PackageImageUrlDialog({
    required this.initialValue,
    required this.validator,
  });
  final String initialValue;
  final String? Function(String?) validator;
  @override
  State<_PackageImageUrlDialog> createState() => _PackageImageUrlDialogState();
}

class _PackageImageUrlDialogState extends State<_PackageImageUrlDialog> {
  late final controller = TextEditingController(text: widget.initialValue);
  final formKey = GlobalKey<FormState>();
  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Package photo'),
    content: Form(
      key: formKey,
      child: TextFormField(
        controller: controller,
        keyboardType: TextInputType.url,
        decoration: const InputDecoration(
          labelText: 'Image URL (optional)',
          hintText: 'https://',
        ),
        validator: widget.validator,
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Cancel'),
      ),
      FilledButton(
        onPressed: () {
          if (formKey.currentState!.validate()) {
            Navigator.pop(context, controller.text.trim());
          }
        },
        child: const Text('Save'),
      ),
    ],
  );
}
