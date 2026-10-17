import { useEffect, useState } from 'react'
import axios from 'axios'
import { Alert, Button, Card, Descriptions, Skeleton, Tag, Typography } from 'antd'
import { profileApi } from '../services/profileApi'
import type { UserProfile } from '../types/profile'
import { profileError, type ProfileError } from '../utils/profileErrors'
import { useAuth } from '../hooks/useAuth'
import { AvatarSettings, PasswordSettings, ProfileDetails } from '../components/profile/ProfileForms'
import './profile.css'

const { Title, Text, Paragraph } = Typography

export function ProfilePage() {
  const { updateProfile } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [issue, setIssue] = useState<ProfileError | null>(null)
  const [attempt, setAttempt] = useState(0)
  useEffect(() => { if (profile) updateProfile(profile) }, [profile, updateProfile])

  useEffect(() => {
    const request = new AbortController()
    profileApi.get(request.signal).then(user => {
      if (!request.signal.aborted) setProfile(user)
    }).catch(cause => {
      if (!request.signal.aborted && !axios.isCancel(cause)) setIssue(profileError(cause, 'load'))
    }).finally(() => {
      if (!request.signal.aborted) setLoading(false)
    })
    return () => request.abort()
  }, [attempt])

  function retry() {
    setIssue(null); setProfile(null); setLoading(true); setAttempt(value => value + 1)
  }
  function accessError(error: ProfileError) {
    setProfile(null)
    setIssue(error)
  }

  return <main className="profile-page">
    <header className="profile-heading">
      <div><Text className="profile-eyebrow">MY ACCOUNT</Text><Title level={1}>My profile</Title>
        <Paragraph type="secondary">Personal details and account security.</Paragraph></div>
      {profile && <Tag>{profile.role.name}</Tag>}
    </header>
    {loading ? <section aria-busy="true" aria-label="Loading profile"><Card><Skeleton active avatar paragraph={{ rows: 5 }} /></Card></section>
      : issue ? <Card><Alert showIcon type={issue.accessDenied ? 'warning' : 'error'}
        title={issue.accessDenied ? 'Account access required' : 'Unable to load profile'} description={issue.message}
        action={<Button onClick={retry}>Try again</Button>} /></Card>
        : profile && <div className="profile-grid" key={profile.id}>
          <section className="profile-forms" aria-label="Profile settings">
            <ProfileDetails profile={profile} onSaved={updated => setProfile(current => current ? { ...updated, avatarUrl: current.avatarUrl } : updated)} onAccessError={accessError}
              avatar={<AvatarSettings profile={profile} onSaved={avatarUrl => setProfile(current => current ? { ...current, avatarUrl } : current)} onAccessError={accessError} />} />
            <PasswordSettings onAccessError={accessError} />
          </section>
          <aside><Card title="Account"><Descriptions column={1} size="small" items={[
            { key: 'email', label: 'Email', children: profile.email },
            { key: 'role', label: 'Role', children: profile.role.name },
            { key: 'status', label: 'Status', children: <Tag color="green">{profile.status}</Tag> },
            { key: 'created', label: 'Created', children: new Date(profile.createdAt).toLocaleDateString() },
          ]} /><Paragraph type="secondary" className="profile-readonly-note">Email and role cannot be changed from your profile.</Paragraph></Card></aside>
        </div>}
  </main>
}
