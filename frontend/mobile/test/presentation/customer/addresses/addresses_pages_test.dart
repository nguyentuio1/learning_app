import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/customer_address.dart';
import 'package:smart_drone_delivery/data/providers/address_providers.dart';
import 'package:smart_drone_delivery/data/services/address_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/addresses/addresses_list_page.dart';
import 'package:smart_drone_delivery/presentation/customer/addresses/add_edit_address_page.dart';

class _FakeAddressApiService extends AddressApiService {
  _FakeAddressApiService({List<CustomerAddress>? initialAddresses})
    : addresses = initialAddresses ?? [],
      super(Dio());

  final List<CustomerAddress> addresses;
  CustomerAddressRequest? lastRequest;
  String? lastMutation;

  @override
  Future<List<CustomerAddress>> getAddresses() async =>
      List.unmodifiable(addresses);

  @override
  Future<void> createAddress(CustomerAddressRequest address) async {
    lastMutation = 'create';
    lastRequest = address;
    addresses.add(_address(request: address));
  }

  @override
  Future<void> updateAddress(String id, CustomerAddressRequest address) async {
    lastMutation = 'update:$id';
    lastRequest = address;
  }

  @override
  Future<void> deleteAddress(String id) async {
    lastMutation = 'delete:$id';
    addresses.removeWhere((address) => address.id == id);
  }

  @override
  Future<void> setDefaultAddress(String id) async {
    lastMutation = 'default:$id';
  }
}

CustomerAddress _address({
  String id = 'address-1',
  String label = 'Home',
  bool isDefault = true,
  CustomerAddressRequest? request,
}) => CustomerAddress(
  id: id,
  addressLabel: request?.addressLabel ?? label,
  recipientName: request?.recipientName ?? 'Mai Tran',
  recipientPhone: request?.recipientPhone ?? '0901234567',
  streetAddress: request?.streetAddress ?? '12 Main Street',
  latitude: request?.latitude,
  longitude: request?.longitude,
  isDefault: request?.isDefault ?? isDefault,
);

Future<void> _pumpPage(
  WidgetTester tester,
  Widget page,
  _FakeAddressApiService service,
) async {
  tester.view.physicalSize = const Size(800, 1800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [addressApiServiceProvider.overrideWithValue(service)],
      child: MaterialApp(home: page),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('C20 displays addresses and default badge', (tester) async {
    final service = _FakeAddressApiService(
      initialAddresses: [
        _address(),
        _address(id: 'address-2', label: 'Office', isDefault: false),
      ],
    );

    await _pumpPage(tester, const AddressesListPage(), service);

    expect(find.text('Home'), findsOneWidget);
    expect(find.text('Office'), findsOneWidget);
    expect(find.text('Mặc định'), findsOneWidget);
    expect(find.text('Set default'), findsOneWidget);
    expect(find.text('Edit'), findsNWidgets(2));
    expect(find.text('Delete'), findsNWidgets(2));
  });

  testWidgets('C20 confirms delete using MSG-P-CO-04', (tester) async {
    final service = _FakeAddressApiService(initialAddresses: [_address()]);

    await _pumpPage(tester, const AddressesListPage(), service);
    await tester.tap(find.text('Delete'));
    await tester.pumpAndSettle();

    expect(find.text('Delete this address?'), findsOneWidget);
    expect(service.lastMutation, isNull);
    await tester.tap(find.widgetWithText(FilledButton, 'Delete'));
    await tester.pumpAndSettle();

    expect(service.lastMutation, 'delete:address-1');
    expect(find.text('No saved addresses'), findsOneWidget);
  });

  testWidgets('C20 sets default and opens the edit form', (tester) async {
    final service = _FakeAddressApiService(
      initialAddresses: [_address(isDefault: false)],
    );

    await _pumpPage(tester, const AddressesListPage(), service);
    await tester.tap(find.text('Set default'));
    await tester.pumpAndSettle();
    expect(service.lastMutation, 'default:address-1');

    await tester.tap(find.text('Edit'));
    await tester.pumpAndSettle();
    expect(find.text('Edit address'), findsOneWidget);
  });

  testWidgets('C21 validates required fields and phone format', (tester) async {
    final service = _FakeAddressApiService();

    await _pumpPage(tester, const AddEditAddressPage(), service);
    await tester.ensureVisible(find.text('Save address'));
    await tester.tap(find.text('Save address'));
    await tester.pumpAndSettle();

    expect(find.text('Label is required.'), findsOneWidget);
    expect(find.text('Recipient name is required.'), findsOneWidget);
    expect(find.text('Phone is required.'), findsOneWidget);
    expect(find.text('Address line is required.'), findsOneWidget);
    expect(service.lastMutation, isNull);

    await tester.enterText(_field(2), '12345');
    await tester.tap(find.text('Save address'));
    await tester.pumpAndSettle();
    expect(
      find.text('Phone number must contain 9 to 11 digits.'),
      findsOneWidget,
    );
  });

  testWidgets('C21 limits label and recipient name to their maximum lengths', (
    tester,
  ) async {
    final service = _FakeAddressApiService();

    await _pumpPage(tester, const AddEditAddressPage(), service);
    await tester.enterText(_field(0), 'L' * 51);
    await tester.enterText(_field(1), 'N' * 151);

    expect(
      tester.widget<TextFormField>(_field(0)).controller!.text,
      hasLength(50),
    );
    expect(
      tester.widget<TextFormField>(_field(1)).controller!.text,
      hasLength(150),
    );
    expect(service.lastMutation, isNull);
  });

  testWidgets('C21 creates address with parsed coordinates and default flag', (
    tester,
  ) async {
    final service = _FakeAddressApiService();

    await _pumpPage(tester, const AddEditAddressPage(), service);
    await tester.enterText(_field(0), 'Home');
    await tester.enterText(_field(1), 'Mai Tran');
    await tester.enterText(_field(2), '0901234567');
    await tester.enterText(_field(3), '12 Main Street');
    await tester.enterText(_field(4), '10.5');
    await tester.enterText(_field(5), '106.7');
    await tester.tap(find.text('Set as default address'));
    await tester.ensureVisible(find.text('Save address'));
    await tester.tap(find.text('Save address'));
    await tester.pumpAndSettle();

    expect(service.lastMutation, 'create');
    expect(service.lastRequest?.latitude, 10.5);
    expect(service.lastRequest?.longitude, 106.7);
    expect(service.lastRequest?.isDefault, isTrue);
  });

  testWidgets('C21 loads existing values and submits update', (tester) async {
    final service = _FakeAddressApiService(initialAddresses: [_address()]);

    await _pumpPage(
      tester,
      AddEditAddressPage(address: service.addresses.single),
      service,
    );
    expect(find.text('Edit address'), findsOneWidget);
    expect(find.text('Home'), findsOneWidget);

    await tester.ensureVisible(find.text('Save changes'));
    await tester.tap(find.text('Save changes'));
    await tester.pumpAndSettle();

    expect(service.lastMutation, 'update:address-1');
    expect(service.lastRequest?.addressLabel, 'Home');
  });
}

Finder _field(int index) => find.byType(TextFormField).at(index);
