import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../core/error/api_exception.dart';
import '../../core/error/error_message.dart';
import 'profile_controller.dart';
import 'widgets/avatar_widget.dart';

class EditProfilePage extends ConsumerStatefulWidget {
  const EditProfilePage({super.key});

  @override
  ConsumerState<EditProfilePage> createState() => _EditProfilePageState();
}

class _EditProfilePageState extends ConsumerState<EditProfilePage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _fullNameController;
  late final TextEditingController _phoneController;
  late final TextEditingController _emailController;
  final ImagePicker _picker = ImagePicker();
  String? _phoneError;
  bool _initialized = false;

  @override
  void initState() {
    super.initState();
    _fullNameController = TextEditingController();
    _phoneController = TextEditingController();
    _emailController = TextEditingController();

    _phoneController.addListener(() {
      if (_phoneError != null) {
        setState(() => _phoneError = null);
      }
    });
  }

  @override
  void dispose() {
    _fullNameController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  void _populateFields() {
    if (_initialized) return;
    final user = ref.read(profileControllerProvider).value;
    if (user != null) {
      _fullNameController.text = user.fullName;
      _phoneController.text = user.phoneNumber;
      _emailController.text = user.email;
      _initialized = true;
    }
  }

  Future<void> _submit() async {
    setState(() => _phoneError = null);
    if (!_formKey.currentState!.validate()) return;
    FocusScope.of(context).unfocus();

    await ref.read(updateProfileControllerProvider.notifier).updateProfile(
          fullName: _fullNameController.text.trim(),
          phoneNumber: _phoneController.text.trim(),
        );

    final updateState = ref.read(updateProfileControllerProvider);
    if (!updateState.hasError && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Profile updated successfully'),
          backgroundColor: Colors.green,
        ),
      );
      Navigator.of(context).pop();
    }
  }

  Future<void> _pickAndUploadAvatar() async {
    final theme = Theme.of(context);
    try {
      final pickedFile = await _picker.pickImage(
        source: ImageSource.gallery,
      );
      if (pickedFile == null || !mounted) return;

      // 1. Kiểm tra định dạng: Chỉ chấp nhận JPEG hoặc PNG
      final ext = pickedFile.name.split('.').last.toLowerCase();
      final mime = pickedFile.mimeType?.toLowerCase();
      final isValidFormat = ext == 'jpg' ||
          ext == 'jpeg' ||
          ext == 'png' ||
          mime == 'image/jpeg' ||
          mime == 'image/png';

      if (!isValidFormat) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: const Text('Chỉ chấp nhận định dạng ảnh JPEG hoặc PNG.'),
            backgroundColor: theme.colorScheme.error,
          ),
        );
        return;
      }

      // 2. Kiểm tra dung lượng: Tối đa 5MB (5 * 1024 * 1024 bytes)
      final bytes = await pickedFile.readAsBytes();
      if (bytes.length > 5 * 1024 * 1024) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: const Text('Kích thước ảnh tối đa là 5MB.'),
            backgroundColor: theme.colorScheme.error,
          ),
        );
        return;
      }

      // 3. Gọi UploadAvatarController upload lên server
      final avatarUrl = await ref
          .read(uploadAvatarControllerProvider.notifier)
          .uploadAvatarBytes(
            bytes: bytes,
            filename: pickedFile.name,
          );

      if (!mounted) return;

      final uploadState = ref.read(uploadAvatarControllerProvider);
      if (uploadState.hasError) {
        final err = uploadState.error;
        String msg = errorMessage(err!);
        if (err is ApiException && err.statusCode == 400) {
          msg = err.message.isNotEmpty
              ? err.message
              : 'File ảnh không hợp lệ hoặc vượt quá 5MB.';
        }
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(msg),
            backgroundColor: theme.colorScheme.error,
          ),
        );
      } else if (avatarUrl != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Cập nhật ảnh đại diện thành công'),
            backgroundColor: Colors.green,
          ),
        );
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(errorMessage(e)),
          backgroundColor: theme.colorScheme.error,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    _populateFields();
    final user = ref.watch(profileControllerProvider).value;
    final isSaving = ref.watch(updateProfileControllerProvider).isLoading;
    final isUploadingAvatar =
        ref.watch(uploadAvatarControllerProvider).isLoading;
    final theme = Theme.of(context);

    ref.listen<AsyncValue<void>>(updateProfileControllerProvider, (_, next) {
      if (next.hasError && !next.isLoading) {
        final err = next.error;
        String msg = errorMessage(err!);
        if (err is ApiException && err.statusCode == 409) {
          msg = 'Số điện thoại này đã được sử dụng bởi tài khoản khác.';
          setState(() {
            _phoneError = 'Số điện thoại đã được đăng ký';
          });
        }
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(msg),
            backgroundColor: theme.colorScheme.error,
          ),
        );
      }
    });

    return Scaffold(
      appBar: AppBar(
        title: const Text('Edit Profile'),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 500),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // Avatar with edit button and upload progress
                    Center(
                      child: Stack(
                        alignment: Alignment.center,
                        children: [
                          AvatarWidget(
                            avatarUrl: user?.avatarUrl,
                            name: _fullNameController.text.isNotEmpty
                                ? _fullNameController.text
                                : (user?.fullName ?? 'U'),
                            radius: 50,
                            showEditBadge: !isUploadingAvatar,
                            onTap: isUploadingAvatar ? null : _pickAndUploadAvatar,
                          ),
                          if (isUploadingAvatar)
                            Container(
                              width: 100,
                              height: 100,
                              decoration: BoxDecoration(
                                color: Colors.black.withOpacity(0.4),
                                shape: BoxShape.circle,
                              ),
                              child: const Center(
                                child: CircularProgressIndicator(
                                  strokeWidth: 3,
                                  color: Colors.white,
                                ),
                              ),
                            ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 8),
                    Center(
                      child: TextButton.icon(
                        icon: isUploadingAvatar
                            ? const SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.photo_camera_outlined, size: 18),
                        label: Text(
                          isUploadingAvatar ? 'Đang tải lên...' : 'Change photo',
                        ),
                        onPressed: isUploadingAvatar ? null : _pickAndUploadAvatar,
                      ),
                    ),
                    const SizedBox(height: 24),

                    // Full Name Field
                    TextFormField(
                      controller: _fullNameController,
                      decoration: const InputDecoration(
                        labelText: 'Full Name *',
                        prefixIcon: Icon(Icons.person_outline),
                        border: OutlineInputBorder(),
                      ),
                      validator: (value) {
                        final val = value?.trim();
                        if (val == null || val.isEmpty) {
                          return 'Full name is required';
                        }
                        if (val.length > 150) {
                          return 'Full name must be at most 150 characters';
                        }
                        return null;
                      },
                      textInputAction: TextInputAction.next,
                    ),
                    const SizedBox(height: 16),

                    // Phone Number Field
                    TextFormField(
                      controller: _phoneController,
                      decoration: InputDecoration(
                        labelText: 'Phone Number *',
                        prefixIcon: const Icon(Icons.phone_outlined),
                        border: const OutlineInputBorder(),
                        helperText: '9 to 11 digits',
                        errorText: _phoneError,
                      ),
                      keyboardType: TextInputType.phone,
                      validator: (value) {
                        final val = value?.trim();
                        if (val == null || val.isEmpty) {
                          return 'Phone number is required';
                        }
                        final digitsOnly = val.replaceAll(RegExp(r'\D'), '');
                        if (digitsOnly.length < 9 || digitsOnly.length > 11) {
                          return 'Phone number must be 9 to 11 digits';
                        }
                        return null;
                      },
                      textInputAction: TextInputAction.next,
                    ),
                    const SizedBox(height: 16),

                    // Email Field (Read-only)
                    TextFormField(
                      controller: _emailController,
                      readOnly: true,
                      enabled: false,
                      decoration: const InputDecoration(
                        labelText: 'Email Address',
                        prefixIcon: Icon(Icons.email_outlined),
                        suffixIcon: Icon(Icons.lock_outline),
                        border: OutlineInputBorder(),
                        helperText: 'Email cannot be changed',
                      ),
                    ),
                    const SizedBox(height: 32),

                    // Save Button
                    FilledButton(
                      style: FilledButton.styleFrom(
                        padding: const EdgeInsets.symmetric(vertical: 16),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                      ),
                      onPressed: isSaving ? null : _submit,
                      child: isSaving
                          ? const SizedBox(
                              height: 20,
                              width: 20,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : const Text(
                              'Save Changes',
                              style: TextStyle(fontSize: 16),
                            ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
