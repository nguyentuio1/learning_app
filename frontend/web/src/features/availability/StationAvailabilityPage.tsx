import { useCallback, useEffect, useState } from 'react'
import { Alert, Button, Progress, Select, Tag } from 'antd'
import { OperationsPage, Metrics, ResourceState, StatusTag } from '../operations/OperationsUi'
import { useResource } from '../operations/useResource'
import { availabilityApi } from './availabilityApi'

function LiveAvailability({ stationId }: { stationId: string }) {
  const load = useCallback((signal: AbortSignal) => availabilityApi.get(stationId, signal), [stationId])
  const resource = useResource(load)
  const { reload } = resource
  useEffect(() => {
    function update() { if (document.visibilityState === 'visible') reload() }
    const timer = window.setInterval(update, 30000)
    document.addEventListener('visibilitychange', update)
    return () => { window.clearInterval(timer); document.removeEventListener('visibilitychange', update) }
  }, [reload])
  const snapshot = resource.data
  return <section aria-label="Station availability">
    <div className="operations-actions"><Button onClick={reload} disabled={resource.loading}>Refresh availability</Button><span className="operations-small">Updates every 30 seconds while this page is visible.</span></div>
    <ResourceState loading={resource.loading} error={resource.error} retry={reload} />
    {resource.error != null && <p role="status">Availability is unknown until a fresh check succeeds.</p>}
    {snapshot && <>
      <section className="operations-panel">
        <header className="operations-heading"><div><h2>Current availability</h2><StatusTag status={snapshot.stationStatus} /></div><time dateTime={snapshot.checkedAt}>Checked {new Date(snapshot.checkedAt).toLocaleString()}</time></header>
        <div className="operations-actions">
          <Tag color={snapshot.isAvailableForArrival ? 'green' : 'orange'}>Arrivals {snapshot.isAvailableForArrival ? 'available' : 'unavailable'}</Tag>
          <Tag color={snapshot.isAvailableForDeparture ? 'green' : 'orange'}>Departures {snapshot.isAvailableForDeparture ? 'available' : 'unavailable'}</Tag>
        </div>
        {(!snapshot.isAvailableForArrival || !snapshot.isAvailableForDeparture) && <Alert type="warning" showIcon title="Station availability is restricted" description="Check the station status and slot inventory before planning a delivery." />}
        <Progress percent={snapshot.totalSlots ? Math.round(snapshot.emptySlots / snapshot.totalSlots * 100) : 0} strokeColor="#4b7856" railColor="#e6eedc" format={() => snapshot.emptySlots + ' empty / ' + snapshot.totalSlots + ' total'} />
      </section>
      <Metrics items={[
        { label: 'Total slots', value: snapshot.totalSlots }, { label: 'Empty', value: snapshot.emptySlots },
        { label: 'Occupied', value: snapshot.occupiedSlots }, { label: 'Reserved', value: snapshot.reservedSlots }, { label: 'Errors', value: snapshot.errorSlots },
      ]} />
    </>}
  </section>
}

export function StationAvailabilityPage() {
  const stations = useResource(availabilityApi.stations)
  const [stationId, setStationId] = useState<string>()
  return <OperationsPage title="Station availability" subtitle="Check arrival, departure and slot availability across the network.">
    <section className="operations-panel"><label htmlFor="availability-station">Station</label>
      <Select id="availability-station" aria-label="Station" value={stationId} onChange={setStationId} style={{ width: '100%', marginTop: 8 }} loading={stations.loading} placeholder="Choose a station" options={stations.data?.map(station => ({ value: station.id, label: station.name + ' · ' + station.stationCode }))} />
      <ResourceState loading={false} error={stations.error} retry={stations.reload} empty={stations.data?.length === 0} />
    </section>
    {stationId && <LiveAvailability key={stationId} stationId={stationId} />}
  </OperationsPage>
}
