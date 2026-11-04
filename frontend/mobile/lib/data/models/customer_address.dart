class CustomerAddress {
  const CustomerAddress({
    required this.id,
    required this.label,
    required this.contactName,
    required this.contactPhone,
    required this.addressLine,
    required this.isDefault,
    this.latitude,
    this.longitude,
    this.createdAt,
    this.updatedAt,
  });

  final String id;
  final String label;
  final String contactName;
  final String contactPhone;
  final String addressLine;
  final double? latitude;
  final double? longitude;
  final bool isDefault;
  final String? createdAt;
  final String? updatedAt;

  factory CustomerAddress.fromJson(Map<String, dynamic> json) =>
      CustomerAddress(
        id: json['id'] as String,
        label: json['label'] as String,
        contactName: json['contactName'] as String,
        contactPhone: json['contactPhone'] as String,
        addressLine: json['addressLine'] as String,
        latitude: (json['latitude'] as num?)?.toDouble(),
        longitude: (json['longitude'] as num?)?.toDouble(),
        isDefault: json['isDefault'] as bool,
        createdAt: json['createdAt'] as String?,
        updatedAt: json['updatedAt'] as String?,
      );

  Map<String, dynamic> toJson() => {
    'label': label,
    'contactName': contactName,
    'contactPhone': contactPhone,
    'addressLine': addressLine,
    'latitude': latitude,
    'longitude': longitude,
    'isDefault': isDefault,
  };
}

class CustomerAddressRequest {
  const CustomerAddressRequest({
    required this.label,
    required this.contactName,
    required this.contactPhone,
    required this.addressLine,
    required this.isDefault,
    this.latitude,
    this.longitude,
  });

  final String label;
  final String contactName;
  final String contactPhone;
  final String addressLine;
  final double? latitude;
  final double? longitude;
  final bool isDefault;

  Map<String, dynamic> toJson() => {
    'label': label,
    'contactName': contactName,
    'contactPhone': contactPhone,
    'addressLine': addressLine,
    'latitude': latitude,
    'longitude': longitude,
    'isDefault': isDefault,
  };
}
