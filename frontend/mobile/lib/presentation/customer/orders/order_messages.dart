import '../../../core/error/api_exception.dart';
import '../../../core/error/error_message.dart';

String orderOperationErrorMessage(Object error, {required bool cancelling}) {
  if (error is ApiException) {
    if (error.code == 'MSG-API-010' ||
        error.message == 'Phone number must be 9 to 11 digits.') {
      return 'Phone number must be 9 to 11 digits.';
    }
    if (cancelling) {
      if (error.code == 'MSG-P-CO-22' || error.statusCode == 409) {
        return 'This order can no longer be cancelled.';
      }
      if (error.code == 'MSG-P-CO-25' || error.statusCode == 404) {
        return 'Order not found or unavailable to your account.';
      }
      if (error.code == 'MSG-P-CO-20') {
        return 'Enter a cancellation reason.';
      }
    } else {
      if (error.code == 'MSG-P-CO-17' || error.statusCode == 409) {
        return 'Only pending orders can be edited.';
      }
      if (error.code == 'MSG-P-CO-18' || error.statusCode == 400) {
        return 'These order fields cannot be changed.';
      }
    }
  }
  return errorMessage(error);
}
