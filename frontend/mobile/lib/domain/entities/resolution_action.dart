enum ResolutionAction {
  reschedule('RESCHEDULE'),
  returnToOrigin('RETURN_TO_ORIGIN'),
  cancel('CANCEL');

  const ResolutionAction(this.value);

  final String value;
}
