import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
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
  Object? getAddressesError;
  Object? mutationError;

  @override
  Future<List<CustomerAddress>> getAddresses() async {
    if (getAddressesError case final error?) throw error;
    return List.unmodifiable(addresses);
  }

  @override
  Future<void> createAddress(CustomerAddressRequest address) async {
    lastMutation = 'create';
    lastRequest = address;
    if (mutationError case final error?) throw error;
    addresses.add(_address(id: 'created', request: address));
  }

  @override
  Future<void> updateAddress(String id, CustomerAddressRequest address) async {
    lastMutation = 'update:$id';
    lastRequest = address;
    if (mutationError case final error?) throw error;
    final index = addresses.indexWhere((item) => item.id == id);
    if (index >= 0) addresses[index] = _address(id: id, request: address);
  }

  @override
  Future<void> deleteAddress(String id) async {
    lastMutation = 'delete:$id';
    if (mutationError case final error?) throw error;
    addresses.removeWhere((address) => address.id == id);
  }

  @override
  Future<void> setDefaultAddress(String id) async {
    lastMutation = 'default:$id';
    if (mutationError case final error?) throw error;
    for (var index = 0; index < addresses.length; index++) {
      final address = addresses[index];
      addresses[index] = CustomerAddress(
        id: address.id,
        label: address.label,
        contactName: address.contactName,
        contactPhone: address.contactPhone,
        addressLine: address.addressLine,
        latitude: address.latitude,
        longitude: address.longitude,
        isDefault: address.id == id,
        createdAt: address.createdAt,
        updatedAt: address.updatedAt,
      );
    }
  }
}

CustomerAddress _address({
  String id = 'address-1',
  String label = 'Home',
  bool isDefault = true,
  CustomerAddressRequest? request,
}) => CustomerAddress(
  id: id,
  label: request?.label ?? label,
  contactName: request?.contactName ?? 'Mai Tran',
  contactPhone: request?.contactPhone ?? '0901234567',
  addressLine: request?.addressLine ?? '12 Main Street',
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

Future<void> _fillValidForm(WidgetTester tester) async {
  await tester.enterText(_field(0), 'Home');
  await tester.enterText(_field(1), 'Mai Tran');
  await tester.enterText(_field(2), '0901234567');
  await tester.enterText(_field(3), '12 Main Street');
}

void main() {
  testWidgets('C20 displays addresses and the default badge', (tester) async {
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

  testWidgets('C20 confirms delete before making the API call', (tester) async {
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

  testWidgets('C20 reloads and displays the newly selected default', (
    tester,
  ) async {
    final service = _FakeAddressApiService(
      initialAddresses: [
        _address(),
        _address(id: 'address-2', label: 'Office', isDefault: false),
      ],
    );

    await _pumpPage(tester, const AddressesListPage(), service);
    await tester.tap(
      find.ancestor(
        of: find.text('Set default'),
        matching: find.byType(OutlinedButton),
      ),
    );
    await tester.pumpAndSettle();

    expect(service.lastMutation, 'default:address-2');
    expect(find.text('Mặc định'), findsOneWidget);
    expect(
      find.ancestor(of: find.text('Office'), matching: find.byType(Card)),
      findsOneWidget,
    );

    await tester.tap(find.text('Edit').last);
    await tester.pumpAndSettle();
    expect(find.text('Edit address'), findsOneWidget);
  });

  testWidgets(
    'C20 displays the API 404 for an address outside the owner scope',
    (tester) async {
      final service = _FakeAddressApiService()
        ..getAddressesError = const ApiException(
          message: 'Address not found.',
          statusCode: 404,
          code: 'address_not_found',
        );

      await _pumpPage(tester, const AddressesListPage(), service);

      expect(find.text('Address not found.'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    },
  );

  testWidgets('C20 displays a delete 404 instead of treating it as success', (
    tester,
  ) async {
    final service = _FakeAddressApiService(initialAddresses: [_address()])
      ..mutationError = const ApiException(
        message: 'Address not found.',
        statusCode: 404,
        code: 'address_not_found',
      );

    await _pumpPage(tester, const AddressesListPage(), service);
    await tester.tap(find.text('Delete'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Delete'));
    await tester.pumpAndSettle();

    expect(service.lastMutation, 'delete:address-1');
    expect(find.text('Address not found.'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
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

  testWidgets('C21 limits label/name lengths and requires coordinate pairs', (
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

    await _fillValidForm(tester);
    await tester.enterText(_field(4), '10.5');
    await tester.ensureVisible(find.text('Save address'));
    await tester.tap(find.text('Save address'));
    await tester.pumpAndSettle();

    expect(
      find.text('Longitude is required when Latitude is provided.'),
      findsOneWidget,
    );
    expect(service.lastMutation, isNull);
  });

  testWidgets('C21 creates address with coordinates and default flag', (
    tester,
  ) async {
    final service = _FakeAddressApiService();

    await _pumpPage(tester, const AddEditAddressPage(), service);
    await _fillValidForm(tester);
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

  testWidgets('C21 loads existing fields and submits an update', (
    tester,
  ) async {
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
    expect(service.lastRequest?.label, 'Home');
  });

  testWidgets('C21 displays RFC 7807 400 and 409 validation errors', (
    tester,
  ) async {
    for (final error in [
      const ApiException(
        message: 'Phone number is invalid.',
        statusCode: 400,
        code: 'validation_error',
      ),
      const ApiException(
        message: 'You have reached the limit of 10 addresses.',
        statusCode: 409,
        code: 'address_limit_reached',
      ),
    ]) {
      final service = _FakeAddressApiService()..mutationError = error;
      await _pumpPage(tester, const AddEditAddressPage(), service);
      await _fillValidForm(tester);
      await tester.ensureVisible(find.text('Save address'));
      await tester.tap(find.text('Save address'));
      await tester.pumpAndSettle();

      expect(find.text(error.message), findsOneWidget);
      expect(service.lastMutation, 'create');
      await tester.pumpWidget(const SizedBox.shrink());
    }
  });
}

Finder _field(int index) => find.byType(TextFormField).at(index);
