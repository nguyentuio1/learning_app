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

  User copyWith({
    String? id,
    String? email,
    String? fullName,
    String? phoneNumber,
    String? avatarUrl,
    String? status,
    String? roleCode,
    DateTime? createdAt,
  }) =>
      User(
        id: id ?? this.id,
        email: email ?? this.email,
        fullName: fullName ?? this.fullName,
        phoneNumber: phoneNumber ?? this.phoneNumber,
        avatarUrl: avatarUrl ?? this.avatarUrl,
        status: status ?? this.status,
        roleCode: roleCode ?? this.roleCode,
        createdAt: createdAt ?? this.createdAt,
      );

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is User &&
          runtimeType == other.runtimeType &&
          id == other.id &&
          email == other.email &&
          fullName == other.fullName &&
          phoneNumber == other.phoneNumber &&
          avatarUrl == other.avatarUrl &&
          status == other.status &&
          roleCode == other.roleCode;

  @override
  int get hashCode => Object.hash(
        id,
        email,
        fullName,
        phoneNumber,
        avatarUrl,
        status,
        roleCode,
      );
}
