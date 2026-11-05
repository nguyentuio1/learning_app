class RegisterRequestDto {
  const RegisterRequestDto({
    required this.email,
    required this.password,
    required this.fullName,
    required this.phoneNumber,
  });

  final String email;
  final String password;
  final String fullName;
  final String phoneNumber;

  Map<String, dynamic> toJson() => {
    'email': email,
    'password': password,
    'fullName': fullName,
    'phoneNumber': phoneNumber,
  };
}
