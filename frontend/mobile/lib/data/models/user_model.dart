import '../../domain/entities/user.dart';

/// DTO người dùng từ backend API (`UserDto`).
class UserModel {
  const UserModel({
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

  factory UserModel.fromJson(Map<String, dynamic> json) {
    final role = json['role'];
    final roleCode = role is Map<String, dynamic>
        ? (role['code'] as String? ?? 'CUSTOMER')
        : (role?.toString() ?? 'CUSTOMER');

    return UserModel(
      id: json['id']?.toString() ?? '',
      email: json['email'] as String? ?? '',
      fullName:
          json['fullName'] as String? ?? json['full_name'] as String? ?? '',
      phoneNumber: json['phoneNumber'] as String? ??
          json['phone_number'] as String? ??
          '',
      avatarUrl:
          json['avatarUrl'] as String? ?? json['avatar_url'] as String?,
      status: json['status'] as String? ?? 'ACTIVE',
      roleCode: roleCode,
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'].toString()) ?? DateTime.now()
          : (json['created_at'] != null
              ? DateTime.tryParse(json['created_at'].toString()) ?? DateTime.now()
              : DateTime.now()),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'email': email,
        'fullName': fullName,
        'phoneNumber': phoneNumber,
        if (avatarUrl != null) 'avatarUrl': avatarUrl,
        'status': status,
        'roleCode': roleCode,
        'createdAt': createdAt.toIso8601String(),
      };

  User toEntity() => User(
        id: id,
        email: email,
        fullName: fullName,
        phoneNumber: phoneNumber,
        avatarUrl: avatarUrl,
        status: status,
        roleCode: roleCode,
        createdAt: createdAt,
      );
}
