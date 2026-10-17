/// Đối tượng người dùng trong domain layer (Dart thuần).
class User {
  const User({
    required this.id,
    required this.email,
    required this.fullName,
    required this.phoneNumber,
    this.avatarUrl,
    required this.status,
    required this.roleCode,
    required this.createdAt,
  });

  final String id;
  final String email;
  final String fullName;
  final String phoneNumber;
  final String? avatarUrl;
  final String status;
  final String roleCode;
  final DateTime createdAt;
}
