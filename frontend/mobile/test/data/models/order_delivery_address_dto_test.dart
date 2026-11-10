import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_detail_response_dto.dart';

void main() {
  group('OrderDeliveryAddressDto.fromJson', () {
    test('reads the field names returned by the API', () {
      final dto = OrderDeliveryAddressDto.fromJson({
        'id': 'a1',
        'label': 'Home',
        'contactName': 'Customer Name',
        'contactPhone': '0901234567',
        'addressLine': '321 Test Street, Thu Duc',
      });

      expect(dto.addressLabel, 'Home');
      expect(dto.recipientName, 'Customer Name');
      expect(dto.recipientPhone, '0901234567');
      expect(dto.streetAddress, '321 Test Street, Thu Duc');
    });

    test('still accepts the legacy field names', () {
      final dto = OrderDeliveryAddressDto.fromJson({
        'addressLabel': 'Office',
        'recipientName': 'Legacy Name',
        'recipientPhone': '0907654321',
        'streetAddress': '1 Main Street',
      });

      expect(dto.addressLabel, 'Office');
      expect(dto.recipientName, 'Legacy Name');
      expect(dto.recipientPhone, '0907654321');
      expect(dto.streetAddress, '1 Main Street');
    });
  });
}
