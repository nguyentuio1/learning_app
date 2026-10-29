import { Form, Input } from 'antd'

export function UserDetailsFields() {
  return <>
    <Form.Item name="fullName" label="Full name" rules={[{ required: true, whitespace: true, message: 'Enter a full name.' }, { max: 150 }]}>
      <Input maxLength={150} autoComplete="name" />
    </Form.Item>
    <div className="users-form-row">
      <Form.Item name="email" label="Email" rules={[{ required: true, message: 'Enter an email.' }, { type: 'email', message: 'Enter a valid email.' }, { max: 255 }]}>
        <Input maxLength={255} autoComplete="off" />
      </Form.Item>
      <Form.Item name="phoneNumber" label="Phone number" rules={[{ required: true, message: 'Enter a phone number.' }, { pattern: /^\d{9,11}$/, message: 'Enter 9 to 11 digits.' }]}>
        <Input inputMode="numeric" maxLength={11} autoComplete="tel" />
      </Form.Item>
    </div>
  </>
}
