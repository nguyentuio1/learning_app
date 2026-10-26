class CustomerAddress {
  const CustomerAddress({
    required this.id,
    required this.addressLabel,
    required this.recipientName,
    required this.recipientPhone,
    required this.streetAddress,
    required this.isDefault,
    this.latitude,
    this.longitude,
  });

  final String id;
  final String addressLabel;
  final String recipientName;
  final String recipientPhone;
  final String streetAddress;
  final double? latitude;
  final double? longitude;
  final bool isDefault;

  factory CustomerAddress.fromJson(Map<String, dynamic> json) =>
      CustomerAddress(
        id: json['id'] as String,
        addressLabel: json['addressLabel'] as String,
        recipientName: json['recipientName'] as String,
        recipientPhone: json['recipientPhone'] as String,
        streetAddress: json['streetAddress'] as String,
        latitude: (json['latitude'] as num?)?.toDouble(),
        longitude: (json['longitude'] as num?)?.toDouble(),
        isDefault: json['isDefault'] as bool,
      );

  Map<String, dynamic> toJson() => {
    'id': id,
    'addressLabel': addressLabel,
    'recipientName': recipientName,
    'recipientPhone': recipientPhone,
    'streetAddress': streetAddress,
    'latitude': latitude,
    'longitude': longitude,
    'isDefault': isDefault,
  };
}

class CustomerAddressRequest {
  const CustomerAddressRequest({
    required this.addressLabel,
    required this.recipientName,
    required this.recipientPhone,
    required this.streetAddress,
    required this.isDefault,
    this.latitude,
    this.longitude,
  });

  final String addressLabel;
  final String recipientName;
  final String recipientPhone;
  final String streetAddress;
  final double? latitude;
  final double? longitude;
  final bool isDefault;

  Map<String, dynamic> toJson() => {
    'addressLabel': addressLabel,
    'recipientName': recipientName,
    'recipientPhone': recipientPhone,
    'streetAddress': streetAddress,
    'latitude': latitude,
    'longitude': longitude,
    'isDefault': isDefault,
  };
}
