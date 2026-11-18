BEGIN;
UPDATE delivery_orders SET "Status"='ARRIVED', "ActualDeliveryTime"=NULL, "UpdatedAt"=now()
WHERE "TrackingNumber"='ORD-DEMO-003';
DELETE FROM delivery_status_logs WHERE "OrderId"=(SELECT "Id" FROM delivery_orders WHERE "TrackingNumber"='ORD-DEMO-003') AND "ToStatus"='DELIVERED';
UPDATE delivery_status_logs SET "CreatedAt"=now(), "Remarks"='Prepared ARRIVED precondition for LA-364 operator UI test; flight not executed'
WHERE "OrderId"=(SELECT "Id" FROM delivery_orders WHERE "TrackingNumber"='ORD-DEMO-003') AND "ToStatus"='ARRIVED';
UPDATE flight_missions SET "DestinationSlotId"=(
 SELECT s."Id" FROM station_slots s JOIN delivery_orders o ON o."DestinationStationId"=s."StationId"
 WHERE o."TrackingNumber"='ORD-DEMO-003' ORDER BY s."SlotNumber" LIMIT 1), "FlightStatus"='COMPLETED', "CreatedAt"=now()
WHERE "OrderId"=(SELECT "Id" FROM delivery_orders WHERE "TrackingNumber"='ORD-DEMO-003');
UPDATE station_slots SET "Status"='OCCUPIED', "LastUpdated"=now() WHERE "Id"=(
 SELECT m."DestinationSlotId" FROM flight_missions m JOIN delivery_orders o ON o."Id"=m."OrderId" WHERE o."TrackingNumber"='ORD-DEMO-003');
COMMIT;
